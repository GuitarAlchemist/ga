// src/components/PrimeRadiant/shaders/TSLUniforms.ts
// Central TSL uniform management for all Prime Radiant procedural materials.
// Updated once per frame from ForceRadiant.tsx tick loop.
// Avoids creating duplicate uniforms across materials.

import * as THREE from 'three';
import { uniform } from 'three/tsl';

/** Quality tier for TSL material complexity selection */
export type QualityTier = 'low' | 'medium' | 'high';

// ─── Shared Uniforms (created once, reused by all TSL materials) ───

/** Quality level as float: 0.0 = low, 0.5 = medium, 1.0 = high */
export const uQualityLevel = uniform(1.0);

/** Camera world position — updated per frame for Fresnel, LOD, etc. */
export const uCameraPosition = uniform(new THREE.Vector3());

/** Quality budget from ForceRadiant (-1.0 to +1.0) */
export const uQualityBudget = uniform(0.0);

/** Number of governance nodes (for Voronoi seed count, etc.) */
export const uNodeCount = uniform(0);

// ─── Update function (call once per frame from tick loop) ───

export function updateTSLUniforms(
  qualityBudget: number,
  qualityLevel: QualityTier,
  cameraPos: THREE.Vector3,
  nodeCount: number,
): void {
  uQualityBudget.value = qualityBudget;
  uQualityLevel.value = qualityLevel === 'high' ? 1.0 : qualityLevel === 'medium' ? 0.5 : 0.0;
  (uCameraPosition.value as THREE.Vector3).copy(cameraPos);
  uNodeCount.value = nodeCount;
}

// ─── Quality helpers ───

/** Map qualityBudget to a QualityTier string */
export function budgetToTier(qualityBudget: number): QualityTier {
  if (qualityBudget > 0.2) return 'high';
  if (qualityBudget > -0.3) return 'medium';
  return 'low';
}

/** Check if a feature should be enabled at the current quality budget */
export function featureEnabled(qualityBudget: number, threshold: number): boolean {
  return qualityBudget > threshold;
}
