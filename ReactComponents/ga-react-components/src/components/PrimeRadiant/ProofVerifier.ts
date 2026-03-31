// src/components/PrimeRadiant/ProofVerifier.ts
// Closes the governance self-verification loop.
// Subscribes to __renderProof__ signals, maintains a proof history ring buffer,
// detects divergence between consecutive proofs for the same panel,
// and publishes verification violations back through the signal bus.
//
// The loop: data → render → proof → ProofVerifier → violation → case law
// This makes the proof infrastructure self-correcting instead of write-only.

import { signalBus, type DashboardSignal } from './DashboardSignalBus';
import type { RenderProof, DivergenceSeverity } from './RenderProof';
import { classifyDivergences } from './RenderProof';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ProofHistoryEntry {
  proof: RenderProof;
  receivedAt: number;
}

export interface ProofDivergenceAlert {
  panelId: string;
  panelType: string;
  alertType: 'data-changed-render-stale' | 'render-changed-data-stale' | 'new-divergences' | 'checksum-drift';
  details: string;
  severity: DivergenceSeverity;
  previousChecksum: string | null;
  currentChecksum: string | null;
  timestamp: number;
}

// ---------------------------------------------------------------------------
// ProofVerifier
// ---------------------------------------------------------------------------

const HISTORY_SIZE = 10; // ring buffer size per panel
const STALE_THRESHOLD_MS = 60_000; // 1 minute without proof = stale panel

class ProofVerifierImpl {
  private history = new Map<string, ProofHistoryEntry[]>();
  private unsubscribe: (() => void) | null = null;
  private tickId: number | null = null;
  private alertCount = 0;

  /** Start listening to proof signals */
  start(): void {
    if (this.unsubscribe) return; // already running

    // Subscribe to all signal bus changes and filter for proof signals
    this.unsubscribe = signalBus.subscribe(() => {
      const all = signalBus.getAll();
      for (const [name, signal] of all) {
        if (name.indexOf('__renderProof__') === 0) {
          this.processProof(signal);
        }
      }
    });

    // Staleness check every 30 seconds
    this.tickId = window.setInterval(() => this.checkStaleness(), 30_000);
  }

  /** Stop listening */
  stop(): void {
    if (this.unsubscribe) {
      this.unsubscribe();
      this.unsubscribe = null;
    }
    if (this.tickId !== null) {
      window.clearInterval(this.tickId);
      this.tickId = null;
    }
  }

  /** Get proof history for a panel */
  getHistory(panelId: string): ProofHistoryEntry[] {
    return this.history.get(panelId) ?? [];
  }

  /** Get all tracked panel IDs */
  getTrackedPanels(): string[] {
    return Array.from(this.history.keys());
  }

  /** Get alert count since last reset */
  getAlertCount(): number {
    return this.alertCount;
  }

  /** Reset for test isolation */
  reset(): void {
    this.stop();
    this.history.clear();
    this.alertCount = 0;
  }

  // ── Internal ──

  private processProof(signal: DashboardSignal): void {
    const proof = signal.value as RenderProof;
    if (!proof || !proof.panelId) return;

    const panelId = proof.panelId;
    const now = Date.now();

    // Get or create history ring buffer
    let ring = this.history.get(panelId);
    if (!ring) {
      ring = [];
      this.history.set(panelId, ring);
    }

    const previous = ring.length > 0 ? ring[ring.length - 1] : null;

    // Add to ring buffer (cap at HISTORY_SIZE)
    ring.push({ proof, receivedAt: now });
    if (ring.length > HISTORY_SIZE) {
      ring.shift();
    }

    // Skip verification on first proof (no baseline)
    if (!previous) return;

    // --- Divergence detection ---

    // 1. New divergences that weren't in the previous proof
    if (proof.divergences.length > 0 && previous.proof.divergences.length === 0) {
      this.publishAlert({
        panelId,
        panelType: proof.panelType,
        alertType: 'new-divergences',
        details: `Panel "${panelId}" has ${proof.divergences.length} new divergences: ${proof.divergences.join('; ')}`,
        severity: classifyDivergences(proof.divergences),
        previousChecksum: null,
        currentChecksum: null,
        timestamp: now,
      });
    }

    // 2. Checksum drift detection (requires cognitive checksum in proof)
    // Proofs don't carry checksums directly, but we can detect when
    // the proof shape changes unexpectedly between consecutive renders
    const prevRowCount = getRowCount(previous.proof);
    const currRowCount = getRowCount(proof);

    // Data changed but render shows same row count → possible stale render
    if (prevRowCount !== currRowCount && proof.divergences.length === 0 && previous.proof.divergences.length === 0) {
      // Row count changed — this is normal. But if it changed drastically, flag it.
      const changeRatio = prevRowCount > 0 ? Math.abs(currRowCount - prevRowCount) / prevRowCount : 0;
      if (changeRatio > 0.5 && prevRowCount > 5) {
        this.publishAlert({
          panelId,
          panelType: proof.panelType,
          alertType: 'checksum-drift',
          details: `Panel "${panelId}" row count changed by ${Math.round(changeRatio * 100)}% (${prevRowCount} → ${currRowCount}). Large data shift may indicate upstream issue.`,
          severity: 'warning',
          previousChecksum: String(prevRowCount),
          currentChecksum: String(currRowCount),
          timestamp: now,
        });
      }
    }
  }

  private checkStaleness(): void {
    const now = Date.now();
    for (const [panelId, ring] of this.history) {
      if (ring.length === 0) continue;
      const latest = ring[ring.length - 1];
      const elapsed = now - latest.receivedAt;

      if (elapsed > STALE_THRESHOLD_MS) {
        this.publishAlert({
          panelId,
          panelType: latest.proof.panelType,
          alertType: 'data-changed-render-stale',
          details: `Panel "${panelId}" has not produced a render proof in ${Math.round(elapsed / 1000)}s. Panel may be unmounted or frozen.`,
          severity: 'warning',
          previousChecksum: null,
          currentChecksum: null,
          timestamp: now,
        });
      }
    }
  }

  private publishAlert(alert: ProofDivergenceAlert): void {
    this.alertCount++;

    // Publish as a violation signal — ViolationMonitor and AlgedonicPanel can subscribe
    signalBus.publish('verification:divergence', alert, '__proofVerifier__');

    // Also publish as a specific panel alert for the DivergenceBadge
    signalBus.publish('__proofAlert__' + alert.panelId, alert, '__proofVerifier__');
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function getRowCount(proof: RenderProof): number {
  switch (proof.panelType) {
    case 'grid':
      return (proof as { model: { totalRowCount: number } }).model.totalRowCount;
    case 'viz':
      return (proof as { model: { dataPointCount: number } }).model.dataPointCount;
    case 'form':
      return (proof as { spec: { fieldCount: number } }).spec.fieldCount;
    default:
      return 0;
  }
}

// ---------------------------------------------------------------------------
// Singleton + exports
// ---------------------------------------------------------------------------

export const proofVerifier = new ProofVerifierImpl();
