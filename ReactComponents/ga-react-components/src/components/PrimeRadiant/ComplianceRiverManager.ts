// src/components/PrimeRadiant/ComplianceRiverManager.ts
// Manages compliance river particle visualization — flow field particles
// showing governance directive flow through the graph.
//
// Integrates FlowFieldEngine (CPU simulation) with FlowFieldParticleTSL
// (GPU rendering via TSL PointsNodeMaterial).

import * as THREE from 'three';
import type { GovernanceNode, GovernanceEdge } from './types';
import { FlowParticleSystem, type FlowFieldConfig } from './FlowFieldEngine';
import { createFlowParticleMaterial, createFlowParticleGeometry } from './shaders/FlowFieldParticleTSL';
import { budgetToTier, type QualityTier } from './shaders/TSLUniforms';

// ── Types ──

interface PositionedNode extends GovernanceNode {
  x?: number;
  y?: number;
  z?: number;
}

export interface ComplianceRiverHandle {
  /** Update particles — call every frame */
  update(dt: number, qualityBudget: number): void;
  /** Rebuild field when graph changes */
  rebuild(nodes: PositionedNode[], edges: GovernanceEdge[]): void;
  /** Dispose everything */
  dispose(): void;
  /** Current active particle count */
  readonly particleCount: number;
}

// ── Quality presets ──

const QUALITY_CONFIGS: Record<QualityTier, Partial<FlowFieldConfig>> = {
  low: { resolution: 8, maxParticles: 500, speedScale: 0.5, curlStrength: 0.15 },
  medium: { resolution: 16, maxParticles: 2000, speedScale: 0.8, curlStrength: 0.25 },
  high: { resolution: 32, maxParticles: 5000, speedScale: 1.0, curlStrength: 0.35 },
};

// ── Factory ──

/**
 * Create a compliance river particle system.
 *
 * @param nodes Governance nodes (with positions)
 * @param edges Governance edges
 * @param scene Scene to add particles to
 * @param quality Initial quality tier
 */
export function createComplianceRivers(
  nodes: GovernanceNode[],
  edges: GovernanceEdge[],
  scene: THREE.Scene | THREE.Group,
  quality: QualityTier,
): ComplianceRiverHandle {
  const config = QUALITY_CONFIGS[quality];
  const maxParticles = config.maxParticles ?? 2000;

  // Create simulation engine
  const engine = new FlowParticleSystem(config);

  // Create rendering geometry + material
  const { geometry, positions, healths, phases, speeds } = createFlowParticleGeometry(maxParticles);

  let material: THREE.PointsNodeMaterial | THREE.PointsMaterial;
  try {
    material = createFlowParticleMaterial({ quality });
  } catch {
    // Fallback if TSL fails
    material = new THREE.PointsMaterial({
      size: 2.0,
      color: 0x44ff88,
      transparent: true,
      opacity: 0.5,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      sizeAttenuation: true,
    });
  }

  const points = new THREE.Points(geometry, material);
  points.name = 'compliance-rivers';
  points.frustumCulled = false;
  points.renderOrder = 2; // render after shells, before bloom
  scene.add(points);

  // Initial field build
  engine.rebuildField(nodes as PositionedNode[], edges);

  // Track last rebuild time
  let lastRebuildTime = 0;
  const REBUILD_INTERVAL = 5000; // rebuild field every 5s as nodes move

  return {
    get particleCount() { return engine.particles.length; },

    update(dt: number, qualityBudget: number) {
      // Hide on very low quality
      const visible = qualityBudget > -0.4;
      points.visible = visible;
      if (!visible) return;

      // Step simulation
      engine.step(dt);

      // Write to GPU buffers
      const count = engine.writeToBuffers(positions, healths, phases, speeds);
      geometry.setDrawRange(0, count);

      // Flag attributes for upload
      (geometry.attributes.position as THREE.BufferAttribute).needsUpdate = true;
      (geometry.attributes.aHealth as THREE.BufferAttribute).needsUpdate = true;
      (geometry.attributes.aPhase as THREE.BufferAttribute).needsUpdate = true;
      (geometry.attributes.aSpeed as THREE.BufferAttribute).needsUpdate = true;
    },

    rebuild(nodes: PositionedNode[], edges: GovernanceEdge[]) {
      const now = Date.now();
      if (now - lastRebuildTime < REBUILD_INTERVAL) return;
      lastRebuildTime = now;
      engine.rebuildField(nodes, edges);
    },

    dispose() {
      scene.remove(points);
      geometry.dispose();
      material.dispose();
    },
  };
}
