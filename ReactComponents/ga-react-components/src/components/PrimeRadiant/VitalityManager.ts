// src/components/PrimeRadiant/VitalityManager.ts
// Vitality Decay + Focal Crystallization
// Panels that aren't interacted with naturally blur and shed quality budget.
// Cursor proximity drives local quality — entities crystallize near the cursor.

import { stateRegistry } from './StateRegistry';

// ---------------------------------------------------------------------------
// Vitality Decay — panels blur when unused
// ---------------------------------------------------------------------------

export interface PanelVitality {
  panelId: string;
  vitality: number;          // 1.0 = fully alive, 0.0 = dormant
  lastInteraction: number;   // epoch ms
}

class VitalityManagerImpl {
  private panels = new Map<string, PanelVitality>();
  private tickId: number | null = null;

  /** Register a panel for vitality tracking */
  trackPanel(panelId: string): void {
    if (!this.panels.has(panelId)) {
      this.panels.set(panelId, {
        panelId,
        vitality: 1.0,
        lastInteraction: Date.now(),
      });
    }
  }

  /** Record user interaction (click, scroll, type) on a panel */
  touch(panelId: string): void {
    const panel = this.panels.get(panelId);
    if (panel) {
      panel.lastInteraction = Date.now();
      panel.vitality = 1.0; // full restore on interaction
    }
  }

  /** Get vitality for a panel */
  getVitality(panelId: string): number {
    return this.panels.get(panelId)?.vitality ?? 1.0;
  }

  /** Start the 1Hz vitality decay tick */
  start(): void {
    if (this.tickId !== null) return;
    this.tickId = window.setInterval(() => this.tick(), 1000);
  }

  /** Stop */
  stop(): void {
    if (this.tickId !== null) {
      window.clearInterval(this.tickId);
      this.tickId = null;
    }
  }

  private tick(): void {
    const now = Date.now();
    const decayRate = stateRegistry.get('interaction.vitality.decayRate') ?? 0.001;

    for (const panel of this.panels.values()) {
      const idleSeconds = (now - panel.lastInteraction) / 1000;
      // Exponential decay: vitality drops faster the longer idle
      // But floor at 0.2 — never fully invisible
      const newVitality = Math.max(0.2, 1.0 - decayRate * idleSeconds);
      panel.vitality = newVitality;
    }
  }

  /** Get all panels with their vitality */
  getAll(): PanelVitality[] {
    return Array.from(this.panels.values());
  }

  /** Untrack a panel */
  untrack(panelId: string): void {
    this.panels.delete(panelId);
  }

  reset(): void {
    this.stop();
    this.panels.clear();
  }
}

export const vitalityManager = new VitalityManagerImpl();

// ---------------------------------------------------------------------------
// Focal Crystallization — cursor proximity drives local quality
// ---------------------------------------------------------------------------

export interface FocalPoint {
  x: number;
  y: number;
  radius: number;
  boost: number;
}

/**
 * Compute the quality multiplier for a screen position based on
 * cursor proximity. Entities near the cursor get a quality boost;
 * entities far away get reduced quality.
 *
 * @param entityScreenX — entity's screen X position
 * @param entityScreenY — entity's screen Y position
 * @param cursorX — cursor X
 * @param cursorY — cursor Y
 * @returns multiplier 0.0-1.0 where 1.0 = full quality at cursor
 */
export function focalQuality(
  entityScreenX: number,
  entityScreenY: number,
  cursorX: number,
  cursorY: number,
): number {
  const radius = stateRegistry.get('interaction.focal.radius') ?? 150;
  const boost = stateRegistry.get('interaction.focal.boost') ?? 0.5;

  const dx = entityScreenX - cursorX;
  const dy = entityScreenY - cursorY;
  const dist = Math.sqrt(dx * dx + dy * dy);

  if (dist < radius) {
    // Inside focal zone — full boost at center, linear falloff to edge
    const t = 1.0 - (dist / radius);
    return 1.0 - boost + boost * t; // at center: 1.0, at edge: 1.0 - boost
  }

  // Outside focal zone — reduced quality
  // Gentle falloff beyond the radius
  const falloff = Math.min(1.0, (dist - radius) / (radius * 2));
  return Math.max(0.3, (1.0 - boost) * (1.0 - falloff * 0.5));
}
