// src/components/PrimeRadiant/ActivityPanel.tsx
// Top-left accordion panel — Activities, Commits, and more

import React, { useEffect, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export interface Activity {
  id: string;
  name: string;
  status: 'active' | 'pending' | 'completed' | 'blocked';
  progress: number;
  eta?: string;
  category: 'governance' | 'research' | 'build' | 'test' | 'deploy';
}

interface CommitInfo {
  hash: string;
  message: string;
  repo: string;
  time: string;
  timestamp: number; // epoch ms for sorting
}

interface IssueInfo {
  number: number;
  title: string;
  repo: string;
  url: string;
}

// ---------------------------------------------------------------------------
// GitHub API configuration
// ---------------------------------------------------------------------------
const GITHUB_OWNER = 'GuitarAlchemist';
const GITHUB_REPOS = ['ga', 'Demerzel', 'hari', 'tars', 'ix', 'demerzel-bot', 'guitar-singularity'];
const GITHUB_API = 'https://api.github.com';

// Resolve GitHub token once at module level (priority: env var > localStorage > none)
const githubToken: string | null =
  (typeof import.meta !== 'undefined' && (import.meta as Record<string, unknown>).env
    ? (((import.meta as Record<string, unknown>).env) as Record<string, string | undefined>)['VITE_GITHUB_TOKEN'] ?? null
    : null)
  ?? (typeof localStorage !== 'undefined' ? localStorage.getItem('ga-github-token') : null);

const GITHUB_HEADERS: HeadersInit = {
  'Accept': 'application/vnd.github.v3+json',
  ...(githubToken ? { 'Authorization': `Bearer ${githubToken}` } : {}),
};

const DEFAULT_REFRESH_MS = 60_000;
const THROTTLED_REFRESH_MS = 300_000; // 5 minutes when rate-limited
const RATE_LIMIT_THRESHOLD = 10;

// Shared mutable rate-limit state (updated by every GitHub fetch)
let rateLimitRemaining: number | null = null;

function updateRateLimit(res: Response): void {
  const header = res.headers.get('X-RateLimit-Remaining');
  if (header !== null) {
    rateLimitRemaining = parseInt(header, 10);
  }
}

function isRateLimited(): boolean {
  return rateLimitRemaining !== null && rateLimitRemaining < RATE_LIMIT_THRESHOLD;
}

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

// ─── Fetch real commits from GitHub API ───
async function fetchCommits(): Promise<CommitInfo[]> {
  try {
    const allCommits: CommitInfo[] = [];
    for (const repo of GITHUB_REPOS) {
      const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/commits?per_page=8`, {
        headers: GITHUB_HEADERS,
      });
      updateRateLimit(res);
      if (!res.ok) continue;
      const data = await res.json();
      for (const commit of data) {
        const dateStr = commit.commit?.committer?.date ?? new Date().toISOString();
        allCommits.push({
          hash: commit.sha ?? '?',
          message: commit.commit?.message?.split('\n')[0] ?? '',
          repo,
          time: timeAgo(dateStr),
          timestamp: new Date(dateStr).getTime(),
        });
      }
    }
    return allCommits.sort((a, b) => b.timestamp - a.timestamp).slice(0, 15);
  } catch {
    return []; // silent fail — panel shows empty
  }
}

// ─── Fetch real issues from GitHub API ───
async function fetchIssues(): Promise<IssueInfo[]> {
  try {
    const allIssues: IssueInfo[] = [];
    for (const repo of GITHUB_REPOS) {
      const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/issues?state=open&per_page=10&sort=updated`, {
        headers: GITHUB_HEADERS,
      });
      updateRateLimit(res);
      if (!res.ok) continue;
      const data = await res.json();
      for (const issue of data) {
        if (issue.pull_request) continue; // skip PRs
        allIssues.push({
          number: issue.number,
          title: issue.title,
          repo,
          url: issue.html_url,
        });
      }
    }
    return allIssues.slice(0, 10);
  } catch {
    return [];
  }
}

// ─── Category inference from repo name ───
function categoryForRepo(repo: string): Activity['category'] {
  switch (repo) {
    case 'Demerzel':
    case 'demerzel-bot':
      return 'governance';
    case 'hari':
      return 'research';
    default:
      return 'build';
  }
}

// ─── Derive activities from GitHub PRs and milestones ───
async function fetchActivities(): Promise<Activity[]> {
  const headers = GITHUB_HEADERS;
  const activities: Activity[] = [];

  // 1. Fetch open milestones from all repos
  const milestonePromises = GITHUB_REPOS.map(async (repo) => {
    try {
      const res = await fetch(
        `${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/milestones?state=open&per_page=5`,
        { headers },
      );
      updateRateLimit(res);
      if (!res.ok) return [];
      const data: Array<{
        id: number;
        title: string;
        open_issues: number;
        closed_issues: number;
        due_on: string | null;
      }> = await res.json();
      return data.map((ms) => {
        const total = ms.open_issues + ms.closed_issues;
        const progress = total > 0 ? Math.round((ms.closed_issues / total) * 100) : 0;
        const now = Date.now();
        const dueSoon = ms.due_on && new Date(ms.due_on).getTime() - now < 7 * 24 * 3600 * 1000;
        const overdue = ms.due_on && new Date(ms.due_on).getTime() < now;
        const status: Activity['status'] = overdue ? 'blocked' : dueSoon ? 'active' : 'pending';
        return {
          id: `ms-${repo}-${ms.id}`,
          name: `[${repo}] ${ms.title}`,
          status,
          progress,
          eta: ms.due_on ? new Date(ms.due_on).toLocaleDateString() : undefined,
          category: categoryForRepo(repo),
        } satisfies Activity;
      });
    } catch {
      return [];
    }
  });

  // 2. Fetch open PRs from all repos
  const prPromises = GITHUB_REPOS.map(async (repo) => {
    try {
      const res = await fetch(
        `${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/pulls?state=open&per_page=5&sort=updated&direction=desc`,
        { headers },
      );
      updateRateLimit(res);
      if (!res.ok) return [];
      const data: Array<{
        id: number;
        number: number;
        title: string;
        draft: boolean;
        requested_reviewers: unknown[];
        created_at: string;
      }> = await res.json();
      return data.map((pr) => {
        // Progress heuristic: draft=25%, awaiting review=50%, has reviewers=75%
        const progress = pr.draft ? 25 : (pr.requested_reviewers?.length > 0 ? 50 : 75);
        return {
          id: `pr-${repo}-${pr.number}`,
          name: `PR #${pr.number} [${repo}] ${pr.title}`,
          status: 'active' as const,
          progress,
          eta: timeAgo(pr.created_at),
          category: categoryForRepo(repo),
        } satisfies Activity;
      });
    } catch {
      return [];
    }
  });

  // 3. Fetch recently closed/merged PRs from all repos
  const closedPrPromises = GITHUB_REPOS.map(async (repo) => {
    try {
      const res = await fetch(
        `${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/pulls?state=closed&per_page=3&sort=updated&direction=desc`,
        { headers },
      );
      updateRateLimit(res);
      if (!res.ok) return [];
      const data: Array<{
        id: number;
        number: number;
        title: string;
        merged_at: string | null;
        closed_at: string | null;
      }> = await res.json();
      // Only include PRs closed within the last 7 days
      const cutoff = Date.now() - 7 * 24 * 3600 * 1000;
      return data
        .filter((pr) => {
          const closedTime = pr.merged_at ?? pr.closed_at;
          return closedTime && new Date(closedTime).getTime() > cutoff;
        })
        .map((pr) => ({
          id: `pr-closed-${repo}-${pr.number}`,
          name: `PR #${pr.number} [${repo}] ${pr.title}`,
          status: 'completed' as const,
          progress: 100,
          eta: pr.merged_at ? `merged ${timeAgo(pr.merged_at)}` : `closed ${timeAgo(pr.closed_at!)}`,
          category: categoryForRepo(repo),
        } satisfies Activity));
    } catch {
      return [];
    }
  });

  // Await all in parallel
  const [milestoneResults, prResults, closedPrResults] = await Promise.all([
    Promise.all(milestonePromises),
    Promise.all(prPromises),
    Promise.all(closedPrPromises),
  ]);

  // Flatten and combine: open PRs first, then milestones, then recent closed
  activities.push(
    ...prResults.flat(),
    ...milestoneResults.flat(),
    ...closedPrResults.flat(),
  );

  return activities;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const STATUS_COLOR: Record<Activity['status'], string> = {
  active: '#33CC66',
  pending: '#888888',
  completed: '#33CC66',
  blocked: '#FF4444',
};

const CATEGORY_ICON: Record<Activity['category'], string> = {
  governance: 'G',
  research: 'R',
  build: 'B',
  test: 'T',
  deploy: 'D',
};

const REPO_COLOR: Record<string, string> = {
  ga: '#FFB300',
  Demerzel: '#FFD700',
  hari: '#c4b5fd',
  tars: '#4FC3F7',
  ix: '#73d13d',
  'demerzel-bot': '#ff85c0',
  'guitar-singularity': '#ff7a45',
};

// ---------------------------------------------------------------------------
// Accordion Section
// ---------------------------------------------------------------------------
const AccordionSection: React.FC<{
  title: string;
  badge?: string;
  defaultOpen?: boolean;
  children: React.ReactNode;
}> = ({ title, badge, defaultOpen = false, children }) => {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <div className="prime-radiant__accordion-section">
      <div
        className="prime-radiant__accordion-header"
        onClick={() => setOpen(!open)}
      >
        <span className="prime-radiant__accordion-title">
          {title}
          {badge && <span className="prime-radiant__accordion-badge">{badge}</span>}
        </span>
        <span className="prime-radiant__accordion-arrow">{open ? '▼' : '▶'}</span>
      </div>
      {open && <div className="prime-radiant__accordion-body">{children}</div>}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const ActivityPanel: React.FC = () => {
  const [activities, setActivities] = useState<Activity[]>([]);
  const [commits, setCommits] = useState<CommitInfo[]>([]);
  const [issues, setIssues] = useState<IssueInfo[]>([]);
  const [panelCollapsed, setPanelCollapsed] = useState(false);
  const [rateLimited, setRateLimited] = useState(false);

  useEffect(() => {
    function refreshAll(): void {
      fetchActivities().then(setActivities);
      fetchCommits().then(setCommits);
      fetchIssues().then(setIssues);
      // Update rate-limit UI state after fetches settle
      setTimeout(() => setRateLimited(isRateLimited()), 2000);
    }

    // Initial fetch
    refreshAll();

    // Adaptive refresh: slow down when rate-limited
    let intervalId: ReturnType<typeof setInterval>;
    function scheduleRefresh(): void {
      const ms = isRateLimited() ? THROTTLED_REFRESH_MS : DEFAULT_REFRESH_MS;
      intervalId = setInterval(() => {
        refreshAll();
        // Re-schedule if rate-limit state changed
        const newMs = isRateLimited() ? THROTTLED_REFRESH_MS : DEFAULT_REFRESH_MS;
        if (newMs !== ms) {
          clearInterval(intervalId);
          scheduleRefresh();
        }
      }, ms);
    }
    scheduleRefresh();

    return () => clearInterval(intervalId);
  }, []);

  const active = activities.filter((a) => a.status === 'active');
  const _pending = activities.filter((a) => a.status === 'pending' || a.status === 'blocked');

  return (
    <div className="prime-radiant__activity">
      <div
        className="prime-radiant__activity-header"
        onClick={() => setPanelCollapsed(!panelCollapsed)}
      >
        <span className="prime-radiant__activity-title">
          Command
          <span className="prime-radiant__activity-count">
            {active.length} active · {commits.length} commits
          </span>
          {rateLimited && (
            <span style={{ color: '#FF4444', fontSize: '0.75em', marginLeft: 8 }}>
              Rate limited — refreshing every 5m
            </span>
          )}
        </span>
        <span className="prime-radiant__activity-toggle">
          {panelCollapsed ? '▶' : '▼'}
        </span>
      </div>

      {!panelCollapsed && (
        <>
          {/* Activities accordion */}
          <AccordionSection
            title="Activities"
            badge={`${active.length}`}
            defaultOpen={true}
          >
            {activities.map((a) => (
              <div key={a.id} className="prime-radiant__activity-item">
                <span
                  className={`prime-radiant__activity-cat${a.status === 'active' ? ' prime-radiant__activity-cat--active' : ''}`}
                  style={{ color: STATUS_COLOR[a.status] }}
                >
                  {CATEGORY_ICON[a.category]}
                </span>
                <div className="prime-radiant__activity-info">
                  <div className="prime-radiant__activity-name" style={{
                    color: a.status === 'completed' ? '#6b7280' : '#c9d1d9',
                    textDecoration: a.status === 'completed' ? 'line-through' : 'none',
                  }}>
                    {a.name}
                  </div>
                  {a.status !== 'completed' && (
                    <div className="prime-radiant__activity-bar">
                      <div
                        className="prime-radiant__activity-fill"
                        style={{
                          width: `${a.progress}%`,
                          backgroundColor: STATUS_COLOR[a.status],
                        }}
                      />
                    </div>
                  )}
                </div>
                <span className="prime-radiant__activity-eta" style={{
                  color: a.status === 'blocked' ? '#FF4444' : '#6b7280',
                }}>
                  {a.eta}
                </span>
              </div>
            ))}
          </AccordionSection>

          {/* Commits accordion */}
          <AccordionSection
            title="Commits"
            badge={`${commits.length}`}
            defaultOpen={false}
          >
            {commits.map((c) => (
              <div
                key={c.hash}
                className="prime-radiant__commit-item"
                title={`${c.repo}/${c.hash} — ${c.message}`}
                style={{ cursor: 'pointer' }}
                onClick={() => window.open(`https://github.com/${GITHUB_OWNER}/${c.repo}/commit/${c.hash}`, '_blank')}
              >
                <span className="prime-radiant__repo-badge" style={{
                  color: REPO_COLOR[c.repo] ?? '#8b949e',
                  borderColor: `${REPO_COLOR[c.repo] ?? '#8b949e'}44`,
                }}>
                  {c.repo}
                </span>
                <span className="prime-radiant__commit-hash" style={{
                  color: REPO_COLOR[c.repo] ?? '#8b949e',
                }}>
                  {c.hash.slice(0, 7)}
                </span>
                <span className="prime-radiant__commit-msg">{c.message}</span>
                <span className="prime-radiant__commit-time">{c.time}</span>
              </div>
            ))}
          </AccordionSection>

          {/* Issues accordion */}
          <AccordionSection
            title="Issues"
            badge={`${issues.length}`}
            defaultOpen={false}
          >
            {issues.map((issue) => (
              <div
                key={`${issue.repo}-${issue.number}`}
                className="prime-radiant__commit-item"
                style={{ cursor: 'pointer' }}
                onClick={() => window.open(issue.url, '_blank')}
                title={`${issue.repo}#${issue.number} — ${issue.title}`}
              >
                <span className="prime-radiant__repo-badge" style={{
                  color: REPO_COLOR[issue.repo] ?? '#8b949e',
                  borderColor: `${REPO_COLOR[issue.repo] ?? '#8b949e'}44`,
                }}>
                  {issue.repo}
                </span>
                <span className="prime-radiant__commit-hash" style={{
                  color: REPO_COLOR[issue.repo] ?? '#33CC66',
                }}>
                  #{issue.number}
                </span>
                <span className="prime-radiant__commit-msg">{issue.title}</span>
              </div>
            ))}
          </AccordionSection>
        </>
      )}
    </div>
  );
};
