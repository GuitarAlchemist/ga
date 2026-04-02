// src/components/PrimeRadiant/CrisisTextureManager.ts
// Manages reaction-diffusion crisis textures for governance nodes.
//
// Runs GrayScottGrid simulations at 2Hz (low CPU cost), produces
// DataTextures that drive TSL materials on node meshes.
// Health status → RD parameters: healthy=spots, crisis=chaos.

import * as THREE from 'three';
import type { GovernanceNode, GovernanceHealthStatus, GovernanceNodeType } from './types';
import { GrayScottGrid } from './ReactionDiffusionSim';
import { createCrisisMaterial } from './shaders/ReactionDiffusionTSL';
import { NODE_COLORS } from './types';
import type { QualityTier } from './shaders/TSLUniforms';

// ── Types ──

interface GraphNode extends GovernanceNode {
  x?: number;
  y?: number;
  z?: number;
  __threeObj?: THREE.Object3D;
}

interface SimEntry {
  grid: GrayScottGrid;
  health: GovernanceHealthStatus;
  material: THREE.MeshStandardNodeMaterial | null;
}

export interface CrisisTextureHandle {
  /** Tick — runs RD simulation at 2Hz internally */
  update(nodes: GraphNode[], qualityBudget: number): void;
  /** Get the crisis material for a node type + health (returns null if quality too low) */
  getMaterial(type: GovernanceNodeType, health: GovernanceHealthStatus): THREE.MeshStandardNodeMaterial | null;
  /** Dispose all */
  dispose(): void;
}

// ── Quality settings ──

const QUALITY_GRID_SIZE: Record<QualityTier, number> = {
  low: 0,    // disabled
  medium: 64,
  high: 128,
};

const QUALITY_ITERATIONS: Record<QualityTier, number> = {
  low: 0,
  medium: 5,
  high: 10,
};

// ── Factory ──

export function createCrisisTextures(quality: QualityTier): CrisisTextureHandle {
  const gridSize = QUALITY_GRID_SIZE[quality];

  // On low quality, return a no-op handle
  if (gridSize === 0) {
    return {
      update() {},
      getMaterial() { return null; },
      dispose() {},
    };
  }

  // One simulation per node type that has unhealthy nodes
  const sims = new Map<string, SimEntry>(); // key: `${type}-${health}`
  let lastUpdateTime = 0;
  const UPDATE_INTERVAL_MS = 500; // 2Hz

  const iterations = QUALITY_ITERATIONS[quality];

  /** Get or create a simulation for a type+health combination */
  function getOrCreateSim(type: GovernanceNodeType, health: GovernanceHealthStatus): SimEntry {
    const key = `${type}-${health}`;
    let entry = sims.get(key);
    if (!entry) {
      const grid = new GrayScottGrid(gridSize, gridSize);
      grid.setHealth(health);
      // Run a few warmup iterations so patterns are visible immediately
      grid.step(50);
      grid.updateTexture();

      const baseColor = new THREE.Color(NODE_COLORS[type] ?? '#668899');
      let material: THREE.MeshStandardNodeMaterial | null = null;
      try {
        material = createCrisisMaterial({
          baseColor,
          rdTexture: grid.getTexture(),
          quality,
        });
      } catch {
        // TSL failed — material stays null, caller uses existing material
      }

      entry = { grid, health, material };
      sims.set(key, entry);
    }
    return entry;
  }

  return {
    update(nodes: GraphNode[], qualityBudget: number) {
      if (qualityBudget < -0.3) return; // skip on low quality budget

      const now = Date.now();
      if (now - lastUpdateTime < UPDATE_INTERVAL_MS) return;
      lastUpdateTime = now;

      // Collect unique type+health combos from graph
      const activeKeys = new Set<string>();
      for (const n of nodes) {
        const health = n.healthStatus ?? 'unknown';
        // Only apply RD to non-healthy nodes on medium quality
        if (quality === 'medium' && health === 'healthy') continue;
        const key = `${n.type}-${health}`;
        activeKeys.add(key);

        // Ensure sim exists
        const entry = getOrCreateSim(n.type, health);
        entry.grid.setHealth(health);
      }

      // Step all active simulations
      for (const [key, entry] of sims) {
        if (!activeKeys.has(key)) continue;
        entry.grid.step(iterations);
        entry.grid.updateTexture();
      }
    },

    getMaterial(type: GovernanceNodeType, health: GovernanceHealthStatus): THREE.MeshStandardNodeMaterial | null {
      const key = `${type}-${health}`;
      return sims.get(key)?.material ?? null;
    },

    dispose() {
      for (const entry of sims.values()) {
        entry.grid.dispose();
        entry.material?.dispose();
      }
      sims.clear();
    },
  };
}
