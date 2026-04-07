// UncertaintyHalo.ts — Probabilistic halo for governance nodes with uncertainty
// Governance nodes are emergent, not deterministic. This halo visualizes
// "where this node might be next cycle" — a translucent sphere that breathes
// with the node's uncertainty level.

import * as THREE from 'three';
import type { GovernanceHealthStatus } from './types';

// ---------------------------------------------------------------------------
// Uncertainty mapping — how much positional doubt each health state carries
// ---------------------------------------------------------------------------
export const UNCERTAINTY_LEVEL: Record<GovernanceHealthStatus, number> = {
  healthy: 0,
  warning: 0.15,
  error: 0.4,
  unknown: 0.25,
  contradictory: 0.5,
};

// Halo color per health — uncertain states get cooler, diffuse tones
const HALO_COLOR: Record<GovernanceHealthStatus, number> = {
  healthy: 0x33cc66,       // green — stable, barely visible
  warning: 0xffaa44,       // amber
  error: 0xff4444,         // red — sharp uncertainty
  unknown: 0x4488ff,       // blue — epistemic fog
  contradictory: 0xcc44ff, // purple — conflicting signals
};

/**
 * Create a translucent BackSide sphere representing positional uncertainty.
 * Radius scales with the node's base radius and its uncertainty level.
 * Returns null for healthy nodes (zero uncertainty = no halo needed).
 */
export function createUncertaintyHalo(
  baseRadius: number,
  healthStatus: GovernanceHealthStatus,
): THREE.Mesh | null {
  const uncertainty = UNCERTAINTY_LEVEL[healthStatus] ?? 0;
  if (uncertainty <= 0) return null;

  const haloRadius = baseRadius * (1.2 + uncertainty * 0.8);
  const geo = new THREE.SphereGeometry(haloRadius, 16, 16);
  const mat = new THREE.MeshBasicMaterial({
    color: HALO_COLOR[healthStatus] ?? 0x4488ff,
    transparent: true,
    opacity: 0.03 + uncertainty * 0.07,
    side: THREE.BackSide,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
  });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.userData.isUncertaintyHalo = true;
  mesh.userData.baseUncertainty = uncertainty;
  return mesh;
}

/**
 * Animate the halo each frame — pulse opacity and breathe scale.
 * Called from the animation loop; no allocations, no traversals.
 */
export function updateUncertaintyHalo(
  halo: THREE.Mesh,
  time: number,
  phase: number,
): void {
  const uncertainty = halo.userData.baseUncertainty as number;
  if (!uncertainty) return;

  // Opacity pulse — slow, ethereal breathing
  const pulse = 1.0 + 0.2 * Math.sin(time * 1.5 + phase);
  (halo.material as THREE.MeshBasicMaterial).opacity =
    (0.03 + uncertainty * 0.07) * pulse;

  // Scale breathes — larger uncertainty = more visible expansion
  const scale = 1.0 + uncertainty * 0.3 * (1 + 0.1 * Math.sin(time * 2.0 + phase * 0.7));
  halo.scale.setScalar(scale);
}
