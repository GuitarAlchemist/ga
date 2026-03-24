// src/components/PrimeRadiant/EdgeRenderer.ts
// Foundation TV aesthetic — edges are FLOWING PARTICLES along curved paths
// No solid tubes or lines — everything is made of light

import * as THREE from 'three';
import type { GovernanceEdge, GovernanceEdgeType } from './types';

// ---------------------------------------------------------------------------
// Edge style: particle streams for ALL edge types
// ---------------------------------------------------------------------------
interface EdgeStyle {
  color: THREE.Color;
  particleCount: number;
  particleSize: number;
  speed: number;
  opacity: number;
  curveHeight: number; // arc height as fraction of distance
}

const EDGE_STYLES: Record<GovernanceEdgeType, EdgeStyle> = {
  'constitutional-hierarchy': {
    color: new THREE.Color('#FFD700'),
    particleCount: 24,
    particleSize: 0.12,
    speed: 0.4,
    opacity: 0.9,
    curveHeight: 0.15,
  },
  'policy-persona': {
    color: new THREE.Color('#FFA500'),
    particleCount: 10,
    particleSize: 0.06,
    speed: 0.6,
    opacity: 0.5,
    curveHeight: 0.1,
  },
  'pipeline-flow': {
    color: new THREE.Color('#00CED1'),
    particleCount: 16,
    particleSize: 0.08,
    speed: 1.2,
    opacity: 0.7,
    curveHeight: 0.2,
  },
  'cross-repo': {
    color: new THREE.Color('#008B8B'),
    particleCount: 12,
    particleSize: 0.07,
    speed: 0.8,
    opacity: 0.5,
    curveHeight: 0.35,
  },
  'lolli': {
    color: new THREE.Color('#FF4444'),
    particleCount: 8,
    particleSize: 0.1,
    speed: 0.3,
    opacity: 0.8,
    curveHeight: 0.1,
  },
};

// ---------------------------------------------------------------------------
// Build a CatmullRomCurve3 between two points with an arc
// ---------------------------------------------------------------------------
function buildEdgeCurve(
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
  curveHeight: number,
): THREE.CatmullRomCurve3 {
  const mid = new THREE.Vector3()
    .addVectors(sourcePos, targetPos)
    .multiplyScalar(0.5);
  const dist = sourcePos.distanceTo(targetPos);
  mid.y += dist * curveHeight;

  // Create a smooth curve with 4 control points
  const q1 = new THREE.Vector3().lerpVectors(sourcePos, mid, 0.5);
  const q2 = new THREE.Vector3().lerpVectors(mid, targetPos, 0.5);

  return new THREE.CatmullRomCurve3([
    sourcePos.clone(),
    q1,
    mid,
    q2,
    targetPos.clone(),
  ]);
}

// ---------------------------------------------------------------------------
// Edge particle stream — the main edge representation
// Each edge is a set of particles flowing along a curved path
// ---------------------------------------------------------------------------
export interface EdgeParticleStream {
  edge: GovernanceEdge;
  points: THREE.Points;
  offsets: Float32Array;
  speed: number;
  curve: THREE.CatmullRomCurve3;
}

// ---------------------------------------------------------------------------
// Create edge as flowing particle stream (replaces createEdgeLine)
// Returns the particle stream directly — no separate line needed
// ---------------------------------------------------------------------------
export function createEdgeLine(
  edge: GovernanceEdge,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
): THREE.Points {
  const style = EDGE_STYLES[edge.type];
  const count = style.particleCount;

  const curve = buildEdgeCurve(sourcePos, targetPos, style.curveHeight);
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);

  for (let i = 0; i < count; i++) {
    const t = i / count;
    const p = curve.getPoint(t);
    positions[i * 3] = p.x;
    positions[i * 3 + 1] = p.y;
    positions[i * 3 + 2] = p.z;

    // Slight color variation along the stream
    const brightness = 0.7 + (Math.sin(t * Math.PI) * 0.3);
    colors[i * 3] = style.color.r * brightness;
    colors[i * 3 + 1] = style.color.g * brightness;
    colors[i * 3 + 2] = style.color.b * brightness;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const material = new THREE.PointsMaterial({
    size: style.particleSize,
    vertexColors: true,
    transparent: true,
    opacity: style.opacity,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  const points = new THREE.Points(geometry, material);
  points.userData = { edgeId: edge.id, edgeType: edge.type };
  points.name = `edge-${edge.id}`;
  points.renderOrder = -1;

  return points;
}

// ---------------------------------------------------------------------------
// Create edge particle stream for animation
// ---------------------------------------------------------------------------
export function createEdgeParticles(
  edge: GovernanceEdge,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
): EdgeParticleStream | null {
  const style = EDGE_STYLES[edge.type];
  const count = style.particleCount;
  const curve = buildEdgeCurve(sourcePos, targetPos, style.curveHeight);

  const offsets = new Float32Array(count);
  for (let i = 0; i < count; i++) {
    offsets[i] = i / count;
  }

  // The points object is the same one created in createEdgeLine
  // We need a reference to it for animation, so the caller links them
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);

  for (let i = 0; i < count; i++) {
    const t = offsets[i];
    const p = curve.getPoint(t);
    positions[i * 3] = p.x;
    positions[i * 3 + 1] = p.y;
    positions[i * 3 + 2] = p.z;

    const brightness = 0.7 + Math.sin(t * Math.PI) * 0.3;
    colors[i * 3] = style.color.r * brightness;
    colors[i * 3 + 1] = style.color.g * brightness;
    colors[i * 3 + 2] = style.color.b * brightness;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const material = new THREE.PointsMaterial({
    size: style.particleSize,
    vertexColors: true,
    transparent: true,
    opacity: style.opacity,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  const points = new THREE.Points(geometry, material);
  points.userData = { edgeId: edge.id, isParticleStream: true };
  points.renderOrder = 1;

  return {
    edge,
    points,
    offsets,
    speed: style.speed,
    curve,
  };
}

// ---------------------------------------------------------------------------
// Animate particles flowing along the curve
// ---------------------------------------------------------------------------
export function animateEdgeParticles(
  stream: EdgeParticleStream,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
  dt: number,
): void {
  // Rebuild curve for current node positions
  const style = EDGE_STYLES[stream.edge.type];
  stream.curve = buildEdgeCurve(sourcePos, targetPos, style.curveHeight);

  const pos = stream.points.geometry.attributes.position as THREE.BufferAttribute;
  const count = stream.offsets.length;

  for (let i = 0; i < count; i++) {
    stream.offsets[i] = (stream.offsets[i] + dt * stream.speed) % 1.0;
    const t = stream.offsets[i];
    const p = stream.curve.getPoint(t);
    pos.setXYZ(i, p.x, p.y, p.z);
  }
  pos.needsUpdate = true;
}

// ---------------------------------------------------------------------------
// Update edge (for after force layout — rebuild positions on the curve)
// ---------------------------------------------------------------------------
export function updateEdgeLine(
  obj: THREE.Object3D,
  sourcePos: THREE.Vector3,
  targetPos: THREE.Vector3,
  edgeType: GovernanceEdgeType,
): void {
  if (!(obj instanceof THREE.Points)) return;
  const style = EDGE_STYLES[edgeType];
  const curve = buildEdgeCurve(sourcePos, targetPos, style.curveHeight);

  const pos = obj.geometry.attributes.position as THREE.BufferAttribute;
  const count = pos.count;

  for (let i = 0; i < count; i++) {
    const t = i / count;
    const p = curve.getPoint(t);
    pos.setXYZ(i, p.x, p.y, p.z);
  }
  pos.needsUpdate = true;
}

// ---------------------------------------------------------------------------
// Highlight / dim edges
// ---------------------------------------------------------------------------
export function setEdgeHighlight(obj: THREE.Object3D, highlighted: boolean): void {
  if (!(obj instanceof THREE.Points)) return;
  const mat = obj.material as THREE.PointsMaterial;
  mat.opacity = highlighted ? 1.0 : 0.5;
  mat.size = highlighted ? mat.size * 1.3 : mat.size;
}

export function setEdgeDimmed(obj: THREE.Object3D, dimmed: boolean): void {
  if (!(obj instanceof THREE.Points)) return;
  const mat = obj.material as THREE.PointsMaterial;
  if (dimmed) {
    (mat as unknown as Record<string, number>)._baseOpacity = mat.opacity;
    mat.opacity = 0.03;
  } else {
    mat.opacity = (mat as unknown as Record<string, number>)._baseOpacity ?? 0.5;
  }
}
