// src/components/PrimeRadiant/NodeInstancer.ts
// InstancedMesh renderer for governance graph nodes.
// Reduces ~600+ draw calls (individual Mesh per node) to ~8 (one per GovernanceNodeType).
//
// Sprites and particle dust remain per-node (lightweight, hard to instance).
// The mesh core is the expensive part — this module handles it.
//
// Integration point (ForceRadiant.tsx — future PR):
//   const instancer = createNodeInstancer(scene, nodes);
//   // After each force-graph tick:
//   instancer.syncPositions(nodes);
//   // In animation loop:
//   instancer.update(time);
//   // On selection change:
//   instancer.highlight(selectedNodeId);
//   // On unmount:
//   instancer.dispose();

import * as THREE from 'three';
import type { GovernanceNodeType, GovernanceHealthStatus } from './types';
import { getNodeMaterial } from './CrystalNodeMaterials';

// ---------------------------------------------------------------------------
// Re-export constants that match ForceRadiant.tsx exactly
// ---------------------------------------------------------------------------

/** Size factor per type — mirrors ForceRadiant TYPE_SIZE */
const TYPE_SIZE: Record<GovernanceNodeType, number> = {
  constitution: 30,
  department: 18,
  policy: 8,
  persona: 8,
  pipeline: 7,
  schema: 4,
  test: 4,
  ixql: 4,
};

/** Geometry factory per type — mirrors ForceRadiant TYPE_GEOMETRY */
const TYPE_GEOMETRY: Record<GovernanceNodeType, (r: number) => THREE.BufferGeometry> = {
  constitution: (r) => new THREE.DodecahedronGeometry(r, 0),
  department:   (r) => new THREE.OctahedronGeometry(r, 0),
  policy:       (r) => new THREE.BoxGeometry(r * 1.4, r * 1.4, r * 1.4),
  persona:      (r) => new THREE.ConeGeometry(r, r * 2, 8),
  pipeline:     (r) => new THREE.CylinderGeometry(r * 0.5, r * 0.5, r * 2, 8),
  schema:       (r) => new THREE.TetrahedronGeometry(r, 0),
  test:         (r) => new THREE.IcosahedronGeometry(r, 1),
  ixql:         (r) => new THREE.TorusKnotGeometry(r * 0.6, r * 0.2, 32, 8),
};

/** Visual prominence per health state — mirrors ForceRadiant HEALTH_PROMINENCE */
const HEALTH_PROMINENCE: Record<GovernanceHealthStatus, {
  sizeMult: number;
  particleMult: number;
  opacity: number;
  glowIntensity: number;
  spinSpeed: number;
}> = {
  error:         { sizeMult: 1.5, particleMult: 2.0, opacity: 1.0, glowIntensity: 1.8, spinSpeed: 2.0 },
  warning:       { sizeMult: 1.3, particleMult: 1.5, opacity: 1.0, glowIntensity: 1.4, spinSpeed: 0.6 },
  healthy:       { sizeMult: 1.0, particleMult: 1.0, opacity: 1.0, glowIntensity: 1.0, spinSpeed: 0.1 },
  unknown:       { sizeMult: 0.7, particleMult: 0.5, opacity: 0.4, glowIntensity: 0.5, spinSpeed: 0.0 },
  contradictory: { sizeMult: 1.5, particleMult: 2.0, opacity: 1.0, glowIntensity: 1.8, spinSpeed: 1.5 },
};

const ALL_TYPES: GovernanceNodeType[] = [
  'constitution', 'department', 'policy', 'persona',
  'pipeline', 'schema', 'test', 'ixql',
];

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** Minimal node interface required by the instancer (subset of ForceRadiant GraphNode) */
export interface InstancerNode {
  id: string;
  type: GovernanceNodeType;
  color: string;
  healthStatus?: GovernanceHealthStatus;
  x?: number;
  y?: number;
  z?: number;
}

/** Handle returned by createNodeInstancer */
export interface NodeInstancerHandle {
  /** Call once after force-graph assigns positions */
  syncPositions(nodes: InstancerNode[]): void;
  /** Call every frame for animation (rotation, glow pulse) */
  update(time: number): void;
  /** Highlight a specific node (selection). Pass null to clear. */
  highlight(nodeId: string | null): void;
  /** Update health status for a single node */
  updateHealth(nodeId: string, health: GovernanceHealthStatus): void;
  /** Dispose all GPU resources */
  dispose(): void;
}

// ---------------------------------------------------------------------------
// Internal per-type batch
// ---------------------------------------------------------------------------

interface TypeBatch {
  type: GovernanceNodeType;
  mesh: THREE.InstancedMesh;
  geometry: THREE.BufferGeometry;
  material: THREE.MeshPhysicalMaterial;
  /** Ordered node IDs for this batch (index matches instance index) */
  nodeIds: string[];
  /** Per-instance health status for animation */
  healthStatuses: GovernanceHealthStatus[];
  /** Per-instance phase offset for desynchronized animation */
  phaseOffsets: number[];
  /** Per-instance base color (from node.color) */
  baseColors: THREE.Color[];
}

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------

/**
 * Create an InstancedMesh-based renderer for governance graph nodes.
 *
 * One InstancedMesh per GovernanceNodeType, using the same geometry factories
 * and MeshPhysicalMaterial definitions as ForceRadiant's createNodeObject.
 *
 * @param scene - THREE.Scene to add meshes to
 * @param nodes - Initial node list (used to allocate instance counts)
 * @returns Handle for position sync, animation, highlight, and disposal
 */
export function createNodeInstancer(
  scene: THREE.Scene,
  nodes: InstancerNode[],
): NodeInstancerHandle {

  // ── Group nodes by type ──
  const nodesByType = new Map<GovernanceNodeType, InstancerNode[]>();
  for (const t of ALL_TYPES) nodesByType.set(t, []);
  for (const n of nodes) {
    const bucket = nodesByType.get(n.type);
    if (bucket) bucket.push(n);
  }

  // ── Node ID -> batch + instance index lookup ──
  const nodeIndex = new Map<string, { batch: TypeBatch; instanceIndex: number }>();

  // ── Reusable dummy for matrix composition (zero-alloc in hot path) ──
  const _dummy = new THREE.Object3D();
  const _color = new THREE.Color();
  const _highlightColor = new THREE.Color(0xffffff);

  // ── Build one InstancedMesh per type ──
  const batches: TypeBatch[] = [];

  for (const nodeType of ALL_TYPES) {
    const bucket = nodesByType.get(nodeType)!;
    if (bucket.length === 0) continue;

    // Use unit-radius geometry; scale per-instance via matrix
    const baseRadius = 1.0;
    const geometry = TYPE_GEOMETRY[nodeType](baseRadius);
    if (!geometry.attributes.normal) geometry.computeVertexNormals();

    // Shared material from CrystalNodeMaterials cache (not cloned per instance)
    const material = getNodeMaterial(nodeType);

    const mesh = new THREE.InstancedMesh(geometry, material, bucket.length);
    mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);

    // Enable per-instance color
    mesh.instanceColor = new THREE.InstancedBufferAttribute(
      new Float32Array(bucket.length * 3), 3,
    );
    mesh.instanceColor.setUsage(THREE.DynamicDrawUsage);

    mesh.frustumCulled = false; // force-graph moves nodes; let the GPU clip
    mesh.name = `instanced-nodes-${nodeType}`;

    const batch: TypeBatch = {
      type: nodeType,
      mesh,
      geometry,
      material,
      nodeIds: [],
      healthStatuses: [],
      phaseOffsets: [],
      baseColors: [],
    };

    for (let i = 0; i < bucket.length; i++) {
      const n = bucket[i];
      const hs = n.healthStatus ?? 'unknown';
      const prom = HEALTH_PROMINENCE[hs];

      // Compute radius matching ForceRadiant: sqrt(TYPE_SIZE) * 0.8 * sizeMult * 0.5
      const radius = Math.pow(TYPE_SIZE[n.type] ?? 5, 0.5) * 0.8 * prom.sizeMult * 0.5;

      // Initial matrix: position at origin, scaled to radius
      _dummy.position.set(n.x ?? 0, n.y ?? 0, n.z ?? 0);
      _dummy.rotation.set(0, 0, 0);
      _dummy.scale.setScalar(radius);
      _dummy.updateMatrix();
      mesh.setMatrixAt(i, _dummy.matrix);

      // Initial color from node
      _color.set(n.color);
      mesh.setColorAt(i, _color);

      batch.nodeIds.push(n.id);
      batch.healthStatuses.push(hs);
      batch.phaseOffsets.push(Math.random() * Math.PI * 2);
      batch.baseColors.push(new THREE.Color(n.color));

      nodeIndex.set(n.id, { batch, instanceIndex: i });
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;

    scene.add(mesh);
    batches.push(batch);
  }

  // ── Highlight state ──
  let highlightedNodeId: string | null = null;

  // ── Handle implementation ──

  function syncPositions(updatedNodes: InstancerNode[]): void {
    for (const n of updatedNodes) {
      const entry = nodeIndex.get(n.id);
      if (!entry) continue;

      const { batch, instanceIndex } = entry;
      const hs = n.healthStatus ?? 'unknown';
      const prom = HEALTH_PROMINENCE[hs];
      const radius = Math.pow(TYPE_SIZE[n.type] ?? 5, 0.5) * 0.8 * prom.sizeMult * 0.5;

      _dummy.position.set(n.x ?? 0, n.y ?? 0, n.z ?? 0);
      _dummy.rotation.set(0, 0, 0);
      _dummy.scale.setScalar(radius);
      _dummy.updateMatrix();
      batch.mesh.setMatrixAt(instanceIndex, _dummy.matrix);

      // Update cached health
      batch.healthStatuses[instanceIndex] = hs;
    }

    // Flag all batches for GPU upload
    for (const b of batches) {
      b.mesh.instanceMatrix.needsUpdate = true;
    }
  }

  function update(time: number): void {
    for (const batch of batches) {
      const count = batch.nodeIds.length;
      let matrixDirty = false;
      let colorDirty = false;

      for (let i = 0; i < count; i++) {
        const hs = batch.healthStatuses[i];
        const prom = HEALTH_PROMINENCE[hs];
        const phase = batch.phaseOffsets[i];

        // ── Spin animation ──
        if (prom.spinSpeed > 0) {
          // Read current matrix to preserve position + scale
          batch.mesh.getMatrixAt(i, _dummy.matrix);
          _dummy.matrix.decompose(_dummy.position, _dummy.quaternion, _dummy.scale);

          if (hs === 'contradictory') {
            _dummy.rotation.y = time * prom.spinSpeed * Math.sin(time * 0.7 + phase);
            _dummy.rotation.x = Math.sin(time * 1.3 + phase) * 0.3;
          } else {
            _dummy.rotation.set(0, time * prom.spinSpeed, 0);
          }

          _dummy.updateMatrix();
          batch.mesh.setMatrixAt(i, _dummy.matrix);
          matrixDirty = true;
        }

        // ── Health glow pulse (error/warning nodes pulse emissive color) ──
        if (hs === 'error' || hs === 'warning' || hs === 'contradictory') {
          const pulse = 0.7 + 0.3 * Math.sin(time * 3.0 + phase);
          const base = batch.baseColors[i];
          _color.copy(base).multiplyScalar(pulse * prom.glowIntensity);

          // Highlight override
          if (highlightedNodeId === batch.nodeIds[i]) {
            _color.lerp(_highlightColor, 0.4);
          }

          batch.mesh.setColorAt(i, _color);
          colorDirty = true;
        } else if (highlightedNodeId === batch.nodeIds[i]) {
          // Non-pulsing but highlighted
          _color.copy(batch.baseColors[i]).lerp(_highlightColor, 0.4);
          batch.mesh.setColorAt(i, _color);
          colorDirty = true;
        }
      }

      if (matrixDirty) batch.mesh.instanceMatrix.needsUpdate = true;
      if (colorDirty && batch.mesh.instanceColor) {
        batch.mesh.instanceColor.needsUpdate = true;
      }
    }
  }

  function highlight(nodeId: string | null): void {
    // Reset previously highlighted node to base color
    if (highlightedNodeId) {
      const prev = nodeIndex.get(highlightedNodeId);
      if (prev) {
        const { batch, instanceIndex } = prev;
        batch.mesh.setColorAt(instanceIndex, batch.baseColors[instanceIndex]);
        if (batch.mesh.instanceColor) batch.mesh.instanceColor.needsUpdate = true;
      }
    }

    highlightedNodeId = nodeId;

    // Apply highlight to new node
    if (nodeId) {
      const entry = nodeIndex.get(nodeId);
      if (entry) {
        const { batch, instanceIndex } = entry;
        _color.copy(batch.baseColors[instanceIndex]).lerp(_highlightColor, 0.4);
        batch.mesh.setColorAt(instanceIndex, _color);
        if (batch.mesh.instanceColor) batch.mesh.instanceColor.needsUpdate = true;
      }
    }
  }

  function updateHealth(nodeId: string, health: GovernanceHealthStatus): void {
    const entry = nodeIndex.get(nodeId);
    if (!entry) return;

    const { batch, instanceIndex } = entry;
    batch.healthStatuses[instanceIndex] = health;

    // Recompute scale for new health prominence
    const prom = HEALTH_PROMINENCE[health];
    const nodeType = batch.type;
    const radius = Math.pow(TYPE_SIZE[nodeType] ?? 5, 0.5) * 0.8 * prom.sizeMult * 0.5;

    batch.mesh.getMatrixAt(instanceIndex, _dummy.matrix);
    _dummy.matrix.decompose(_dummy.position, _dummy.quaternion, _dummy.scale);
    _dummy.scale.setScalar(radius);
    _dummy.updateMatrix();
    batch.mesh.setMatrixAt(instanceIndex, _dummy.matrix);
    batch.mesh.instanceMatrix.needsUpdate = true;
  }

  function dispose(): void {
    for (const batch of batches) {
      scene.remove(batch.mesh);
      batch.geometry.dispose();
      // Don't dispose material — it's from the shared cache in CrystalNodeMaterials
      batch.mesh.dispose();
    }
    batches.length = 0;
    nodeIndex.clear();
  }

  return { syncPositions, update, highlight, updateHealth, dispose };
}
