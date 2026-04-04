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

/**
 * Group nodes into jurisdictions by governance type.
 *
 * Prior implementation used edge-flood Union-Find on both
 * constitutional-hierarchy AND policy-persona edges, which collapsed ~75%
 * of the graph into one mega-cluster labelled "constitutional" because
 * dominantType hardcoded constitution as winner. The resulting shell was
 * an opaque dome wrapping almost every node — no bounded meaning.
 *
 * Type-based clustering gives one jurisdiction per governance type
 * (constitution, department, policy, persona, pipeline, schema, test).
 * Each cluster has a clean boundary, a matching legend swatch, and the
 * shell's color/label is determined deterministically by the type itself
 * — no dominant-type voting, no edge-flood.
 */
function detectClusters(
  nodes: GovernanceNode[],
  _edges: GovernanceEdge[],
): Map<string, Set<string>> {
  const clusters = new Map<string, Set<string>>();
  for (const n of nodes) {
    const key = n.type; // one cluster per type
    if (!clusters.has(key)) clusters.set(key, new Set());
    clusters.get(key)!.add(n.id);
  }
  return clusters;
}

/**
 * Dominant type is now trivial: every cluster's key IS the type.
 * Kept as a function so the shell-creation callsite stays unchanged.
 */
function dominantType(nodeIds: Set<string>, nodeMap: Map<string, GovernanceNode>): string {
  for (const id of nodeIds) {
    const n = nodeMap.get(id);
    if (n) return n.type;
  }
  return 'default';
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
    // Tag for hover raycaster — the UI layer reads this to show a label
    // with "Constitutional Jurisdiction" + node count + color accent.
    mesh.userData.isVoronoiShell = true;
    mesh.userData.shellType = type;
    mesh.userData.shellNodeCount = nodeIds.size;
    mesh.userData.shellColorHex = '#' + color.getHexString();

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
