// src/components/PrimeRadiant/HealthOverlay.ts
// Foundation TV aesthetic — Fresnel containment sphere + golden health aura

import * as THREE from 'three';
import { HEALTH_COLORS, CONTAINMENT_SPHERE_RADIUS } from './types';
import { getHealthStatus } from './DataLoader';
import type { HealthMetrics } from './types';

// ---------------------------------------------------------------------------
// Containment grid — wireframe latitude/longitude rings (Foundation TV style)
// Thin teal lines forming a spherical cage, not a solid surface
// ---------------------------------------------------------------------------
export function createContainmentSphere(): THREE.Mesh {
  // Use a low-segment sphere with wireframe — gives clean lat/lon lines
  const geometry = new THREE.SphereGeometry(CONTAINMENT_SPHERE_RADIUS, 32, 16);

  const material = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#006666'),
    wireframe: true,
    transparent: true,
    opacity: 0.08,
    depthWrite: false,
    // Normal blending — no additive, so bloom doesn't blow it out
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.name = 'containment-sphere';
  mesh.userData = { isContainmentSphere: true };
  return mesh;
}

// ---------------------------------------------------------------------------
// Animate containment sphere (slow rotation + subtle breathing)
// ---------------------------------------------------------------------------
export function animateContainmentSphere(mesh: THREE.Mesh, time: number): void {
  mesh.rotation.y += 0.0008;
  mesh.rotation.x += 0.0002;

  // Subtle opacity breathing
  const mat = mesh.material as THREE.MeshBasicMaterial;
  const pulse = Math.sin(time * 0.3) * 0.5 + 0.5;
  mat.opacity = 0.06 + pulse * 0.04;
}

// ---------------------------------------------------------------------------
// Health aura — inner glow that reflects governance health
// ---------------------------------------------------------------------------
export function createHealthAura(health: HealthMetrics): THREE.Mesh {
  const geometry = new THREE.SphereGeometry(CONTAINMENT_SPHERE_RADIUS * 0.95, 64, 64);
  const status = getHealthStatus(health.resilienceScore);
  const color = new THREE.Color(HEALTH_COLORS[status]);

  const material = new THREE.MeshBasicMaterial({
    color,
    transparent: true,
    opacity: 0.012,
    side: THREE.BackSide,
    depthWrite: false,
    // Normal blending — additive was causing bloom white-out
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.name = 'health-aura';
  mesh.userData = { isHealthAura: true };
  return mesh;
}

// ---------------------------------------------------------------------------
// Animate health aura
// ---------------------------------------------------------------------------
export function animateHealthAura(
  aura: THREE.Mesh,
  time: number,
  health: HealthMetrics,
): void {
  const status = getHealthStatus(health.resilienceScore);
  const color = new THREE.Color(HEALTH_COLORS[status]);
  const mat = aura.material as THREE.MeshBasicMaterial;
  mat.color.copy(color);

  const pulseSpeed = status === 'healthy' ? 0.4 : status === 'watch' ? 1.0 : 2.0;
  const pulse = Math.sin(time * pulseSpeed) * 0.5 + 0.5;
  mat.opacity = 0.008 + pulse * 0.012;
}

// ---------------------------------------------------------------------------
// Starfield — sparse points for deep space background
// ---------------------------------------------------------------------------
export function createStarfield(count: number = 1500): THREE.Points {
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);

  for (let i = 0; i < count; i++) {
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    const r = 100 + Math.random() * 150;

    positions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    positions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
    positions[i * 3 + 2] = r * Math.cos(phi);

    // Dim blue-white stars
    const brightness = 0.2 + Math.random() * 0.4;
    colors[i * 3] = brightness * 0.9;
    colors[i * 3 + 1] = brightness * 0.95;
    colors[i * 3 + 2] = brightness;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const material = new THREE.PointsMaterial({
    size: 0.08,
    vertexColors: true,
    transparent: true,
    opacity: 0.5,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  const points = new THREE.Points(geometry, material);
  points.name = 'starfield';
  points.userData = { isStarfield: true };
  return points;
}

export function animateStarfield(starfield: THREE.Points, dt: number): void {
  starfield.rotation.y += dt * 0.003;
  starfield.rotation.x += dt * 0.001;
}

// Keep API compatible
export function createMarkovCloud(
  position: THREE.Vector3,
  probabilities: number[],
): THREE.Group {
  const group = new THREE.Group();
  group.position.copy(position);
  group.name = 'markov-cloud';

  const stateColors = [
    new THREE.Color('#FFD700'),
    new THREE.Color('#FFA500'),
    new THREE.Color('#FF4444'),
    new THREE.Color('#008B8B'),
  ];

  probabilities.forEach((prob, idx) => {
    if (prob < 0.01) return;
    const count = Math.floor(prob * 40);
    const positions = new Float32Array(count * 3);
    const color = stateColors[idx] ?? stateColors[3];

    for (let i = 0; i < count; i++) {
      const angle = (idx / probabilities.length) * Math.PI * 2 + (Math.random() - 0.5) * 0.5;
      const r = 2 + Math.random() * 3 * prob;
      positions[i * 3] = Math.cos(angle) * r;
      positions[i * 3 + 1] = (Math.random() - 0.5) * 2;
      positions[i * 3 + 2] = Math.sin(angle) * r;
    }

    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

    const material = new THREE.PointsMaterial({
      color,
      size: 0.08,
      transparent: true,
      opacity: 0.4 * prob + 0.1,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    group.add(new THREE.Points(geometry, material));
  });

  return group;
}
