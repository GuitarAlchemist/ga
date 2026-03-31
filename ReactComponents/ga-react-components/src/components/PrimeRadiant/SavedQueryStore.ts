// src/components/PrimeRadiant/SavedQueryStore.ts
// Persists named IXQL queries as governance artifacts in localStorage.
// Version-stamped with rationale for audit trail.

import { useSyncExternalStore } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface SavedQuery {
  id: string;
  command: string;           // the original IXQL command text
  asArtifact: boolean;       // whether saved AS artifact
  rationale: string | null;
  version: number;
  savedAt: number;           // epoch ms
  grammarVersion?: string;   // grammar version at save time (Living Grammar)
  usesExperimentalKeywords?: string[];  // trial keywords used in this query
}

type Listener = () => void;

// ---------------------------------------------------------------------------
// Store
// ---------------------------------------------------------------------------

const STORAGE_KEY = 'prime-radiant-saved-queries';

class SavedQueryStoreImpl {
  private queries = new Map<string, SavedQuery>();
  private listeners = new Set<Listener>();
  private snapshot: SavedQuery[] = [];
  private dirty = true;

  constructor() {
    this.loadFromStorage();
  }

  /** Save a query. Increments version if id already exists. */
  save(id: string, command: string, asArtifact: boolean, rationale: string | null): void {
    const existing = this.queries.get(id);
    const version = existing ? existing.version + 1 : 1;

    this.queries.set(id, {
      id,
      command,
      asArtifact,
      rationale,
      version,
      savedAt: Date.now(),
    });

    this.dirty = true;
    this.persistToStorage();
    this.notify();
  }

  /** Get a saved query by id. */
  get(id: string): SavedQuery | undefined {
    return this.queries.get(id);
  }

  /** Get all saved queries as a stable snapshot, sorted by savedAt desc. */
  getAll(): SavedQuery[] {
    if (this.dirty) {
      this.snapshot = [...this.queries.values()].sort((a, b) => b.savedAt - a.savedAt);
      this.dirty = false;
    }
    return this.snapshot;
  }

  /** Delete a saved query. */
  remove(id: string): void {
    if (this.queries.delete(id)) {
      this.dirty = true;
      this.persistToStorage();
      this.notify();
    }
  }

  /** Clear all saved queries. */
  clear(): void {
    this.queries.clear();
    this.dirty = true;
    this.persistToStorage();
    this.notify();
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  // ── Persistence ──

  private loadFromStorage(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const arr = JSON.parse(raw) as SavedQuery[];
      if (!Array.isArray(arr)) return;
      for (const q of arr) {
        if (q && typeof q.id === 'string') {
          this.queries.set(q.id, q);
        }
      }
      this.dirty = true;
    } catch {
      // Corrupted storage — start fresh
    }
  }

  private persistToStorage(): void {
    try {
      const arr = [...this.queries.values()];
      localStorage.setItem(STORAGE_KEY, JSON.stringify(arr));
    } catch {
      // Storage full or unavailable — silently degrade
    }
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }
}

export const savedQueryStore = new SavedQueryStoreImpl();

// ---------------------------------------------------------------------------
// React hooks
// ---------------------------------------------------------------------------

/** Subscribe to saved queries. Returns sorted list. */
export function useSavedQueries(): SavedQuery[] {
  return useSyncExternalStore(
    (cb) => savedQueryStore.subscribe(cb),
    () => savedQueryStore.getAll(),
  );
}
