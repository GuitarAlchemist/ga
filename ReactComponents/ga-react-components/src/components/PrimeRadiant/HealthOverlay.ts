// src/components/PrimeRadiant/HealthOverlay.ts
// Resilience score visualization — pulsing aura and health indicators

import * as THREE from 'three';
import { HEALTH_COLORS } from './types';
import { getHealthStatus } from './DataLoader';
import type { HealthMetrics } from './types';

// ---------------------------------------------------------------------------
// Global health aura — a large transparent sphere surrounding the scene
// ---------------------------------------------------------------------------
export function createHealthAura(health: HealthMetrics): THREE.Mesh {
  const geometry = new THREE.SphereGeometry(60, 64, 64);
  const status = getHealthStatus(health.resilienceScore);
  const color = new THREE.Color(HEALTH_COLORS[status]);

  const material = new THREE.MeshBasicMaterial({
    color,
    transparent: true,
    opacity: 0.03,
    side: THREE.BackSide,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.name = 'health-aura';
  mesh.userData = { isHealthAura: true };
  return mesh;
}

// ---------------------------------------------------------------------------
// Animate health aura (breathing pulse)
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

  // Pulse speed varies by health: healthy=slow, freeze=fast
  const pulseSpeed = status === 'healthy' ? 0.5 : status === 'watch' ? 1.0 : 2.0;
  const pulse = Math.sin(time * pulseSpeed) * 0.5 + 0.5;
  mat.opacity = 0.02 + pulse * 0.03;

  // Slight scale breathing
  const scale = 1.0 + pulse * 0.02;
  aura.scale.setScalar(scale);
}

// ---------------------------------------------------------------------------
// Starfield background particles
// ---------------------------------------------------------------------------
export function createStarfield(count: number = 2000): THREE.Points {
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);
  const sizes = new Float32Array(count);

  for (let i = 0; i < count; i++) {
    // Distribute in a large sphere
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    const r = 80 + Math.random() * 120;

    positions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    positions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
    positions[i * 3 + 2] = r * Math.cos(phi);

    // Subtle blue-white tints
    const brightness = 0.3 + Math.random() * 0.7;
    colors[i * 3] = brightness * (0.8 + Math.random() * 0.2);
    colors[i * 3 + 1] = brightness * (0.8 + Math.random() * 0.2);
    colors[i * 3 + 2] = brightness;

    sizes[i] = 0.05 + Math.random() * 0.15;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  geometry.setAttribute('size', new THREE.BufferAttribute(sizes, 1));

  const material = new THREE.PointsMaterial({
    size: 0.1,
    vertexColors: true,
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  const points = new THREE.Points(geometry, material);
  points.name = 'starfield';
  points.userData = { isStarfield: true };
  return points;
}

// ---------------------------------------------------------------------------
// Animate starfield (slow rotation)
// ---------------------------------------------------------------------------
export function animateStarfield(starfield: THREE.Points, dt: number): void {
  starfield.rotation.y += dt * 0.005;
  starfield.rotation.x += dt * 0.002;
}

// ---------------------------------------------------------------------------
// Markov prediction cloud — probability distribution visualization
// ---------------------------------------------------------------------------
export function createMarkovCloud(
  position: THREE.Vector3,
  probabilities: number[],
): THREE.Group {
  const group = new THREE.Group();
  group.position.copy(position);
  group.name = 'markov-cloud';

  const stateColors = [
    new THREE.Color('#4CB050'), // healthy
    new THREE.Color('#E5C07B'), // watch
    new THREE.Color('#E06C75'), // freeze
    new THREE.Color('#8b949e'), // unknown
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
