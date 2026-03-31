// src/components/PrimeRadiant/HexavalentTemporal.ts
// Hexavalent Temporal Semantics — Phase B
// T/F are stable. P/D decay toward U. C triggers escalation.
// Adaptive refresh intervals based on truth-value volatility.
// Zero-allocation decay tracking via Float64Array.

import { signalBus } from './DashboardSignalBus';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type HexavalentValue = 'T' | 'P' | 'U' | 'D' | 'F' | 'C';

/** Transition adjacency — allowed single-step moves */
const ADJACENCY: Record<HexavalentValue, Set<HexavalentValue>> = {
  T: new Set(['P', 'C']),
  P: new Set(['T', 'U', 'C']),
  U: new Set(['P', 'D', 'C']),
  D: new Set(['U', 'F', 'C']),
  F: new Set(['D', 'C']),
  C: new Set(['T', 'P', 'U', 'D', 'F']), // Resolution from C can go anywhere
};

export function isValidTransition(from: HexavalentValue, to: HexavalentValue): boolean {
  if (from === to) return true;
  return ADJACENCY[from]?.has(to) ?? false;
}

export function getAllowedTransitions(from: HexavalentValue): HexavalentValue[] {
  const allowed = ADJACENCY[from];
  return allowed ? Array.from(allowed) : [];
}

/** Transition event for the audit trail */
export interface HexavalentTransitionEvent {
  field: string;
  panelId: string;
  fromValue: HexavalentValue;
  toValue: HexavalentValue;
  timestamp: number;
  actor: 'user' | 'decay' | 'fetch' | 'resolution';
  evidence?: string;
  article?: number;
}

export function publishTransition(event: HexavalentTransitionEvent): void {
  signalBus.publish('belief:transition', event, event.panelId);
}

// ---------------------------------------------------------------------------
// Decay Tracker — zero-allocation, 1Hz tick
// ---------------------------------------------------------------------------

export interface DecayConfig {
  halfLifeMs: number;     // default 120000 (120s)
  threshold: number;      // default 0.3 — below this, P→U or D→U
}

const DEFAULT_CONFIG: DecayConfig = {
  halfLifeMs: 120000,
  threshold: 0.3,
};

/**
 * Tracks decay of P and D hexavalent values.
 * Uses pre-allocated typed arrays — no GC pressure in tick loop.
 * Single setInterval at 1Hz.
 */
export class DecayTracker {
  private timestamps: Float64Array;    // last reinforcement time per slot
  private values: Uint8Array;          // 0=none, 1=P, 2=D
  private fieldIds: string[];          // field identifier per slot
  private size: number;
  private tickId: number | null = null;
  private config: DecayConfig;
  private panelId: string;
  private lambda: number;              // decay constant: ln(2) / halfLife
  private onDecay: ((batch: DecayEvent[]) => void) | null = null;

  constructor(panelId: string, maxCells: number = 200, config?: Partial<DecayConfig>) {
    this.panelId = panelId;
    this.config = { ...DEFAULT_CONFIG, ...config };
    this.lambda = 0.693147 / this.config.halfLifeMs; // ln(2) / halfLife
    this.timestamps = new Float64Array(maxCells);
    this.values = new Uint8Array(maxCells);
    this.fieldIds = new Array(maxCells).fill('');
    this.size = 0;
  }

  /** Start the 1Hz decay tick */
  start(onDecay?: (batch: DecayEvent[]) => void): void {
    this.onDecay = onDecay ?? null;
    if (this.tickId === null) {
      this.tickId = window.setInterval(() => this.tick(), 1000);
    }
  }

  /** Stop tracking */
  stop(): void {
    if (this.tickId !== null) {
      window.clearInterval(this.tickId);
      this.tickId = null;
    }
  }

  /** Register or reinforce a P/D value. Returns the slot index. */
  track(fieldId: string, value: 'P' | 'D'): number {
    // Find existing slot for this field
    for (let i = 0; i < this.size; i++) {
      if (this.fieldIds[i] === fieldId) {
        this.timestamps[i] = Date.now();
        this.values[i] = value === 'P' ? 1 : 2;
        return i;
      }
    }
    // New slot
    if (this.size < this.timestamps.length) {
      const idx = this.size;
      this.fieldIds[idx] = fieldId;
      this.timestamps[idx] = Date.now();
      this.values[idx] = value === 'P' ? 1 : 2;
      this.size++;
      return idx;
    }
    return -1; // capacity full
  }

  /** Remove tracking for a field (value changed to T/F/U/C) */
  untrack(fieldId: string): void {
    for (let i = 0; i < this.size; i++) {
      if (this.fieldIds[i] === fieldId) {
        this.values[i] = 0;
        return;
      }
    }
  }

  /** Reinforce a field (data fetch confirmed same value) */
  reinforce(fieldId: string): void {
    for (let i = 0; i < this.size; i++) {
      if (this.fieldIds[i] === fieldId && this.values[i] !== 0) {
        this.timestamps[i] = Date.now();
        return;
      }
    }
  }

  /** Get elapsed ms since last reinforcement for a field */
  getElapsed(fieldId: string): number {
    for (let i = 0; i < this.size; i++) {
      if (this.fieldIds[i] === fieldId && this.values[i] !== 0) {
        return Date.now() - this.timestamps[i];
      }
    }
    return 0;
  }

  /** Get remaining strength [0-1] for a field */
  getStrength(fieldId: string): number {
    const elapsed = this.getElapsed(fieldId);
    if (elapsed === 0) return 1.0;
    return Math.exp(-this.lambda * elapsed);
  }

  /** The tick — check all tracked values for decay */
  private tick(): void {
    const now = Date.now();
    const batch: DecayEvent[] = [];

    for (let i = 0; i < this.size; i++) {
      if (this.values[i] === 0) continue;
      const elapsed = now - this.timestamps[i];
      const strength = Math.exp(-this.lambda * elapsed);

      if (strength < this.config.threshold) {
        const oldValue: HexavalentValue = this.values[i] === 1 ? 'P' : 'D';
        batch.push({
          fieldId: this.fieldIds[i],
          oldValue,
          newValue: 'U',
          decayDuration: elapsed,
        });
        this.values[i] = 0; // Stop tracking — now U
      }
    }

    if (batch.length > 0) {
      // Publish batch decay signal
      signalBus.publish('belief:decayed:batch', batch, this.panelId);
      if (this.onDecay) this.onDecay(batch);
    }
  }

  dispose(): void {
    this.stop();
  }
}

export interface DecayEvent {
  fieldId: string;
  oldValue: HexavalentValue;
  newValue: 'U';
  decayDuration: number;
}

// ---------------------------------------------------------------------------
// Adaptive Refresh Interval
// ---------------------------------------------------------------------------

/** Urgency weights per truth value */
const URGENCY_WEIGHT: Record<string, number> = {
  T: 0.0,
  P: 0.5,
  U: 0.3,
  D: 0.5,
  F: 0.0,
  C: 1.0,
};

/**
 * Compute adaptive refresh interval from truth-value distribution.
 * Returns interval in ms. Clamps to 2000ms if any C values present.
 */
export function adaptiveRefreshInterval(
  hexDistribution: Record<string, number>,
  baseIntervalMs: number,
  minIntervalMs: number = 2000,
): number {
  let totalWeight = 0;
  let totalCells = 0;

  for (const [value, count] of Object.entries(hexDistribution)) {
    const upper = value.toUpperCase();
    const weight = URGENCY_WEIGHT[upper] ?? 0;
    totalWeight += weight * count;
    totalCells += count;
  }

  // C override — any contradictory values → minimum interval
  if ((hexDistribution['C'] ?? 0) > 0) {
    return minIntervalMs;
  }

  if (totalCells === 0) return baseIntervalMs;

  const volatility = totalWeight / totalCells; // [0, 1]
  const factor = (1 - volatility) * (1 - volatility); // quadratic
  return Math.max(minIntervalMs, Math.round(baseIntervalMs * factor + minIntervalMs));
}

/**
 * CSS animation-delay value for a decaying cell.
 * Negative delay starts the animation partway through, matching elapsed time.
 * Returns a CSS string like "-90s".
 */
export function decayAnimationDelay(elapsedMs: number, halfLifeMs: number = 120000): string {
  // The CSS animation duration equals 2x half-life (covers full decay to threshold)
  const durationMs = halfLifeMs * 2;
  const delayMs = Math.min(elapsedMs, durationMs);
  return `-${(delayMs / 1000).toFixed(1)}s`;
}

/**
 * CSS animation duration for decay animations.
 * Returns a CSS string like "240s".
 */
export function decayAnimationDuration(halfLifeMs: number = 120000): string {
  return `${(halfLifeMs * 2 / 1000).toFixed(0)}s`;
}
