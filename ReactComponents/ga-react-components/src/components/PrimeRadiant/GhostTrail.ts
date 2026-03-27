// src/components/PrimeRadiant/GhostTrail.ts
// Ghost Trail Predictions — translucent copies of node meshes fading along
// predicted trajectory, colored by Markov probability distribution.
// Toggleable via showPredictions flag in group.userData.

import * as THREE from 'three';
import type { HealthMetrics } from './types';

// ---------------------------------------------------------------------------
// Health state colors (matching types.ts HEALTH_COLORS / spec)
// ---------------------------------------------------------------------------
const STATE_COLORS = [
  new THREE.Color('#33CC66'), // Healthy
  new THREE.Color('#FFB300'), // Watch
  new THREE.Color('#FF4444'), // Warning
  new THREE.Color('#FF4444'), // Freeze
];

const GHOST_COUNT = 4;
const GHOST_OPACITIES = [0.5, 0.35, 0.2, 0.1];

// ---------------------------------------------------------------------------
// Interpolate a color from the Markov probability distribution
// ---------------------------------------------------------------------------
function markovBlendColor(probs: number[]): THREE.Color {
  const result = new THREE.Color(0, 0, 0);
  const len = Math.min(probs.length, STATE_COLORS.length);
  for (let i = 0; i < len; i++) {
    result.r += STATE_COLORS[i].r * probs[i];
    result.g += STATE_COLORS[i].g * probs[i];
    result.b += STATE_COLORS[i].b * probs[i];
  }
  return result;
}

// ---------------------------------------------------------------------------
// Create ghost trail group for a node with Markov predictions
// ---------------------------------------------------------------------------
export function createGhostTrails(
  node: THREE.Object3D,
  healthMetrics: HealthMetrics,
): THREE.Group {
  const group = new THREE.Group();
  group.userData.isGhostTrail = true;
  group.userData.showPredictions = true;
  group.userData.sourceNode = node;

  const probs = healthMetrics.markovPrediction;
  if (!probs || probs.length < 4) {
    group.visible = false;
    return group;
  }

  const blendedColor = markovBlendColor(probs);

  // Compute a trajectory direction from the probability distribution.
  // Higher warning/freeze probabilities push the trail "downward";
  // healthy pushes "upward". This gives a visual sense of direction.
  const healthScore = (probs[0] ?? 0) - (probs[2] ?? 0) - (probs[3] ?? 0);
  const trajectoryDir = new THREE.Vector3(
    0.3,                          // slight lateral drift
    healthScore * 0.8,            // up = healthy, down = declining
    -0.2,                         // slight depth offset
  ).normalize();

  // Create ghost copies
  for (let i = 0; i < GHOST_COUNT; i++) {
    const ghostMat = new THREE.MeshBasicMaterial({
      color: blendedColor,
      transparent: true,
      opacity: GHOST_OPACITIES[i],
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });

    // Use a simple sphere as the ghost shape (lightweight proxy for the node)
    const ghostGeo = new THREE.SphereGeometry(0.6 - i * 0.08, 8, 8);
    const ghost = new THREE.Mesh(ghostGeo, ghostMat);

    // Position each ghost progressively along the trajectory
    const offset = (i + 1) * 1.2;
    ghost.position.copy(trajectoryDir.clone().multiplyScalar(offset));
    ghost.name = `ghost-${i}`;

    group.add(ghost);
  }

  // Store trajectory for animation
  group.userData.trajectoryDir = trajectoryDir;
  group.userData.probs = probs;

  return group;
}

// ---------------------------------------------------------------------------
// Animate ghost trails each frame
// ---------------------------------------------------------------------------
export function updateGhostTrails(group: THREE.Group, time: number): void {
  if (!group.userData.isGhostTrail) return;

  // Toggle visibility
  group.visible = !!group.userData.showPredictions;
  if (!group.visible) return;

  const trajectoryDir = group.userData.trajectoryDir as THREE.Vector3 | undefined;
  if (!trajectoryDir) return;

  // Track source node position
  const sourceNode = group.userData.sourceNode as THREE.Object3D | undefined;
  if (sourceNode) {
    const wp = new THREE.Vector3();
    sourceNode.getWorldPosition(wp);
    // Convert to local space of group's parent
    if (group.parent) {
      group.parent.worldToLocal(wp);
    }
    group.position.copy(wp);
  }

  // Animate ghosts — gentle floating/pulsing along trajectory
  for (let i = 0; i < group.children.length; i++) {
    const ghost = group.children[i] as THREE.Mesh;
    if (!ghost.isMesh) continue;

    const baseOffset = (i + 1) * 1.2;
    // Sinusoidal drift along trajectory
    const drift = Math.sin(time * 0.8 + i * 1.2) * 0.3;
    const pos = trajectoryDir.clone().multiplyScalar(baseOffset + drift);
    ghost.position.copy(pos);

    // Pulse opacity
    const mat = ghost.material as THREE.MeshBasicMaterial;
    const baseOpacity = GHOST_OPACITIES[i] ?? 0.1;
    mat.opacity = baseOpacity * (0.7 + 0.3 * Math.sin(time * 1.5 + i * 0.8));

    // Subtle scale breathing
    const breath = 1 + Math.sin(time * 1.2 + i * 0.5) * 0.1;
    ghost.scale.setScalar(breath);
  }
}
