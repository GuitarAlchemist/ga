// src/components/PrimeRadiant/CymaticFilaments.ts
// Cymatics wave enhancement layer for terminal filaments.
//
// Adds TubeGeometry filaments with TSL wave displacement alongside
// existing Line-based filaments. When quality allows, these replace
// the visual appearance while keeping the existing filament system
// for position tracking and lifecycle.
//
// Governance meaning:
//   Standing waves = alignment, Interference noise = conflict

import * as THREE from 'three';
import { createCymaticsMaterial } from './shaders/CymaticsTSL';
import type { QualityTier } from './shaders/TSLUniforms';

// ── Types ──

interface CymaticTube {
  mesh: THREE.Mesh;
  alignmentUniform: ReturnType<typeof import('three/tsl').uniform>;
  origin: THREE.Vector3;
  direction: THREE.Vector3;
  length: number;
  nodeId: string;
}

export interface CymaticFilamentsHandle {
  group: THREE.Group;
  /** Update tube positions from existing filament node tracking */
  update(nodePositions: Map<string, THREE.Vector3>, alignments: Map<string, number>): void;
  dispose(): void;
  readonly tubeCount: number;
}

// ── Config ──

interface CymaticConfig {
  /** Tube radius. Default: 0.02 */
  radius?: number;
  /** Radial segments. Default: 4 (low) / 6 (medium) / 8 (high) */
  radialSegments?: number;
  /** Tubular segments along length. Default: 8 (medium) / 16 (high) */
  tubularSegments?: number;
}

const QUALITY_CONFIG: Record<QualityTier, CymaticConfig> = {
  low: { radius: 0.015, radialSegments: 3, tubularSegments: 6 },
  medium: { radius: 0.02, radialSegments: 4, tubularSegments: 8 },
  high: { radius: 0.025, radialSegments: 6, tubularSegments: 16 },
};

// ── Factory ──

/**
 * Create cymatics wave filaments for terminal governance nodes.
 *
 * @param terminalNodes Array of { position, direction, nodeId, length }
 * @param quality Quality tier
 */
export function createCymaticFilaments(
  terminalNodes: { position: THREE.Vector3; direction: THREE.Vector3; nodeId: string; length: number }[],
  quality: QualityTier,
): CymaticFilamentsHandle {
  const group = new THREE.Group();
  group.name = 'cymatics-filaments';
  const tubes: CymaticTube[] = [];

  const config = QUALITY_CONFIG[quality];
  const { radius = 0.02, radialSegments = 4, tubularSegments = 8 } = config;

  for (const node of terminalNodes) {
    // Create a straight tube along the filament direction
    const start = node.position.clone();
    const end = start.clone().add(node.direction.clone().multiplyScalar(node.length));

    // CatmullRomCurve3 with slight bend for organic feel
    const mid = start.clone().lerp(end, 0.5);
    mid.x += (Math.random() - 0.5) * node.length * 0.15;
    mid.y += (Math.random() - 0.5) * node.length * 0.15;
    mid.z += (Math.random() - 0.5) * node.length * 0.15;

    const curve = new THREE.CatmullRomCurve3([start, mid, end]);
    const geo = new THREE.TubeGeometry(curve, tubularSegments, radius, radialSegments, false);

    // Initial alignment (will be updated per frame)
    const alignment = 0.8; // default to mostly aligned
    const color = new THREE.Color(0x88ddff);

    const { material, alignmentUniform } = createCymaticsMaterial({
      alignment,
      color,
      quality,
    });

    const mesh = new THREE.Mesh(geo, material);
    mesh.name = `cymatics-${node.nodeId}`;
    mesh.frustumCulled = false;
    group.add(mesh);

    tubes.push({
      mesh,
      alignmentUniform,
      origin: node.position.clone(),
      direction: node.direction.clone(),
      length: node.length,
      nodeId: node.nodeId,
    });
  }

  return {
    group,
    get tubeCount() { return tubes.length; },

    update(nodePositions: Map<string, THREE.Vector3>, alignments: Map<string, number>) {
      for (const tube of tubes) {
        // Update alignment from governance data
        const alignment = alignments.get(tube.nodeId) ?? 0.8;
        tube.alignmentUniform.value = alignment;

        // Track node position if it moved
        const pos = nodePositions.get(tube.nodeId);
        if (pos) {
          const delta = pos.clone().sub(tube.origin);
          tube.mesh.position.copy(delta);
        }
      }
    },

    dispose() {
      for (const tube of tubes) {
        group.remove(tube.mesh);
        tube.mesh.geometry.dispose();
        if (tube.mesh.material instanceof THREE.Material) tube.mesh.material.dispose();
      }
      tubes.length = 0;
    },
  };
}
