// src/components/PrimeRadiant/JurisdictionVolumetrics.ts
// Manager for Local Proxy Raymarching (LPR) volumetric boxes — one per
// governance-type cluster. Parallels VoronoiShellManager but with a
// different visual idiom: VoronoiShell draws the jurisdiction MEMBRANE;
// LPR draws the jurisdiction INTERIOR atmosphere.
//
// Governance meaning: each type-cluster (constitution, department, policy,
// persona, pipeline, schema, test) gets a box-proxy scaled to its cluster
// AABB. A TSL raymarch material renders bounded volumetric scattering
// inside each box. The visible result: each jurisdiction has its own
// "authority fog" in its own color, confined to its territory.
//
// Integration in ForceRadiant.tsx (one-liner near createVoronoiShells):
//   jurisdictionVolHandle = createJurisdictionVolumetrics(
//     graph.nodes, fg.scene(), budgetToTier(qualityBudget),
//   );
// And in the tick loop:
//   jurisdictionVolHandle.update(fg.graphData().nodes, qualityBudget);

import * as THREE from 'three';
import type { GovernanceNode } from './types';
import { createLocalProxyVolumetricMaterial } from './shaders/LocalProxyVolumetricTSL';
import { CLUSTER_COLORS } from './shaders/VoronoiShellTSL';
import type { QualityTier } from './shaders/TSLUniforms';

// ── Types ──

interface GraphNode extends GovernanceNode {
  x?: number;
  y?: number;
  z?: number;
}

interface ProxyBox {
  type: string;
  mesh: THREE.Mesh;
  nodeIds: Set<string>;
  boxMin: THREE.Uniform<THREE.Vector3>;
  boxMax: THREE.Uniform<THREE.Vector3>;
}

export interface JurisdictionVolumetricsHandle {
  update(nodes: GraphNode[], qualityBudget: number): void;
  dispose(): void;
  readonly proxyCount: number;
  setRevealed(on: boolean): void;
  readonly isRevealed: boolean;
}

// ── Cluster detection (mirrors VoronoiShellManager: one cluster per type) ──

function detectTypeClusters(nodes: GovernanceNode[]): Map<string, Set<string>> {
  const clusters = new Map<string, Set<string>>();
  for (const n of nodes) {
    const key = n.type;
    if (!clusters.has(key)) clusters.set(key, new Set());
    clusters.get(key)!.add(n.id);
  }
  return clusters;
}

// ── Factory ──

/**
 * Create local-proxy volumetric boxes, one per type-cluster.
 *
 * Only clusters with ≥3 nodes get a proxy (singletons would be
 * degenerate/misleading). Proxy geometry is a unit cube scaled
 * per-frame to the cluster's axis-aligned bounding box.
 */
export function createJurisdictionVolumetrics(
  nodes: GovernanceNode[],
  scene: THREE.Scene | THREE.Group,
  quality: QualityTier,
): JurisdictionVolumetricsHandle {
  const clusters = detectTypeClusters(nodes);
  const proxies: ProxyBox[] = [];

  for (const [type, nodeIds] of clusters) {
    if (nodeIds.size < 3) continue;

    const color = CLUSTER_COLORS[type] ?? CLUSTER_COLORS.default;
    const { material, boxMin, boxMax } = createLocalProxyVolumetricMaterial({
      color,
      quality,
      // Map governance type → institutional openness via HG anisotropy:
      // constitution (transparent authority) → high forward scattering
      // policy/schema (rules) → moderate forward
      // persona (agents) → near isotropic
      anisotropy: type === 'constitution' ? 0.55
        : type === 'policy' || type === 'schema' ? 0.35
        : type === 'persona' || type === 'test' ? 0.15
        : 0.25,
      // Extinction cut in half — overlapping clusters were compounding
      // into opaque fog that washed out the graph.
      extinction: 0.04,
      // Intensity cut in half again — should feel like a faint hint of
      // authority color around clusters, not a dominant atmospheric layer.
      intensity: 0.05,
      // Swirl rate ≈ institutional velocity.
      // Constitutional: slow, dignified. Pipeline/persona: active, busy.
      // Test: jittery. Department: steady managerial pace.
      swirlRate: type === 'constitution' ? 0.04
        : type === 'department' ? 0.08
        : type === 'policy' || type === 'schema' ? 0.12
        : type === 'pipeline' ? 0.20
        : type === 'persona' ? 0.28
        : type === 'test' ? 0.35
        : 0.15,
    });

    // Unit cube — scaled per-frame via mesh.scale to fit cluster AABB.
    // We also update boxMin/boxMax uniforms with the WORLD-space AABB so
    // the shader's slab intersect operates in world space (same space as
    // cameraPosition + positionWorld).
    const geo = new THREE.BoxGeometry(1, 1, 1);
    const mesh = new THREE.Mesh(geo, material);
    mesh.name = `lpr-proxy-${type}`;
    mesh.frustumCulled = false;
    // Render AFTER voronoi shells (-1) but BEFORE opaque nodes (default 0).
    // Additive + depthTest=false means draw order mostly affects blending
    // priority when proxies overlap; volumetric-behind-shell reads nicest.
    mesh.renderOrder = -0.5;
    mesh.userData.isJurisdictionVolumetric = true;
    mesh.userData.jurisdictionType = type;

    scene.add(mesh);
    proxies.push({ type, mesh, nodeIds, boxMin, boxMax });
  }

  // Reusable vectors — zero GC pressure per tick
  const _min = new THREE.Vector3();
  const _max = new THREE.Vector3();
  const _pos = new THREE.Vector3();
  const _center = new THREE.Vector3();
  const _size = new THREE.Vector3();

  // Padding around cluster AABB — prevents nodes from sitting exactly at
  // box edges where raymarching has zero interval. 6 units is roughly
  // one node-radius in Prime Radiant's scale.
  const AABB_PADDING = 6;

  // Opt-in: hidden by default, revealed only on legend hover.
  let revealed = false;

  return {
    get proxyCount() { return proxies.length; },
    setRevealed(on: boolean) { revealed = on; },
    get isRevealed() { return revealed; },

    update(graphNodes: GraphNode[], qualityBudget: number) {
      // Hide on negative quality budget OR when not revealed by user.
      const visible = qualityBudget > -0.1 && revealed;

      for (const proxy of proxies) {
        proxy.mesh.visible = visible;
        if (!visible) continue;

        _min.set(Infinity, Infinity, Infinity);
        _max.set(-Infinity, -Infinity, -Infinity);
        let count = 0;

        for (const gn of graphNodes) {
          if (!proxy.nodeIds.has(gn.id) || gn.x === undefined) continue;
          _pos.set(gn.x, gn.y ?? 0, gn.z ?? 0);
          _min.min(_pos);
          _max.max(_pos);
          count++;
        }

        if (count < 3) { proxy.mesh.visible = false; continue; }

        // Pad the AABB
        _min.subScalar(AABB_PADDING);
        _max.addScalar(AABB_PADDING);

        _center.addVectors(_min, _max).multiplyScalar(0.5);
        _size.subVectors(_max, _min);

        proxy.mesh.position.copy(_center);
        proxy.mesh.scale.copy(_size); // unit cube → AABB-sized

        // World-space AABB uniforms for shader slab intersect
        proxy.boxMin.value.copy(_min);
        proxy.boxMax.value.copy(_max);
      }
    },

    dispose() {
      for (const p of proxies) {
        scene.remove(p.mesh);
        p.mesh.geometry.dispose();
        if (p.mesh.material instanceof THREE.Material) p.mesh.material.dispose();
      }
      proxies.length = 0;
    },
  };
}
