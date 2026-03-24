// src/components/PrimeRadiant/NodeRenderer.ts
// Foundation TV aesthetic — each node is a CLUSTER of golden particles, not a solid mesh

import * as THREE from 'three';
import type { GovernanceNode, GovernanceNodeType } from './types';
import { NODE_SCALES, NODE_PARTICLE_BASE } from './types';

// ---------------------------------------------------------------------------
// Particle cluster for a single governance node
// Uses THREE.Points with additive blending for holographic look
// ---------------------------------------------------------------------------

function particleCount(type: GovernanceNodeType): number {
  return Math.floor(NODE_PARTICLE_BASE * NODE_SCALES[type]);
}

function clusterRadius(type: GovernanceNodeType): number {
  return NODE_SCALES[type] * 1.2;
}

// ---------------------------------------------------------------------------
// Create node as a particle cluster (THREE.Points wrapped in a Group)
// The Group carries userData for raycasting; an invisible sphere inside
// handles the actual raycasting hit-test.
// ---------------------------------------------------------------------------
export function createNodeMesh(node: GovernanceNode): THREE.Group {
  const group = new THREE.Group();
  group.userData = { nodeId: node.id, nodeType: node.type };
  group.name = `node-${node.id}`;

  const count = particleCount(node.type);
  const radius = clusterRadius(node.type);
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);
  const sizes = new Float32Array(count);
  const phases = new Float32Array(count); // for Brownian drift

  const baseColor = new THREE.Color(node.color);

  // Unhealthy nodes get red tint
  const isUnhealthy = node.health && node.health.lolliCount > 0;
  const unhealthyColor = new THREE.Color('#FF4444');

  for (let i = 0; i < count; i++) {
    // Distribute in a sphere with slight concentration at center
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    const r = radius * Math.pow(Math.random(), 0.6); // bias toward center

    positions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    positions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
    positions[i * 3 + 2] = r * Math.cos(phi);

    // Color variation — gold with slight randomness
    const variation = 0.7 + Math.random() * 0.3;
    const c = isUnhealthy
      ? baseColor.clone().lerp(unhealthyColor, 0.4 + Math.random() * 0.3)
      : baseColor.clone();
    colors[i * 3] = c.r * variation;
    colors[i * 3 + 1] = c.g * variation;
    colors[i * 3 + 2] = c.b * variation;

    sizes[i] = 0.08 + Math.random() * 0.12;
    phases[i] = Math.random() * Math.PI * 2;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  geometry.setAttribute('size', new THREE.BufferAttribute(sizes, 1));
  geometry.setAttribute('phase', new THREE.BufferAttribute(phases, 1));

  const material = new THREE.PointsMaterial({
    size: 0.12,
    vertexColors: true,
    transparent: true,
    opacity: 0.85,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  const points = new THREE.Points(geometry, material);
  group.add(points);

  // Invisible sphere for raycasting
  const hitGeo = new THREE.SphereGeometry(radius * 1.2, 8, 8);
  const hitMat = new THREE.MeshBasicMaterial({
    visible: false,
    transparent: true,
    opacity: 0,
  });
  const hitSphere = new THREE.Mesh(hitGeo, hitMat);
  hitSphere.userData = { nodeId: node.id, nodeType: node.type };
  group.add(hitSphere);

  return group;
}

// ---------------------------------------------------------------------------
// Text sprite — canvas-based label that floats near a node
// Returns sprite or null for very small node types
// ---------------------------------------------------------------------------
export function createTextSprite(text: string, color: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d')!;

  const fontSize = 48;
  canvas.width = 512;
  canvas.height = 64;

  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.font = `${fontSize}px "JetBrains Mono", "Fira Code", monospace`;
  ctx.fillStyle = color;
  ctx.shadowColor = color;
  ctx.shadowBlur = 8;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, canvas.width / 2, canvas.height / 2);

  const texture = new THREE.CanvasTexture(canvas);
  texture.minFilter = THREE.LinearFilter;

  const material = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    opacity: 0,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    depthTest: false,
  });

  const sprite = new THREE.Sprite(material);
  sprite.scale.set(5, 0.6, 1);
  sprite.userData = { isTextLabel: true, targetOpacity: 0 };
  return sprite;
}

// ---------------------------------------------------------------------------
// Animate node particles — gentle Brownian drift
// ---------------------------------------------------------------------------
export function animateNodeParticles(group: THREE.Group, time: number, dt: number): void {
  const points = group.children[0] as THREE.Points;
  if (!points || !(points instanceof THREE.Points)) return;

  const pos = points.geometry.attributes.position as THREE.BufferAttribute;
  const phases = points.geometry.attributes.phase as THREE.BufferAttribute;
  if (!phases) return;

  const type = group.userData.nodeType as GovernanceNodeType;
  const radius = clusterRadius(type);
  const drift = 0.15;

  for (let i = 0; i < pos.count; i++) {
    const phase = phases.getX(i);
    const dx = Math.sin(time * 0.5 + phase) * drift * dt;
    const dy = Math.cos(time * 0.7 + phase * 1.3) * drift * dt;
    const dz = Math.sin(time * 0.3 + phase * 0.7) * drift * dt;

    let x = pos.getX(i) + dx;
    let y = pos.getY(i) + dy;
    let z = pos.getZ(i) + dz;

    // Keep within cluster radius
    const dist = Math.sqrt(x * x + y * y + z * z);
    if (dist > radius) {
      const scale = radius / dist;
      x *= scale;
      y *= scale;
      z *= scale;
    }

    pos.setXYZ(i, x, y, z);
  }
  pos.needsUpdate = true;
}

// ---------------------------------------------------------------------------
// Animate text labels — fade in/out based on camera distance
// ---------------------------------------------------------------------------
export function animateTextSprite(
  sprite: THREE.Sprite,
  cameraPosition: THREE.Vector3,
  fadeInDist: number,
  fadeOutDist: number,
): void {
  const dist = sprite.getWorldPosition(new THREE.Vector3()).distanceTo(cameraPosition);
  const mat = sprite.material as THREE.SpriteMaterial;

  if (dist < fadeInDist) {
    mat.opacity = Math.min(mat.opacity + 0.05, 0.9);
    const scaleT = Math.max(0.3, dist / fadeInDist);
    sprite.scale.set(5 * scaleT, 0.6 * scaleT, 1);
  } else if (dist > fadeOutDist) {
    mat.opacity = Math.max(mat.opacity - 0.05, 0);
  } else {
    const t = 1 - (dist - fadeInDist) / (fadeOutDist - fadeInDist);
    mat.opacity = t * 0.7;
  }
}

// ---------------------------------------------------------------------------
// Highlight / unhighlight — changes particle opacity
// ---------------------------------------------------------------------------
export function setNodeHighlight(obj: THREE.Object3D, highlighted: boolean): void {
  const group = obj as THREE.Group;
  for (const child of group.children) {
    if (child instanceof THREE.Points) {
      const mat = child.material as THREE.PointsMaterial;
      mat.opacity = highlighted ? 1.0 : 0.85;
      mat.size = highlighted ? 0.16 : 0.12;
    }
  }
}

export function setNodeDimmed(obj: THREE.Object3D, dimmed: boolean): void {
  const group = obj as THREE.Group;
  for (const child of group.children) {
    if (child instanceof THREE.Points) {
      const mat = child.material as THREE.PointsMaterial;
      mat.opacity = dimmed ? 0.08 : 0.85;
    }
  }
}

// ---------------------------------------------------------------------------
// LOLLI decay effect — red particles emanating from unhealthy nodes
// ---------------------------------------------------------------------------
export function createLolliParticles(position: THREE.Vector3): THREE.Points {
  const count = 30;
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);

  for (let i = 0; i < count; i++) {
    positions[i * 3] = (Math.random() - 0.5) * 3;
    positions[i * 3 + 1] = Math.random() * 4;
    positions[i * 3 + 2] = (Math.random() - 0.5) * 3;

    colors[i * 3] = 1.0;
    colors[i * 3 + 1] = 0.15 + Math.random() * 0.15;
    colors[i * 3 + 2] = 0.1 + Math.random() * 0.1;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const material = new THREE.PointsMaterial({
    size: 0.1,
    vertexColors: true,
    transparent: true,
    opacity: 0.8,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });

  const points = new THREE.Points(geometry, material);
  points.position.copy(position);
  points.userData = { isLolliDecay: true };
  return points;
}

export function animateLolliParticles(points: THREE.Points, dt: number): void {
  const pos = points.geometry.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < pos.count; i++) {
    let y = pos.getY(i);
    y += dt * 0.6;
    if (y > 4) y = 0;
    pos.setY(i, y);
  }
  pos.needsUpdate = true;
}

// No geometry cache needed — we use BufferGeometry per node now
export function createDepartmentParticles(_node: GovernanceNode): THREE.Points {
  // Department particles are now integrated into the main node cluster
  // Return empty points to maintain API compatibility
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(0), 3));
  return new THREE.Points(geo, new THREE.PointsMaterial({ visible: false }));
}

export function disposeNodeGeometries(): void {
  // No shared geometry cache to dispose in particle mode
}
