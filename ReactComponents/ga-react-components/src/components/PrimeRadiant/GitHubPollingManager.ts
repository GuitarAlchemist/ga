// src/components/PrimeRadiant/GitHubPollingManager.ts
// Central GitHub API polling manager — single polling loop, ETag caching,
// rate-limit awareness, subscriber-based reactive data distribution.

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export type GitHubDataType =
  | 'milestones'
  | 'pulls-open'
  | 'pulls-closed'
  | 'commits'
  | 'issues'
  | 'workflow-runs';

type Subscriber = (data: Map<string, unknown[]>) => void;

interface CacheEntry {
  data: unknown[];
  etag: string | null;
  fetchedAt: number;
}

interface Subscription {
  dataType: GitHubDataType;
  repos: string[];
  callback: Subscriber;
}

export interface RateLimitState {
  remaining: number;
  resetAt: Date;
}

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
const GITHUB_OWNER = 'GuitarAlchemist';
const GITHUB_API = 'https://api.github.com';
const CACHE_TTL_MS = 30_000;        // 30s cache TTL
const DEFAULT_POLL_MS = 60_000;      // 60s default interval
const THROTTLED_POLL_MS = 300_000;   // 5 min when rate-limited
const RATE_LIMIT_SLOW = 100;         // slow down below this
const RATE_LIMIT_STOP = 10;          // stop polling below this

// Resolve GitHub token once at module level (same pattern as existing panels)
const githubToken: string | null =
  (typeof import.meta !== 'undefined' && (import.meta as Record<string, unknown>).env
    ? (((import.meta as Record<string, unknown>).env) as Record<string, string | undefined>)['VITE_GITHUB_TOKEN'] ?? null
    : null)
  ?? (typeof localStorage !== 'undefined' ? localStorage.getItem('ga-github-token') : null);

// ---------------------------------------------------------------------------
// URL builders per data type
// ---------------------------------------------------------------------------
function buildUrl(dataType: GitHubDataType, repo: string): string {
  const base = `${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}`;
  switch (dataType) {
    case 'milestones':
      return `${base}/milestones?state=open&per_page=5`;
    case 'pulls-open':
      return `${base}/pulls?state=open&per_page=5&sort=updated&direction=desc`;
    case 'pulls-closed':
      return `${base}/pulls?state=closed&per_page=3&sort=updated&direction=desc`;
    case 'commits':
      return `${base}/commits?per_page=8`;
    case 'issues':
      return `${base}/issues?state=open&per_page=10&sort=updated`;
    case 'workflow-runs':
      return `${base}/actions/runs?per_page=5`;
  }
}

// ---------------------------------------------------------------------------
// Singleton class
// ---------------------------------------------------------------------------
class GitHubPollingManagerImpl {
  private cache = new Map<string, CacheEntry>(); // key: "dataType:repo"
  private subscriptions: Subscription[] = [];
  private intervalId: ReturnType<typeof setInterval> | null = null;
  private currentIntervalMs = DEFAULT_POLL_MS;
  private rateLimitRemaining = Infinity;
  private rateLimitResetAt = new Date(0);
  private polling = false;

  // ── Cache key ──
  private cacheKey(dataType: GitHubDataType, repo: string): string {
    return `${dataType}:${repo}`;
  }

  // ── Compute which (dataType, repo) pairs are needed ──
  private getNeededPairs(): Array<{ dataType: GitHubDataType; repo: string }> {
    const seen = new Set<string>();
    const pairs: Array<{ dataType: GitHubDataType; repo: string }> = [];
    for (const sub of this.subscriptions) {
      for (const repo of sub.repos) {
        const key = this.cacheKey(sub.dataType, repo);
        if (!seen.has(key)) {
          seen.add(key);
          pairs.push({ dataType: sub.dataType, repo });
        }
      }
    }
    return pairs;
  }

  // ── Single fetch with ETag support ──
  private async fetchOne(dataType: GitHubDataType, repo: string): Promise<unknown[]> {
    const key = this.cacheKey(dataType, repo);
    const cached = this.cache.get(key);

    // Return cache if within TTL
    if (cached && Date.now() - cached.fetchedAt < CACHE_TTL_MS) {
      return cached.data;
    }

    const headers: HeadersInit = {
      'Accept': 'application/vnd.github.v3+json',
      ...(githubToken ? { 'Authorization': `Bearer ${githubToken}` } : {}),
      ...(cached?.etag ? { 'If-None-Match': cached.etag } : {}),
    };

    try {
      const res = await fetch(buildUrl(dataType, repo), { headers });

      // Update rate limit state
      const remaining = res.headers.get('X-RateLimit-Remaining');
      const reset = res.headers.get('X-RateLimit-Reset');
      if (remaining !== null) this.rateLimitRemaining = parseInt(remaining, 10);
      if (reset !== null) this.rateLimitResetAt = new Date(parseInt(reset, 10) * 1000);

      // 304 Not Modified — return cached data, refresh timestamp
      if (res.status === 304 && cached) {
        cached.fetchedAt = Date.now();
        return cached.data;
      }

      if (!res.ok) return cached?.data ?? [];

      const etag = res.headers.get('ETag');
      let data = await res.json();

      // workflow-runs nests under .workflow_runs
      if (dataType === 'workflow-runs' && data.workflow_runs) {
        data = data.workflow_runs;
      }

      if (!Array.isArray(data)) data = [];

      this.cache.set(key, { data, etag, fetchedAt: Date.now() });
      return data;
    } catch {
      return cached?.data ?? [];
    }
  }

  // ── Poll all needed pairs and notify subscribers ──
  private async pollAll(): Promise<void> {
    if (this.polling) return;
    if (this.rateLimitRemaining < RATE_LIMIT_STOP) return; // hard stop

    this.polling = true;
    try {
      const pairs = this.getNeededPairs();
      // Fetch in parallel (batches of 6 to avoid flooding)
      const batchSize = 6;
      for (let i = 0; i < pairs.length; i += batchSize) {
        const batch = pairs.slice(i, i + batchSize);
        await Promise.all(batch.map(p => this.fetchOne(p.dataType, p.repo)));
      }
      this.notifyAll();
      this.adaptInterval();
    } finally {
      this.polling = false;
    }
  }

  // ── Notify each subscriber with their requested data ──
  private notifyAll(): void {
    for (const sub of this.subscriptions) {
      const map = new Map<string, unknown[]>();
      for (const repo of sub.repos) {
        const key = this.cacheKey(sub.dataType, repo);
        const cached = this.cache.get(key);
        map.set(repo, cached?.data ?? []);
      }
      try {
        sub.callback(map);
      } catch {
        // subscriber error — ignore
      }
    }
  }

  // ── Adapt polling interval based on rate limit ──
  private adaptInterval(): void {
    const desiredMs =
      this.rateLimitRemaining < RATE_LIMIT_SLOW ? THROTTLED_POLL_MS : DEFAULT_POLL_MS;
    if (desiredMs !== this.currentIntervalMs) {
      this.currentIntervalMs = desiredMs;
      this.stopInterval();
      this.startInterval();
    }
  }

  // ── Interval management ──
  private startInterval(): void {
    if (this.intervalId !== null) return;
    this.intervalId = setInterval(() => this.pollAll(), this.currentIntervalMs);
  }

  private stopInterval(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }

  // ═══════════════════════════════════════════════════════════════════════════
  // Public API
  // ═══════════════════════════════════════════════════════════════════════════

  /**
   * Subscribe to a data type for a set of repos.
   * Callback receives a Map<repo, data[]> on every poll cycle.
   * Returns an unsubscribe function.
   */
  subscribe(
    dataType: GitHubDataType,
    repos: string[],
    callback: Subscriber,
  ): () => void {
    const sub: Subscription = { dataType, repos, callback };
    this.subscriptions.push(sub);

    // Start polling if this is the first subscriber
    if (this.subscriptions.length === 1) {
      this.pollAll(); // initial fetch
      this.startInterval();
    } else {
      // Immediately notify with cached data if available
      const map = new Map<string, unknown[]>();
      let hasCached = false;
      for (const repo of repos) {
        const key = this.cacheKey(dataType, repo);
        const cached = this.cache.get(key);
        if (cached) {
          map.set(repo, cached.data);
          hasCached = true;
        } else {
          map.set(repo, []);
        }
      }
      if (hasCached) {
        try { callback(map); } catch { /* ignore */ }
      }
      // Trigger a fresh poll for any missing data
      this.pollAll();
    }

    return () => {
      const idx = this.subscriptions.indexOf(sub);
      if (idx >= 0) this.subscriptions.splice(idx, 1);
      // Stop polling if no more subscribers
      if (this.subscriptions.length === 0) {
        this.stopInterval();
      }
    };
  }

  /**
   * Get last cached data for a specific (dataType, repo) pair.
   */
  getLastData(dataType: GitHubDataType, repo: string): unknown[] | null {
    const cached = this.cache.get(this.cacheKey(dataType, repo));
    return cached?.data ?? null;
  }

  /**
   * Current rate limit state.
   */
  getRateLimitState(): RateLimitState {
    return {
      remaining: this.rateLimitRemaining === Infinity ? -1 : this.rateLimitRemaining,
      resetAt: this.rateLimitResetAt,
    };
  }

  /**
   * Whether the manager considers itself rate-limited (below RATE_LIMIT_SLOW).
   */
  isRateLimited(): boolean {
    return this.rateLimitRemaining < RATE_LIMIT_SLOW;
  }

  /**
   * Force an immediate refresh of all subscribed data.
   */
  async refresh(): Promise<void> {
    await this.pollAll();
  }
}

// ---------------------------------------------------------------------------
// Singleton export
// ---------------------------------------------------------------------------
export const gitHubPollingManager = new GitHubPollingManagerImpl();
