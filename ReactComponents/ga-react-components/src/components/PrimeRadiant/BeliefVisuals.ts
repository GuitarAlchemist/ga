// src/components/PrimeRadiant/BeliefVisuals.ts
// Three visual systems for governance belief nodes:
//   1. Staleness Decay — corroded look as beliefs age without refresh
//   2. Confidence Glow — emissive intensity scales with belief confidence
//   3. Belief Constellations — faint constellation lines grouping related beliefs

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ConstellationGroup {
  nodeIds: string[];
  label: string;
  color: string;
}

export interface BeliefVisualsHandle {
  /** Call each frame to update constellation line positions and any time-based effects. */
  update: (time: number, nodePositions: Map<string, THREE.Vector3>) => void;
  /** Apply staleness decay and confidence glow to a single node mesh. */
  applyToNode: (nodeId: string, mesh: THREE.Object3D, staleness: number, confidence: number) => void;
  /** Set or replace constellation groupings. Rebuilds line geometry. */
  setConstellations: (groups: ConstellationGroup[]) => void;
  /** THREE.Group containing all constellation lines and labels. Add to your scene. */
  constellationGroup: THREE.Group;
  /** Dispose all GPU resources. */
  dispose: () => void;
}

// ---------------------------------------------------------------------------
// Internal types
// ---------------------------------------------------------------------------

interface ConstellationData {
  lineSegments: THREE.LineSegments;
  material: THREE.LineBasicMaterial;
  /** Pairs of node IDs — each consecutive pair defines one line segment. */
  nodePairs: [string, string][];
  label: THREE.Sprite | null;
  nodeIds: string[];
}

// ---------------------------------------------------------------------------
// Helpers (no allocations in hot path)
// ---------------------------------------------------------------------------

const _gray = new THREE.Color(0.35, 0.35, 0.35);
const _tmpColor = new THREE.Color();
const _tmpVec = new THREE.Vector3();

function clamp01(v: number): number {
  return v < 0 ? 0 : v > 1 ? 1 : v;
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

// ---------------------------------------------------------------------------
// 1. Staleness Decay
// ---------------------------------------------------------------------------

/**
 * Apply visual corrosion to a mesh based on staleness (0 = pristine, 1 = fully corroded).
 * Modifies MeshPhysicalMaterial properties in-place — no new objects created.
 */
export function applyStalenessDecay(mesh: THREE.Object3D, staleness: number): void {
  const s = clamp01(staleness);

  mesh.traverse((child) => {
    if (!(child instanceof THREE.Mesh)) return;
    const mat = child.material;
    if (!(mat instanceof THREE.MeshPhysicalMaterial)) return;

    // Store original values on first application
    const ud = mat.userData as Record<string, unknown>;
    if (ud._beliefOrigColor === undefined) {
      ud._beliefOrigColor = mat.color.getHex();
      ud._beliefOrigRoughness = mat.roughness;
      ud._beliefOrigEmissiveIntensity = mat.emissiveIntensity;
    }

    const origColor = ud._beliefOrigColor as number;
    const origRoughness = ud._beliefOrigRoughness as number;
    const origEmissive = ud._beliefOrigEmissiveIntensity as number;

    // Roughness: pristine → very rough
    mat.roughness = lerp(origRoughness, 0.95, s);

    // Color: lerp toward desaturated gray
    _tmpColor.setHex(origColor);
    _tmpColor.lerp(_gray, s);
    mat.color.copy(_tmpColor);

    // Emissive dims toward near-zero
    mat.emissiveIntensity = lerp(origEmissive, 0.02, s);
  });
}

// ---------------------------------------------------------------------------
// 2. Confidence Glow
// ---------------------------------------------------------------------------

/**
 * Scale emissive glow and opacity based on belief confidence (0 = dim, 1 = bright).
 * Also scales any child Sprite (halo) if present.
 */
export function applyConfidenceGlow(mesh: THREE.Object3D, confidence: number): void {
  const c = clamp01(confidence);

  mesh.traverse((child) => {
    if (child instanceof THREE.Mesh) {
      const mat = child.material;
      if (!(mat instanceof THREE.MeshPhysicalMaterial)) return;

      // Store original values on first application
      const ud = mat.userData as Record<string, unknown>;
      if (ud._beliefOrigOpacity === undefined) {
        ud._beliefOrigOpacity = mat.opacity;
        // Use the staleness-aware emissive if already stored, else current
        if (ud._beliefOrigEmissiveIntensity === undefined) {
          ud._beliefOrigEmissiveIntensity = mat.emissiveIntensity;
        }
      }

      // Emissive intensity: 0.02 at confidence 0, up to 0.5+ at confidence 1
      mat.emissiveIntensity = lerp(0.02, 0.55, c);

      // Opacity: 0.4 at confidence 0, 1.0 at confidence 1
      mat.opacity = lerp(0.4, 1.0, c);
      mat.transparent = mat.opacity < 1.0;
    }

    // Scale sprite halo if present
    if (child instanceof THREE.Sprite) {
      const haloScale = lerp(0.3, 1.0, c);
      child.scale.setScalar(haloScale);
      if (child.material instanceof THREE.SpriteMaterial) {
        child.material.opacity = lerp(0.1, 0.8, c);
        child.material.transparent = true;
      }
    }
  });
}

// ---------------------------------------------------------------------------
// 3. Belief Constellations
// ---------------------------------------------------------------------------

/**
 * Build line-segment pairs connecting nodes in a group using a minimal spanning approach.
 * For small groups (< 10), connect sequentially (chain). This avoids O(n^2) full-mesh lines.
 */
function buildNodePairs(nodeIds: string[]): [string, string][] {
  const pairs: [string, string][] = [];
  for (let i = 0; i < nodeIds.length - 1; i++) {
    pairs.push([nodeIds[i], nodeIds[i + 1]]);
  }
  return pairs;
}

/** Create a tiny text sprite for constellation label at centroid. */
function createLabelSprite(label: string, color: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 64;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    ctx.font = '24px sans-serif';
    ctx.fillStyle = color;
    ctx.globalAlpha = 0.5;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(label, 128, 32);
  }
  const texture = new THREE.CanvasTexture(canvas);
  texture.needsUpdate = true;
  const spriteMat = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    opacity: 0.4,
    depthWrite: false,
  });
  const sprite = new THREE.Sprite(spriteMat);
  sprite.scale.set(4, 1, 1);
  return sprite;
}

function buildConstellationData(group: ConstellationGroup): ConstellationData {
  const nodePairs = buildNodePairs(group.nodeIds);
  const segmentCount = nodePairs.length;

  // Pre-allocate position buffer: 2 vertices per segment, 3 floats each
  const positions = new Float32Array(segmentCount * 2 * 3);
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

  const material = new THREE.LineBasicMaterial({
    color: new THREE.Color(group.color),
    transparent: true,
    opacity: 0.15,
    depthWrite: false,
    linewidth: 1,
  });

  const lineSegments = new THREE.LineSegments(geometry, material);
  lineSegments.frustumCulled = false;

  const label = createLabelSprite(group.label, group.color);

  return {
    lineSegments,
    material,
    nodePairs,
    label,
    nodeIds: group.nodeIds,
  };
}

/** Update line segment positions from current node positions. Reuses the same Float32Array. */
function updateConstellationPositions(
  data: ConstellationData,
  nodePositions: Map<string, THREE.Vector3>,
): void {
  const posAttr = data.lineSegments.geometry.getAttribute('position') as THREE.BufferAttribute;
  const positions = posAttr.array as Float32Array;
  let idx = 0;
  let centroidX = 0, centroidY = 0, centroidZ = 0;
  let centroidCount = 0;

  for (const [idA, idB] of data.nodePairs) {
    const posA = nodePositions.get(idA);
    const posB = nodePositions.get(idB);
    if (posA && posB) {
      positions[idx] = posA.x;
      positions[idx + 1] = posA.y;
      positions[idx + 2] = posA.z;
      positions[idx + 3] = posB.x;
      positions[idx + 4] = posB.y;
      positions[idx + 5] = posB.z;
    }
    idx += 6;
  }

  posAttr.needsUpdate = true;

  // Update label position at centroid
  if (data.label) {
    for (const nodeId of data.nodeIds) {
      const pos = nodePositions.get(nodeId);
      if (pos) {
        centroidX += pos.x;
        centroidY += pos.y;
        centroidZ += pos.z;
        centroidCount++;
      }
    }
    if (centroidCount > 0) {
      data.label.position.set(
        centroidX / centroidCount,
        centroidY / centroidCount + 1.5,  // slight offset above centroid
        centroidZ / centroidCount,
      );
    }
  }
}

// ---------------------------------------------------------------------------
// Combined API — factory
// ---------------------------------------------------------------------------

export function createBeliefVisuals(): BeliefVisualsHandle {
  const constellationGroup = new THREE.Group();
  constellationGroup.name = 'belief-constellations';

  let constellations: ConstellationData[] = [];

  function setConstellations(groups: ConstellationGroup[]): void {
    // Dispose existing
    for (const c of constellations) {
      c.lineSegments.geometry.dispose();
      c.material.dispose();
      if (c.label) {
        const spriteMat = c.label.material as THREE.SpriteMaterial;
        spriteMat.map?.dispose();
        spriteMat.dispose();
      }
    }
    // Clear the group
    while (constellationGroup.children.length > 0) {
      constellationGroup.remove(constellationGroup.children[0]);
    }

    constellations = groups.map((g) => {
      const data = buildConstellationData(g);
      constellationGroup.add(data.lineSegments);
      if (data.label) constellationGroup.add(data.label);
      return data;
    });
  }

  function update(_time: number, nodePositions: Map<string, THREE.Vector3>): void {
    for (const c of constellations) {
      updateConstellationPositions(c, nodePositions);
    }
  }

  function applyToNode(
    _nodeId: string,
    mesh: THREE.Object3D,
    staleness: number,
    confidence: number,
  ): void {
    applyStalenessDecay(mesh, staleness);
    applyConfidenceGlow(mesh, confidence);
  }

  function dispose(): void {
    setConstellations([]);  // disposes all internal resources
  }

  return {
    update,
    applyToNode,
    setConstellations,
    constellationGroup,
    dispose,
  };
}
