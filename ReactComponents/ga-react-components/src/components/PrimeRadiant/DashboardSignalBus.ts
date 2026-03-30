// src/components/PrimeRadiant/DashboardSignalBus.ts
// Cross-widget signal bus for IXQL PUBLISH/SUBSCRIBE.
// Panels publish selection events; subscribers receive them reactively.
// Throttled to prevent broadcast storms from rapid interactions.

import { useSyncExternalStore, useCallback, useRef } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface DashboardSignal {
  name: string;           // signal name (e.g. "selectedBelief")
  value: unknown;         // payload (row data, id, etc.)
  sourcePanel: string;    // panel id that published
  timestamp: number;
}

type SignalListener = () => void;

// ---------------------------------------------------------------------------
// Signal bus singleton
// ---------------------------------------------------------------------------

class SignalBusStore {
  private signals = new Map<string, DashboardSignal>();
  private listeners = new Set<SignalListener>();
  private snapshot = new Map<string, DashboardSignal>();
  private dirty = false;
  private throttleTimers = new Map<string, ReturnType<typeof setTimeout>>();

  /** Publish a signal value. Throttled: max 1 update per signal per 50ms. */
  publish(name: string, value: unknown, sourcePanel: string): void {
    // Clear any pending throttle for this signal
    const existing = this.throttleTimers.get(name);
    if (existing) clearTimeout(existing);

    this.throttleTimers.set(name, setTimeout(() => {
      this.throttleTimers.delete(name);
      this.signals.set(name, {
        name,
        value,
        sourcePanel,
        timestamp: Date.now(),
      });
      this.dirty = true;
      this.notify();
    }, 50));
  }

  /** Get the current value of a signal. */
  get(name: string): DashboardSignal | undefined {
    return this.signals.get(name);
  }

  /** Get all current signals as a snapshot. */
  getAll(): Map<string, DashboardSignal> {
    if (this.dirty) {
      this.snapshot = new Map(this.signals);
      this.dirty = false;
    }
    return this.snapshot;
  }

  /** Clear a specific signal. */
  clear(name: string): void {
    if (this.signals.delete(name)) {
      this.dirty = true;
      this.notify();
    }
  }

  /** Clear all signals. */
  clearAll(): void {
    this.signals.clear();
    this.dirty = true;
    this.notify();
  }

  subscribe(listener: SignalListener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }
}

export const signalBus = new SignalBusStore();

// ---------------------------------------------------------------------------
// React hooks
// ---------------------------------------------------------------------------

/** Subscribe to a specific signal by name. Returns the signal or undefined. */
export function useSignal(name: string): DashboardSignal | undefined {
  const allSignals = useSyncExternalStore(
    (cb) => signalBus.subscribe(cb),
    () => signalBus.getAll(),
  );
  return allSignals.get(name);
}

/** Subscribe to multiple signals. Returns a stable map that only changes when relevant signals change. */
export function useSignals(names: string[]): Map<string, DashboardSignal> {
  const allSignals = useSyncExternalStore(
    (cb) => signalBus.subscribe(cb),
    () => signalBus.getAll(),
  );
  const prevRef = useRef<Map<string, DashboardSignal>>(new Map());

  // Check if any relevant signal actually changed (by timestamp)
  let changed = false;
  for (const name of names) {
    const current = allSignals.get(name);
    const prev = prevRef.current.get(name);
    if (current?.timestamp !== prev?.timestamp) { changed = true; break; }
  }
  // Also check if a previously-present signal was removed
  if (!changed) {
    for (const name of prevRef.current.keys()) {
      if (names.indexOf(name) >= 0 && !allSignals.has(name)) { changed = true; break; }
    }
  }

  if (changed) {
    const result = new Map<string, DashboardSignal>();
    for (const name of names) {
      const sig = allSignals.get(name);
      if (sig) result.set(name, sig);
    }
    prevRef.current = result;
  }

  return prevRef.current;
}

/** Publish a signal from a panel. Returns a stable callback. */
export function usePublish(panelId: string) {
  return useCallback(
    (signalName: string, value: unknown) => {
      signalBus.publish(signalName, value, panelId);
    },
    [panelId],
  );
}
