// IXQL Grammar Telemetry — tracks variant invocation counts
// Batched in-memory with periodic localStorage flush to avoid
// synchronous I/O in hot paths (e.g., violation monitor loops).

export type IxqlVariantStats = {
  invoked: number;
  succeeded: number;
  failed: number;
  lastUsed: string | null;
};

export type IxqlTelemetryData = Record<string, IxqlVariantStats>;

const STORAGE_KEY = 'ixql-telemetry';
const FLUSH_INTERVAL_MS = 5000; // flush to localStorage every 5s

// In-memory cache — avoids sync localStorage reads in hot paths
let cache: IxqlTelemetryData | null = null;
let dirty = false;
let flushTimer: ReturnType<typeof setTimeout> | null = null;

function ensureCache(): IxqlTelemetryData {
  if (cache === null) {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      cache = raw ? (JSON.parse(raw) as IxqlTelemetryData) : {};
    } catch {
      cache = {};
    }
  }
  return cache;
}

function scheduleFlush(): void {
  if (flushTimer !== null) return; // already scheduled
  flushTimer = setTimeout(() => {
    flushTimer = null;
    if (dirty && cache) {
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(cache));
      } catch { /* quota exceeded — silently drop */ }
      dirty = false;
    }
  }, FLUSH_INTERVAL_MS);
}

export function recordInvocation(variant: string, success: boolean): void {
  const data = ensureCache();
  const entry = data[variant] ?? { invoked: 0, succeeded: 0, failed: 0, lastUsed: null };
  entry.invoked++;
  if (success) entry.succeeded++;
  else entry.failed++;
  entry.lastUsed = new Date().toISOString();
  data[variant] = entry;
  dirty = true;
  scheduleFlush();
}

export function getTelemetry(): IxqlTelemetryData {
  return ensureCache();
}

export function resetTelemetry(): void {
  cache = {};
  dirty = false;
  localStorage.removeItem(STORAGE_KEY);
}
