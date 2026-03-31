// src/components/PrimeRadiant/LLMUsageTracker.ts
// Session-scoped LLM token usage tracker with cost estimation.
// Uses useSyncExternalStore for React integration (same pattern as DashboardSignalBus.ts).
// No regex — all header parsing uses indexOf/substring.

import { useSyncExternalStore } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ModelUsage {
  model: string;
  inputTokens: number;
  outputTokens: number;
  requestCount: number;
  estimatedCostUsd: number;
  remainingTokens: number | null;
  limitTokens: number | null;
}

export interface SessionUsage {
  models: Map<string, ModelUsage>;
  sessionStart: number;
  totalCostUsd: number;
}

// ---------------------------------------------------------------------------
// Cost model per 1M tokens
// ---------------------------------------------------------------------------

interface CostRate {
  inputPer1M: number;
  outputPer1M: number;
}

const COST_RATES: Record<string, CostRate> = {
  'claude-opus': { inputPer1M: 15, outputPer1M: 75 },
  'claude-sonnet': { inputPer1M: 3, outputPer1M: 15 },
  'gpt-4o': { inputPer1M: 2.5, outputPer1M: 10 },
  'codestral': { inputPer1M: 0.3, outputPer1M: 0.9 },
};

function getCostRate(model: string): CostRate {
  // Match by checking if the model string contains any known key
  const lowerModel = model.toLowerCase();
  for (const key of Object.keys(COST_RATES)) {
    if (lowerModel.indexOf(key) !== -1) {
      return COST_RATES[key];
    }
  }
  // Default fallback — use sonnet pricing as a reasonable middle ground
  return COST_RATES['claude-sonnet'];
}

function computeCost(inputTokens: number, outputTokens: number, rate: CostRate): number {
  return (inputTokens / 1_000_000) * rate.inputPer1M
       + (outputTokens / 1_000_000) * rate.outputPer1M;
}

// ---------------------------------------------------------------------------
// Header extraction (no regex — uses indexOf/substring)
// ---------------------------------------------------------------------------

function extractHeaderNumber(headers: Headers, name: string): number | null {
  const value = headers.get(name);
  if (value === null || value === undefined) return null;
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  const num = Number(trimmed);
  if (Number.isNaN(num)) return null;
  return num;
}

interface RateLimitInfo {
  remainingTokens: number | null;
  limitTokens: number | null;
}

function extractRateLimits(headers: Headers): RateLimitInfo {
  // Anthropic headers
  const remaining = extractHeaderNumber(headers, 'x-ratelimit-remaining-tokens');
  const limit = extractHeaderNumber(headers, 'x-ratelimit-limit-tokens');

  return {
    remainingTokens: remaining,
    limitTokens: limit,
  };
}

// ---------------------------------------------------------------------------
// Store singleton
// ---------------------------------------------------------------------------

type StoreListener = () => void;

class UsageStore {
  private models = new Map<string, ModelUsage>();
  private sessionStart = Date.now();
  private listeners = new Set<StoreListener>();
  private snapshotVersion = 0;
  private cachedSnapshot: SessionUsage | null = null;

  /** Record usage from a completed LLM request. */
  recordUsage(
    model: string,
    inputTokens: number,
    outputTokens: number,
    headers?: Headers,
  ): void {
    const existing = this.models.get(model);
    const rate = getCostRate(model);
    const rateLimits = headers ? extractRateLimits(headers) : { remainingTokens: null, limitTokens: null };

    if (existing) {
      existing.inputTokens += inputTokens;
      existing.outputTokens += outputTokens;
      existing.requestCount += 1;
      existing.estimatedCostUsd = computeCost(existing.inputTokens, existing.outputTokens, rate);
      // Update rate limits — latest values win
      if (rateLimits.remainingTokens !== null) {
        existing.remainingTokens = rateLimits.remainingTokens;
      }
      if (rateLimits.limitTokens !== null) {
        existing.limitTokens = rateLimits.limitTokens;
      }
    } else {
      const cost = computeCost(inputTokens, outputTokens, rate);
      this.models.set(model, {
        model,
        inputTokens,
        outputTokens,
        requestCount: 1,
        estimatedCostUsd: cost,
        remainingTokens: rateLimits.remainingTokens,
        limitTokens: rateLimits.limitTokens,
      });
    }

    this.snapshotVersion++;
    this.cachedSnapshot = null;
    this.notify();
  }

  /** Get the current session usage snapshot. */
  getSessionUsage(): SessionUsage {
    if (this.cachedSnapshot) return this.cachedSnapshot;

    let totalCostUsd = 0;
    for (const usage of this.models.values()) {
      totalCostUsd += usage.estimatedCostUsd;
    }

    this.cachedSnapshot = {
      models: new Map(this.models),
      sessionStart: this.sessionStart,
      totalCostUsd,
    };
    return this.cachedSnapshot;
  }

  /** Get quota percent remaining for a model (0-100). Returns -1 if no limit data. */
  getQuotaPercent(model: string): number {
    const usage = this.models.get(model);
    if (!usage || usage.limitTokens === null || usage.limitTokens === 0) return -1;
    if (usage.remainingTokens === null) return -1;
    return Math.round((usage.remainingTokens / usage.limitTokens) * 100);
  }

  /** Reset session data. */
  reset(): void {
    this.models.clear();
    this.sessionStart = Date.now();
    this.snapshotVersion++;
    this.cachedSnapshot = null;
    this.notify();
  }

  subscribe(listener: StoreListener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }
}

// ---------------------------------------------------------------------------
// Singleton instance
// ---------------------------------------------------------------------------

const usageStore = new UsageStore();

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/** Record usage from a completed LLM request. */
export function recordUsage(
  model: string,
  inputTokens: number,
  outputTokens: number,
  headers?: Headers,
): void {
  usageStore.recordUsage(model, inputTokens, outputTokens, headers);
}

/** Get the current session usage snapshot. */
export function getSessionUsage(): SessionUsage {
  return usageStore.getSessionUsage();
}

/** Get quota percent remaining for a model (0-100). Returns -1 if no limit data. */
export function getQuotaPercent(model: string): number {
  return usageStore.getQuotaPercent(model);
}

/** Reset session tracking data. */
export function resetSessionUsage(): void {
  usageStore.reset();
}

// ---------------------------------------------------------------------------
// React hook — useSyncExternalStore
// ---------------------------------------------------------------------------

/** React hook that subscribes to session usage updates. */
export function useSessionUsage(): SessionUsage {
  return useSyncExternalStore(
    (cb) => usageStore.subscribe(cb),
    () => usageStore.getSessionUsage(),
  );
}
