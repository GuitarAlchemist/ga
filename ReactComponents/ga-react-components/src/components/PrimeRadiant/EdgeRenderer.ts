// src/components/PrimeRadiant/EdgeRenderer.ts
// Renders relationships between governance nodes as various line/particle styles

import * as THREE from 'three';
import type { GovernanceEdge, GovernanceEdgeType, SceneNode } from './types';

// ---------------------------------------------------------------------------
// Edge style configuration
// ---------------------------------------------------------------------------
interface EdgeStyle {
  color: THREE.Color;
  lineWidth: number;      // logical width (actual rendering uses tube or line)
  dashed: boolean;
  dashScale?: number;
  dashSize?: number;
  gapSize?: number;
  particleCount: number;   // 0 = no particles
  particleSpeed: number;
  opacity: number;
}

const EDGE_STYLES: Record<GovernanceEdgeType, EdgeStyle> = {
  'constitutional-hierarchy': {
    color: new THREE.Color('#FFD700'),
    lineWidth: 3,
    dashed: false,
    particleCount: 0,
    particleSpeed: 0,
    opacity: 0.7,
  },
  'policy-persona': {
    color: new THREE.Color('#8b949e'),
    lineWidth: 1,
    dashed: false,
    particleCount: 0,
    particleSpeed: 0,
    opacity: 0.4,
  },
  'pipeline-flow': {
    color: new THREE.Color('#58A6FF'),
    lineWidth: 1.5,
    dashed: false,
    particleCount: 8,
    particleSpeed: 1.0,
    opacity: 0.5,
  },
  'cross-repo': {
    color: new THREE.Color('#56B6C2'),
    lineWidth: 1,
    dashed: true,
    dashScale: 1,
    dashSize: 0.5,
    gapSize: 0.3,
    particleCount: 0,
    particleSpeed: 0,
    opacity: 0.35,
  },
  'lolli': {
    color: new THREE.Color('#E06C75'),
    lineWidth: 1.5,
    dashed: false,
    particleCount: 4,
    particleSpeed: 0.5,
    opacity: 0.6,
  },
};

// ---------------------------------------------------------------------------
// Build a curve for an edge (arc for cross-repo, straight for others)
// ---------------------------------------------------------------------------
function buildEdgeCurve(
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
  edgeType: GovernanceEdgeType,
): THREE.Curve<THREE.Vector3> {
  if (edgeType === 'cross-repo') {
    const mid = new THREE.Vector3()
      .addVectors(sourcePos, targetPos)
      .multiplyScalar(0.5);
    const dist = sourcePos.distanceTo(targetPos);
    mid.y += dist * 0.3;
    return new THREE.QuadraticBezierCurve3(sourcePos.clone(), mid, targetPos.clone());
  }
  return new THREE.LineCurve3(sourcePos.clone(), targetPos.clone());
}

// ---------------------------------------------------------------------------
// Tube radius per edge type (constitutional-hierarchy gets thick golden beams)
// ---------------------------------------------------------------------------
const TUBE_RADIUS: Record<GovernanceEdgeType, number> = {
  'constitutional-hierarchy': 0.08,
  'policy-persona': 0.025,
  'pipeline-flow': 0.04,
  'cross-repo': 0.025,
  'lolli': 0.04,
};

// ---------------------------------------------------------------------------
// Create edge — TubeGeometry for depth, fallback to Line for dashed
// ---------------------------------------------------------------------------
export function createEdgeLine(
  edge: GovernanceEdge,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
): THREE.Object3D {
  const style = EDGE_STYLES[edge.type];
  const curve = buildEdgeCurve(sourcePos, targetPos, edge.type);

  // Dashed edges still use Line (tubes can't dash)
  if (style.dashed) {
    const points = curve.getPoints(20);
    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    const material = new THREE.LineDashedMaterial({
      color: style.color,
      transparent: true,
      opacity: style.opacity * (edge.weight ?? 1.0),
      dashSize: style.dashSize ?? 0.5,
      gapSize: style.gapSize ?? 0.3,
      depthWrite: false,
    });
    const line = new THREE.Line(geometry, material);
    line.computeLineDistances();
    line.userData = { edgeId: edge.id, edgeType: edge.type, isTube: false };
    line.name = `edge-${edge.id}`;
    line.renderOrder = -1;
    return line;
  }

  // Solid edges use TubeGeometry for real 3D depth
  const segments = edge.type === 'cross-repo' ? 24 : 8;
  const radius = TUBE_RADIUS[edge.type] ?? 0.03;
  const tubeGeo = new THREE.TubeGeometry(curve, segments, radius, 6, false);
  const tubeMat = new THREE.MeshStandardMaterial({
    color: style.color,
    emissive: style.color,
    emissiveIntensity: 0.3,
    metalness: 0.2,
    roughness: 0.6,
    transparent: true,
    opacity: style.opacity * (edge.weight ?? 1.0),
    depthWrite: false,
  });

  const mesh = new THREE.Mesh(tubeGeo, tubeMat);
  mesh.userData = { edgeId: edge.id, edgeType: edge.type, isTube: true };
  mesh.name = `edge-${edge.id}`;
  mesh.renderOrder = -1;

  return mesh;
}

// ---------------------------------------------------------------------------
// Update edge positions when nodes move (handles both Tube and Line)
// ---------------------------------------------------------------------------
export function updateEdgeLine(
  obj: THREE.Object3D,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
  edgeType: GovernanceEdgeType,
): void {
  const isTube = obj.userData.isTube === true;

  if (isTube) {
    // Rebuild tube geometry from new curve
    const mesh = obj as THREE.Mesh;
    const oldGeo = mesh.geometry;
    const curve = buildEdgeCurve(sourcePos, targetPos, edgeType);
    const segments = edgeType === 'cross-repo' ? 24 : 8;
    const radius = TUBE_RADIUS[edgeType] ?? 0.03;
    mesh.geometry = new THREE.TubeGeometry(curve, segments, radius, 6, false);
    oldGeo.dispose();
  } else {
    // Line-based edge
    const line = obj as THREE.Line;
    const geometry = line.geometry;

    if (edgeType === 'cross-repo') {
      const mid = new THREE.Vector3()
        .addVectors(sourcePos, targetPos)
        .multiplyScalar(0.5);
      const dist = sourcePos.distanceTo(targetPos);
      mid.y += dist * 0.3;
      const curve = new THREE.QuadraticBezierCurve3(sourcePos.clone(), mid, targetPos.clone());
      const points = curve.getPoints(20);
      geometry.setFromPoints(points);
    } else {
      geometry.setFromPoints([sourcePos.clone(), targetPos.clone()]);
    }

    line.computeLineDistances();
    geometry.computeBoundingSphere();
  }
}

// ---------------------------------------------------------------------------
// Particle stream for pipeline-flow and lolli edges
// ---------------------------------------------------------------------------
export interface EdgeParticleStream {
  edge: GovernanceEdge;
  points: THREE.Points;
  offsets: Float32Array;
  speed: number;
}

export function createEdgeParticles(
  edge: GovernanceEdge,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
): EdgeParticleStream | null {
  const style = EDGE_STYLES[edge.type];
  if (style.particleCount === 0) return null;

  const count = style.particleCount;
  const positions = new Float32Array(count * 3);
  const offsets = new Float32Array(count);

  // Spread particles evenly along the edge
  for (let i = 0; i < count; i++) {
    offsets[i] = i / count;
    const t = offsets[i];
    positions[i * 3] = THREE.MathUtils.lerp(sourcePos.x, targetPos.x, t);
    positions[i * 3 + 1] = THREE.MathUtils.lerp(sourcePos.y, targetPos.y, t);
    positions[i * 3 + 2] = THREE.MathUtils.lerp(sourcePos.z, targetPos.z, t);
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

  const material = new THREE.PointsMaterial({
    color: style.color,
    size: edge.type === 'lolli' ? 0.2 : 0.15,
    transparent: true,
    opacity: 0.8,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });

  const points = new THREE.Points(geometry, material);
  points.userData = { edgeId: edge.id, isParticleStream: true };
  points.renderOrder = 1;

  return {
    edge,
    points,
    offsets,
    speed: style.particleSpeed,
  };
}

// ---------------------------------------------------------------------------
// Animate particle streams (call each frame)
// ---------------------------------------------------------------------------
export function animateEdgeParticles(
  stream: EdgeParticleStream,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
  dt: number,
): void {
  const pos = stream.points.geometry.attributes.position as THREE.BufferAttribute;
  const count = stream.offsets.length;

  for (let i = 0; i < count; i++) {
    stream.offsets[i] = (stream.offsets[i] + dt * stream.speed) % 1.0;
    const t = stream.offsets[i];

    if (stream.edge.type === 'cross-repo') {
      // Follow arc
      const mid = new THREE.Vector3()
        .addVectors(sourcePos, targetPos)
        .multiplyScalar(0.5);
      const dist = sourcePos.distanceTo(targetPos);
      mid.y += dist * 0.3;

      const p = new THREE.Vector3();
      // Quadratic bezier: (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
      const t1 = 1 - t;
      p.x = t1 * t1 * sourcePos.x + 2 * t1 * t * mid.x + t * t * targetPos.x;
      p.y = t1 * t1 * sourcePos.y + 2 * t1 * t * mid.y + t * t * targetPos.y;
      p.z = t1 * t1 * sourcePos.z + 2 * t1 * t * mid.z + t * t * targetPos.z;

      pos.setXYZ(i, p.x, p.y, p.z);
    } else {
      pos.setXYZ(
        i,
        THREE.MathUtils.lerp(sourcePos.x, targetPos.x, t),
        THREE.MathUtils.lerp(sourcePos.y, targetPos.y, t),
        THREE.MathUtils.lerp(sourcePos.z, targetPos.z, t),
      );
    }
  }

  pos.needsUpdate = true;
}

// ---------------------------------------------------------------------------
// Highlight / dim edges (works with both Line and Mesh/Tube)
// ---------------------------------------------------------------------------
export function setEdgeHighlight(obj: THREE.Object3D, highlighted: boolean): void {
  const mat = (obj as THREE.Mesh | THREE.Line).material as THREE.Material & { opacity: number };
  mat.opacity = highlighted ? 0.9 : (mat as unknown as { _baseOpacity?: number })._baseOpacity ?? 0.4;
}

export function setEdgeDimmed(obj: THREE.Object3D, dimmed: boolean): void {
  const mat = (obj as THREE.Mesh | THREE.Line).material as THREE.Material & { opacity: number };
  if (dimmed) {
    (mat as unknown as Record<string, number>)._baseOpacity = mat.opacity;
    mat.opacity = 0.05;
  } else {
    mat.opacity = (mat as unknown as Record<string, number>)._baseOpacity ?? 0.4;
  }
}
