// src/components/PrimeRadiant/ConnectionLog.ts
// Persistent admin connection log with log rotation and auto-scavenge.
// Tracks connect/disconnect events in localStorage, survives page reloads.

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ConnectionLogEntry {
  id: string;
  event: 'connect' | 'disconnect';
  connectionId: string;
  browser: string;
  color: string;
  timestamp: string;   // ISO 8601
  isSelf: boolean;
}

export interface ConnectionLogConfig {
  /** Max entries to retain (oldest trimmed first). Default: 200 */
  maxEntries?: number;
  /** Max age in ms before auto-scavenge removes entry. Default: 24h */
  maxAgeMs?: number;
  /** localStorage key. Default: 'prime-radiant-connection-log' */
  storageKey?: string;
}

// ---------------------------------------------------------------------------
// Defaults
// ---------------------------------------------------------------------------

const DEFAULT_MAX_ENTRIES = 200;
const DEFAULT_MAX_AGE_MS = 24 * 60 * 60 * 1000; // 24 hours
const DEFAULT_STORAGE_KEY = 'prime-radiant-connection-log';

// ---------------------------------------------------------------------------
// ConnectionLog — singleton per storageKey
// ---------------------------------------------------------------------------

export class ConnectionLog {
  private maxEntries: number;
  private maxAgeMs: number;
  private storageKey: string;
  private entries: ConnectionLogEntry[];
  private listeners = new Set<() => void>();
  private scavengeTimer: ReturnType<typeof setInterval> | null = null;

  constructor(config: ConnectionLogConfig = {}) {
    this.maxEntries = config.maxEntries ?? DEFAULT_MAX_ENTRIES;
    this.maxAgeMs = config.maxAgeMs ?? DEFAULT_MAX_AGE_MS;
    this.storageKey = config.storageKey ?? DEFAULT_STORAGE_KEY;
    this.entries = this.load();
    // Run initial scavenge then schedule periodic cleanup
    this.scavenge();
    this.scavengeTimer = setInterval(() => this.scavenge(), 60_000); // every minute
  }

  // ── Public API ──

  /** Log a connection event */
  logConnect(connectionId: string, browser: string, color: string, isSelf: boolean): void {
    this.push({
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
      event: 'connect',
      connectionId,
      browser,
      color,
      timestamp: new Date().toISOString(),
      isSelf,
    });
  }

  /** Log a disconnection event */
  logDisconnect(connectionId: string, browser: string, color: string, isSelf: boolean): void {
    this.push({
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
      event: 'disconnect',
      connectionId,
      browser,
      color,
      timestamp: new Date().toISOString(),
      isSelf,
    });
  }

  /** Get all entries (newest first) */
  getEntries(): readonly ConnectionLogEntry[] {
    return this.entries;
  }

  /** Get entry count */
  get length(): number {
    return this.entries.length;
  }

  /** Clear all entries */
  clear(): void {
    this.entries = [];
    this.save();
    this.notify();
  }

  /** Subscribe to changes — returns unsubscribe function */
  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  /** Stop the auto-scavenge timer */
  dispose(): void {
    if (this.scavengeTimer) {
      clearInterval(this.scavengeTimer);
      this.scavengeTimer = null;
    }
    this.listeners.clear();
  }

  // ── Internal ──

  private push(entry: ConnectionLogEntry): void {
    this.entries = [entry, ...this.entries];
    this.rotate();
    this.save();
    this.notify();
  }

  /** Trim to maxEntries (rotation) */
  private rotate(): void {
    if (this.entries.length > this.maxEntries) {
      this.entries = this.entries.slice(0, this.maxEntries);
    }
  }

  /** Remove entries older than maxAgeMs (scavenge) */
  private scavenge(): void {
    const cutoff = Date.now() - this.maxAgeMs;
    const before = this.entries.length;
    this.entries = this.entries.filter(e => new Date(e.timestamp).getTime() >= cutoff);
    if (this.entries.length !== before) {
      this.save();
      this.notify();
    }
  }

  private save(): void {
    try {
      localStorage.setItem(this.storageKey, JSON.stringify(this.entries));
    } catch {
      // Storage full — drop oldest half and retry
      this.entries = this.entries.slice(0, Math.floor(this.entries.length / 2));
      try {
        localStorage.setItem(this.storageKey, JSON.stringify(this.entries));
      } catch {
        // Give up silently
      }
    }
  }

  private load(): ConnectionLogEntry[] {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      // Basic shape validation
      return parsed.filter(
        (e: unknown): e is ConnectionLogEntry =>
          typeof e === 'object' && e !== null &&
          'event' in e && 'connectionId' in e && 'timestamp' in e,
      );
    } catch {
      return [];
    }
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }
}

// ---------------------------------------------------------------------------
// Singleton instance
// ---------------------------------------------------------------------------

let _instance: ConnectionLog | null = null;

export function getConnectionLog(config?: ConnectionLogConfig): ConnectionLog {
  if (!_instance) {
    _instance = new ConnectionLog(config);
  }
  return _instance;
}

// ---------------------------------------------------------------------------
// React hook
// ---------------------------------------------------------------------------

import { useEffect, useState } from 'react';

export function useConnectionLog(config?: ConnectionLogConfig): ConnectionLog {
  const [log] = useState(() => getConnectionLog(config));
  const [, forceUpdate] = useState(0);

  useEffect(() => {
    return log.subscribe(() => forceUpdate(n => n + 1));
  }, [log]);

  return log;
}
