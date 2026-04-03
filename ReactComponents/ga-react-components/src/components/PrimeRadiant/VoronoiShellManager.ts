// src/components/PrimeRadiant/VoronoiShellManager.ts
// Manages Voronoi jurisdiction shell meshes for governance clusters.
//
// Detects clusters from graph topology (connected components via
// constitutional-hierarchy edges), creates translucent shell meshes
// per cluster, and updates seed positions per frame as force layout moves.

import * as THREE from 'three';
import type { GovernanceNode, GovernanceEdge } from './types';
import { createVoronoiShellMaterial, CLUSTER_COLORS } from './shaders/VoronoiShellTSL';
import { budgetToTier, type QualityTier } from './shaders/TSLUniforms';

// ── Types ──

interface GraphNode extends GovernanceNode {
  x?: number;
  y?: number;
  z?: number;
}

interface ClusterShell {
  id: string;
  type: string; // dominant governance type in cluster
  mesh: THREE.Mesh;
  nodeIds: Set<string>;
  seedPositions: THREE.Uniform<THREE.Vector3[]>;
  activeSeedCount: THREE.Uniform<number>;
  centroid: THREE.Vector3;
}

export interface VoronoiShellHandle {
  /** Update shell positions from current force layout */
  update(nodes: GraphNode[], qualityBudget: number): void;
  /** Dispose all shells */
  dispose(): void;
  /** Current shell count */
  readonly shellCount: number;
}

// ── Cluster detection ──

/** Find connected components via constitutional-hierarchy edges */
function detectClusters(
  nodes: GovernanceNode[],
  edges: GovernanceEdge[],
): Map<string, Set<string>> {
  // Union-Find
  const parent = new Map<string, string>();
  const find = (x: string): string => {
    if (!parent.has(x)) parent.set(x, x);
    if (parent.get(x) !== x) parent.set(x, find(parent.get(x)!));
    return parent.get(x)!;
  };
  const union = (a: string, b: string) => {
    const ra = find(a), rb = find(b);
    if (ra !== rb) parent.set(ra, rb);
  };

  // Initialize all nodes
  for (const n of nodes) find(n.id);

  // Union via hierarchy edges (constitutional-hierarchy and policy-persona)
  for (const e of edges) {
    if (e.type === 'constitutional-hierarchy' || e.type === 'policy-persona') {
      union(e.source, e.target);
    }
  }

  // Group by root
  const clusters = new Map<string, Set<string>>();
  for (const n of nodes) {
    const root = find(n.id);
    if (!clusters.has(root)) clusters.set(root, new Set());
    clusters.get(root)!.add(n.id);
  }

  return clusters;
}

/** Find the dominant governance type in a cluster */
function dominantType(nodeIds: Set<string>, nodeMap: Map<string, GovernanceNode>): string {
  const counts = new Map<string, number>();
  for (const id of nodeIds) {
    const n = nodeMap.get(id);
    if (!n) continue;
    counts.set(n.type, (counts.get(n.type) ?? 0) + 1);
  }
  let best = 'default';
  let bestCount = 0;
  for (const [type, count] of counts) {
    if (count > bestCount) { best = type; bestCount = count; }
  }
  // Constitution always wins if present
  if (counts.has('constitution')) return 'constitution';
  return best;
}

// ── Manager ──

/**
 * Create Voronoi jurisdiction shells for the governance graph.
 *
 * @param nodes Governance nodes
 * @param edges Governance edges
 * @param scene Three.js scene to add shells to
 * @param quality Initial quality tier
 * @returns Handle for per-frame updates and cleanup
 */
export function createVoronoiShells(
  nodes: GovernanceNode[],
  edges: GovernanceEdge[],
  scene: THREE.Scene | THREE.Group,
  quality: QualityTier,
): VoronoiShellHandle {
  const nodeMap = new Map(nodes.map(n => [n.id, n]));
  const clusters = detectClusters(nodes, edges);
  const shells: ClusterShell[] = [];

  // Only create shells for clusters with 3+ nodes (singletons/pairs aren't meaningful)
  for (const [_root, nodeIds] of clusters) {
    if (nodeIds.size < 3) continue;

    const type = dominantType(nodeIds, nodeMap);
    const color = CLUSTER_COLORS[type] ?? CLUSTER_COLORS.default;

    const { material, seedPositions, activeSeedCount } = createVoronoiShellMaterial({
      color,
      seedCount: nodeIds.size,
      quality,
    });

    // Initial shell: unit sphere, will be scaled/positioned per frame
    const geo = new THREE.SphereGeometry(1, 24, 24);
    const mesh = new THREE.Mesh(geo, material);
    mesh.name = `voronoi-shell-${type}`;
    mesh.frustumCulled = false;
    mesh.renderOrder = -1; // render before opaque nodes
    mesh.visible = false; // hidden until update() — prevents WebGL crash on TSL NodeMaterial

    scene.add(mesh);
    shells.push({
      id: _root,
      type,
      mesh,
      nodeIds,
      seedPositions,
      activeSeedCount,
      centroid: new THREE.Vector3(),
    });
  }

  // Reusable vectors
  const _pos = new THREE.Vector3();
  const _min = new THREE.Vector3();
  const _max = new THREE.Vector3();

  return {
    get shellCount() { return shells.length; },

    update(graphNodes: GraphNode[], qualityBudget: number) {
      // Hide shells on very low quality
      const visible = qualityBudget > 0.0; // only show on high quality — shells are expensive

      for (const shell of shells) {
        shell.mesh.visible = visible;
        if (!visible) continue;

        // Compute bounding box and centroid from positioned nodes
        _min.set(Infinity, Infinity, Infinity);
        _max.set(-Infinity, -Infinity, -Infinity);
        shell.centroid.set(0, 0, 0);
        let count = 0;

        for (const gn of graphNodes) {
          if (!shell.nodeIds.has(gn.id) || gn.x === undefined) continue;
          _pos.set(gn.x, gn.y ?? 0, gn.z ?? 0);
          _min.min(_pos);
          _max.max(_pos);
          shell.centroid.add(_pos);
          count++;
        }

        if (count === 0) { shell.mesh.visible = false; continue; }

        shell.centroid.divideScalar(count);

        // Shell radius: half the bounding box diagonal + padding
        const radius = _min.distanceTo(_max) * 0.5 + 5;

        // Position and scale shell
        shell.mesh.position.copy(shell.centroid);
        shell.mesh.scale.setScalar(radius);

        // Update seed positions (relative to shell center, normalized to shell radius)
        const seeds = shell.seedPositions.value as THREE.Vector3[];
        let seedIdx = 0;
        for (const gn of graphNodes) {
          if (!shell.nodeIds.has(gn.id) || gn.x === undefined) continue;
          if (seedIdx >= seeds.length) break;
          seeds[seedIdx].set(
            (gn.x - shell.centroid.x) / radius,
            ((gn.y ?? 0) - shell.centroid.y) / radius,
            ((gn.z ?? 0) - shell.centroid.z) / radius,
          );
          seedIdx++;
        }
        shell.activeSeedCount.value = seedIdx;
      }
    },

    dispose() {
      for (const shell of shells) {
        scene.remove(shell.mesh);
        shell.mesh.geometry.dispose();
        if (shell.mesh.material instanceof THREE.Material) {
          shell.mesh.material.dispose();
        }
      }
      shells.length = 0;
    },
  };
}
