// src/components/PrimeRadiant/SignalRGisBridge.ts
// Bridge between SignalR governance events (algedonic signals, belief changes)
// and live GIS pin updates on planets.

import type { GisLayerManager } from './GisLayer';
import type { AlgedonicSignalEvent, BeliefState } from './DataLoader';

// ---------------------------------------------------------------------------
// Pin ID prefixes for SignalR-generated pins
// ---------------------------------------------------------------------------
const ALGEDONIC_PIN_PREFIX = 'algedonic-';
const BELIEF_PIN_PREFIX = 'belief-';

// ---------------------------------------------------------------------------
// Deterministic hash — same algorithm as IxqlGisBridge for consistency
// ---------------------------------------------------------------------------

function hashStringToNumber(str: string): number {
  let hash = 2166136261;
  for (let i = 0; i < str.length; i++) {
    hash ^= str.charCodeAt(i);
    hash = (hash * 16777619) >>> 0;
  }
  return hash;
}

function idToLatLon(id: string): { lat: number; lon: number } {
  const h1 = hashStringToNumber(id);
  const h2 = hashStringToNumber(id + ':lon');
  const lat = (h1 / 0xFFFFFFFF) * 160 - 80;
  const lon = (h2 / 0xFFFFFFFF) * 360 - 180;
  return { lat, lon };
}

// ---------------------------------------------------------------------------
// Belief truth value → pin color mapping
// ---------------------------------------------------------------------------

function beliefColor(truthValue: string): string {
  switch (truthValue) {
    case 'C': return '#FF44FF'; // contradictory — magenta
    case 'F': return '#FF4444'; // false — red
    case 'U': return '#888888'; // unknown — gray
    case 'T': return '#33CC66'; // true — green
    default:  return '#4488FF'; // fallback — blue
  }
}

function beliefPulse(truthValue: string): boolean {
  return truthValue === 'C'; // only contradictory pulses
}

// ---------------------------------------------------------------------------
// Auto-remove timer tracking
// ---------------------------------------------------------------------------
type TimerMap = Map<string, ReturnType<typeof setTimeout>>;

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Start the SignalR → GIS bridge. Listens for algedonic signals and belief
 * changes via callbacks that should be wired to the existing SignalR polling
 * system in DataLoader.ts.
 *
 * Returns a cleanup function that stops all timers and removes listeners.
 *
 * @param gisManager - GIS layer manager for the target planet (typically Earth)
 * @param onEvent - Optional callback fired for every processed event (for logging/UI)
 * @returns Cleanup function + event handlers to wire into startLivePolling callbacks
 */
export function startSignalRGisBridge(
  gisManager: GisLayerManager,
  onEvent?: (event: string) => void,
): SignalRGisBridgeHandle {
  const autoRemoveTimers: TimerMap = new Map();
  let disposed = false;

  // --- Algedonic signal handler ---
  const handleAlgedonicSignal = (signal: AlgedonicSignalEvent): void => {
    if (disposed) return;

    const pinId = `${ALGEDONIC_PIN_PREFIX}${signal.id}`;
    const { lat, lon } = idToLatLon(signal.nodeId ?? signal.id);
    const isPain = signal.type === 'pain';

    gisManager.addPin({
      id: pinId,
      lat,
      lon,
      label: signal.signal,
      color: isPain ? '#FF4444' : '#33CC66',
      pulse: true,
      size: signal.severity === 'emergency' ? 2.0 : 1.2,
      category: `algedonic-${signal.type}`,
      data: {
        signalId: signal.id,
        type: signal.type,
        severity: signal.severity,
        source: signal.source,
      },
    });

    // Auto-remove after 30 seconds
    const existing = autoRemoveTimers.get(pinId);
    if (existing) clearTimeout(existing);

    autoRemoveTimers.set(
      pinId,
      setTimeout(() => {
        if (!disposed) {
          gisManager.removePin(pinId);
          autoRemoveTimers.delete(pinId);
        }
      }, 30_000),
    );

    onEvent?.(`algedonic_${signal.type}: ${signal.signal}`);
  };

  // --- Belief change handler ---
  const handleBeliefUpdate = (belief: BeliefState): void => {
    if (disposed) return;

    const pinId = `${BELIEF_PIN_PREFIX}${belief.id}`;
    const { lat, lon } = idToLatLon(belief.id);

    gisManager.addPin({
      id: pinId,
      lat,
      lon,
      label: belief.proposition.slice(0, 20),
      color: beliefColor(belief.truth_value),
      pulse: beliefPulse(belief.truth_value),
      size: 1.0,
      category: 'belief',
      data: {
        beliefId: belief.id,
        truthValue: belief.truth_value,
        confidence: belief.confidence,
      },
    });

    onEvent?.(`belief_changed: ${belief.id} → ${belief.truth_value}`);
  };

  // --- Cleanup ---
  const cleanup = (): void => {
    disposed = true;

    // Clear all auto-remove timers
    for (const timer of autoRemoveTimers.values()) {
      clearTimeout(timer);
    }
    autoRemoveTimers.clear();

    // Remove all SignalR-generated pins
    const existing = gisManager.getPins();
    for (const pin of existing) {
      if (pin.id.startsWith(ALGEDONIC_PIN_PREFIX) || pin.id.startsWith(BELIEF_PIN_PREFIX)) {
        gisManager.removePin(pin.id);
      }
    }
  };

  return {
    cleanup,
    handleAlgedonicSignal,
    handleBeliefUpdate,
  };
}

/** Handle returned by startSignalRGisBridge */
export interface SignalRGisBridgeHandle {
  /** Stop bridge and remove all SignalR-generated pins */
  cleanup: () => void;
  /** Wire this to LiveDataConfig.onAlgedonicSignal */
  handleAlgedonicSignal: (signal: AlgedonicSignalEvent) => void;
  /** Wire this to LiveDataConfig.onBeliefUpdate */
  handleBeliefUpdate: (belief: BeliefState) => void;
}
