// src/components/PrimeRadiant/BeliefWeather.ts
// Visual weather effects for governance nodes based on tetravalent belief state.
// Each node gets atmospheric effects (glow, shroud, fog, lightning) and
// orbiting particles colored by truth value (T/F/U/C).

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type TruthValue = 'true' | 'false' | 'unknown' | 'contradictory';

export interface BeliefWeatherHandle {
  group: THREE.Group;
  update: (time: number) => void;
  setNodeBelief: (nodeId: string, truthValue: TruthValue, confidence: number) => void;
  dispose: () => void;
}

interface NodeWeatherState {
  nodeId: string;
  position: THREE.Vector3;
  radius: number;
  truthValue: TruthValue;
  confidence: number;

  // Visual elements
  shroud: THREE.Mesh;
  particles: THREE.Points;
  particlePositions: Float32Array;
  particleVelocities: Float32Array;
  lightningLines: [THREE.Line, THREE.Line];
  lightningVisible: boolean;

  // Animation state
  orbitPhases: Float32Array;   // per-particle phase offset
  orbitSpeeds: Float32Array;   // per-particle angular speed
  orbitRadii: Float32Array;    // per-particle orbit radius
  orbitTilts: Float32Array;    // per-particle tilt angle
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const PARTICLES_PER_NODE = 8;
const LIGHTNING_SEGMENTS = 6;

const TRUTH_COLORS: Record<TruthValue, number> = {
  true: 0xFFD700,          // gold
  false: 0xCC2222,         // red
  unknown: 0x999999,       // gray
  contradictory: 0x9944DD, // purple
};

const SHROUD_COLORS: Record<TruthValue, { color: number; opacity: number }> = {
  true: { color: 0xFFDD44, opacity: 0.08 },
  false: { color: 0x111111, opacity: 0.25 },
  unknown: { color: 0xAAAAAA, opacity: 0.12 },
  contradictory: { color: 0x7722BB, opacity: 0.15 },
};

// ---------------------------------------------------------------------------
// Shared geometries (reused across all nodes)
// ---------------------------------------------------------------------------

let _sharedShroudGeo: THREE.SphereGeometry | null = null;
let _sharedParticleGeo: THREE.BufferGeometry | null = null;

function getSharedShroudGeo(): THREE.SphereGeometry {
  if (!_sharedShroudGeo) {
    _sharedShroudGeo = new THREE.SphereGeometry(1, 16, 12);
  }
  return _sharedShroudGeo;
}

function getSharedParticleGeo(): THREE.BufferGeometry {
  if (!_sharedParticleGeo) {
    _sharedParticleGeo = new THREE.BufferGeometry();
    // Placeholder positions — each node copies and updates its own attribute
    const pos = new Float32Array(PARTICLES_PER_NODE * 3);
    _sharedParticleGeo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  }
  return _sharedParticleGeo;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function randomRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

/** Generate jagged lightning line positions between two points */
function generateLightningPositions(
  center: THREE.Vector3,
  radius: number,
): Float32Array {
  const positions = new Float32Array((LIGHTNING_SEGMENTS + 1) * 3);
  // Random start and end on sphere surface
  const startDir = new THREE.Vector3(
    Math.random() - 0.5, Math.random() - 0.5, Math.random() - 0.5,
  ).normalize();
  const endDir = new THREE.Vector3(
    Math.random() - 0.5, Math.random() - 0.5, Math.random() - 0.5,
  ).normalize();

  for (let i = 0; i <= LIGHTNING_SEGMENTS; i++) {
    const t = i / LIGHTNING_SEGMENTS;
    // Lerp between start and end with jitter
    const jitter = i > 0 && i < LIGHTNING_SEGMENTS ? randomRange(-0.3, 0.3) * radius : 0;
    positions[i * 3]     = center.x + startDir.x * radius * (1 - t) + endDir.x * radius * t + jitter;
    positions[i * 3 + 1] = center.y + startDir.y * radius * (1 - t) + endDir.y * radius * t + jitter;
    positions[i * 3 + 2] = center.z + startDir.z * radius * (1 - t) + endDir.z * radius * t + jitter;
  }
  return positions;
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

export function createBeliefWeather(
  nodes: { id: string; position: THREE.Vector3; radius: number }[],
): BeliefWeatherHandle {
  const group = new THREE.Group();
  group.name = 'belief-weather';
  const stateMap = new Map<string, NodeWeatherState>();

  for (const node of nodes) {
    const state = createNodeWeather(node);
    stateMap.set(node.id, state);
    group.add(state.shroud);
    group.add(state.particles);
    group.add(state.lightningLines[0]);
    group.add(state.lightningLines[1]);
  }

  // ----- API -----

  function setNodeBelief(nodeId: string, truthValue: TruthValue, confidence: number): void {
    const state = stateMap.get(nodeId);
    if (!state) return;

    state.truthValue = truthValue;
    state.confidence = Math.max(0, Math.min(1, confidence));

    // Update shroud material
    const shroudDef = SHROUD_COLORS[truthValue];
    const shroudMat = state.shroud.material as THREE.MeshBasicMaterial;
    shroudMat.color.setHex(shroudDef.color);
    shroudMat.opacity = shroudDef.opacity * state.confidence;

    // Update particle color
    const particleMat = state.particles.material as THREE.PointsMaterial;
    particleMat.color.setHex(TRUTH_COLORS[truthValue]);

    // Adjust particle speeds based on truth value
    for (let i = 0; i < PARTICLES_PER_NODE; i++) {
      switch (truthValue) {
        case 'true':
          state.orbitSpeeds[i] = randomRange(0.8, 1.5);
          break;
        case 'false':
          state.orbitSpeeds[i] = randomRange(0.3, 0.6);
          break;
        case 'unknown':
          state.orbitSpeeds[i] = randomRange(0.15, 0.35);
          break;
        case 'contradictory':
          state.orbitSpeeds[i] = randomRange(2.0, 4.0);
          break;
      }
    }

    // Lightning visibility
    const showLightning = truthValue === 'contradictory';
    state.lightningVisible = showLightning;
    state.lightningLines[0].visible = showLightning;
    state.lightningLines[1].visible = showLightning;
  }

  function update(time: number): void {
    for (const state of stateMap.values()) {
      updateNodeWeather(state, time);
    }
  }

  function dispose(): void {
    for (const state of stateMap.values()) {
      (state.shroud.material as THREE.Material).dispose();
      (state.particles.material as THREE.Material).dispose();
      state.particles.geometry.dispose();
      (state.lightningLines[0].material as THREE.Material).dispose();
      state.lightningLines[0].geometry.dispose();
      (state.lightningLines[1].material as THREE.Material).dispose();
      state.lightningLines[1].geometry.dispose();
    }
    group.clear();
    stateMap.clear();

    // Release shared geometries
    if (_sharedShroudGeo) { _sharedShroudGeo.dispose(); _sharedShroudGeo = null; }
    if (_sharedParticleGeo) { _sharedParticleGeo.dispose(); _sharedParticleGeo = null; }
  }

  return { group, update, setNodeBelief, dispose };
}

// ---------------------------------------------------------------------------
// Per-node creation
// ---------------------------------------------------------------------------

function createNodeWeather(
  node: { id: string; position: THREE.Vector3; radius: number },
): NodeWeatherState {
  const { id, position, radius } = node;
  const defaultTruth: TruthValue = 'unknown';

  // --- Shroud mesh ---
  const shroudMat = new THREE.MeshBasicMaterial({
    color: SHROUD_COLORS[defaultTruth].color,
    transparent: true,
    opacity: SHROUD_COLORS[defaultTruth].opacity,
    side: THREE.BackSide,
    depthWrite: false,
    blending: THREE.NormalBlending,
  });
  const shroud = new THREE.Mesh(getSharedShroudGeo(), shroudMat);
  const shroudScale = radius * 1.6;
  shroud.scale.setScalar(shroudScale);
  shroud.position.copy(position);

  // --- Particle system ---
  const particlePositions = new Float32Array(PARTICLES_PER_NODE * 3);
  const particleVelocities = new Float32Array(PARTICLES_PER_NODE * 3);
  const orbitPhases = new Float32Array(PARTICLES_PER_NODE);
  const orbitSpeeds = new Float32Array(PARTICLES_PER_NODE);
  const orbitRadii = new Float32Array(PARTICLES_PER_NODE);
  const orbitTilts = new Float32Array(PARTICLES_PER_NODE);

  for (let i = 0; i < PARTICLES_PER_NODE; i++) {
    orbitPhases[i] = Math.random() * Math.PI * 2;
    orbitSpeeds[i] = randomRange(0.15, 0.35); // default: unknown = slow
    orbitRadii[i] = radius * randomRange(1.2, 2.0);
    orbitTilts[i] = randomRange(-Math.PI * 0.4, Math.PI * 0.4);

    // Initialize velocities for fog drift (unknown state)
    particleVelocities[i * 3]     = randomRange(-0.02, 0.02);
    particleVelocities[i * 3 + 1] = randomRange(-0.02, 0.02);
    particleVelocities[i * 3 + 2] = randomRange(-0.02, 0.02);
  }

  // Clone geometry so each node has independent positions
  const particleGeo = getSharedParticleGeo().clone();
  particleGeo.setAttribute(
    'position',
    new THREE.BufferAttribute(particlePositions, 3),
  );

  const particleMat = new THREE.PointsMaterial({
    color: TRUTH_COLORS[defaultTruth],
    size: radius * 0.25,
    transparent: true,
    opacity: 0.7,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });
  const particles = new THREE.Points(particleGeo, particleMat);

  // --- Lightning lines (2 per node, only visible in contradictory state) ---
  const lightningLines = createLightningPair(position, radius);

  return {
    nodeId: id,
    position: position.clone(),
    radius,
    truthValue: defaultTruth,
    confidence: 0.5,
    shroud,
    particles,
    particlePositions,
    particleVelocities,
    lightningLines,
    lightningVisible: false,
    orbitPhases,
    orbitSpeeds,
    orbitRadii,
    orbitTilts,
  };
}

function createLightningPair(
  position: THREE.Vector3,
  radius: number,
): [THREE.Line, THREE.Line] {
  const lines: THREE.Line[] = [];
  for (let l = 0; l < 2; l++) {
    const positions = generateLightningPositions(position, radius * 1.3);
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    const mat = new THREE.LineBasicMaterial({
      color: 0xBB88FF,
      transparent: true,
      opacity: 0.8,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      linewidth: 1,
    });
    const line = new THREE.Line(geo, mat);
    line.visible = false;
    lines.push(line);
  }
  return lines as [THREE.Line, THREE.Line];
}

// ---------------------------------------------------------------------------
// Per-frame update
// ---------------------------------------------------------------------------

function updateNodeWeather(state: NodeWeatherState, time: number): void {
  const { position, radius, truthValue, confidence } = state;

  // --- Shroud pulse ---
  const shroudMat = state.shroud.material as THREE.MeshBasicMaterial;
  const baseShroudOpacity = SHROUD_COLORS[truthValue].opacity * confidence;
  if (truthValue === 'true') {
    // Warm golden pulse
    const pulse = 0.7 + 0.3 * Math.sin(time * 1.5);
    shroudMat.opacity = baseShroudOpacity * pulse;
    const shroudScale = radius * (1.5 + 0.1 * Math.sin(time * 1.2));
    state.shroud.scale.setScalar(shroudScale);
  } else if (truthValue === 'false') {
    // Dark, steady
    shroudMat.opacity = baseShroudOpacity;
    state.shroud.scale.setScalar(radius * 1.6);
  } else if (truthValue === 'unknown') {
    // Gentle fog breathing
    const breath = 0.8 + 0.2 * Math.sin(time * 0.6);
    shroudMat.opacity = baseShroudOpacity * breath;
    state.shroud.scale.setScalar(radius * (1.5 + 0.15 * Math.sin(time * 0.4)));
  } else {
    // Contradictory: erratic flicker
    const flicker = 0.5 + 0.5 * Math.abs(Math.sin(time * 5.0 + Math.sin(time * 13.0)));
    shroudMat.opacity = baseShroudOpacity * flicker;
    state.shroud.scale.setScalar(radius * 1.6);
  }
  state.shroud.position.copy(position);

  // --- Particle orbits ---
  const posAttr = state.particles.geometry.attributes.position as THREE.BufferAttribute;
  const positions = posAttr.array as Float32Array;

  for (let i = 0; i < PARTICLES_PER_NODE; i++) {
    const phase = state.orbitPhases[i];
    const speed = state.orbitSpeeds[i];
    const orbitR = state.orbitRadii[i];
    const tilt = state.orbitTilts[i];

    if (truthValue === 'unknown') {
      // Fog drift: slow brownian motion within a radius
      const drift = state.particleVelocities;
      const dx = drift[i * 3]     * Math.sin(time * 0.3 + phase);
      const dy = drift[i * 3 + 1] * Math.cos(time * 0.25 + phase * 1.3);
      const dz = drift[i * 3 + 2] * Math.sin(time * 0.35 + phase * 0.7);

      const fogR = orbitR * (0.5 + 0.5 * Math.sin(time * 0.2 + phase));
      positions[i * 3]     = position.x + fogR * dx * 10;
      positions[i * 3 + 1] = position.y + fogR * dy * 10;
      positions[i * 3 + 2] = position.z + fogR * dz * 10;
    } else if (truthValue === 'contradictory') {
      // Erratic: jagged orbits with random offsets
      const angle = time * speed + phase;
      const jitterX = Math.sin(time * 7.0 + phase * 3.0) * radius * 0.3;
      const jitterY = Math.cos(time * 9.0 + phase * 5.0) * radius * 0.3;
      positions[i * 3]     = position.x + Math.cos(angle) * orbitR + jitterX;
      positions[i * 3 + 1] = position.y + Math.sin(tilt) * Math.sin(angle) * orbitR + jitterY;
      positions[i * 3 + 2] = position.z + Math.cos(tilt) * Math.sin(angle) * orbitR;
    } else {
      // Normal orbit (true = sparkly fast, false = dim slow)
      const angle = time * speed + phase;
      positions[i * 3]     = position.x + Math.cos(angle) * orbitR;
      positions[i * 3 + 1] = position.y + Math.sin(tilt) * Math.sin(angle) * orbitR;
      positions[i * 3 + 2] = position.z + Math.cos(tilt) * Math.sin(angle) * orbitR;
    }
  }
  posAttr.needsUpdate = true;

  // --- Particle material pulse ---
  const pMat = state.particles.material as THREE.PointsMaterial;
  if (truthValue === 'true') {
    // Sparkle: oscillate opacity and size
    pMat.opacity = 0.5 + 0.5 * Math.abs(Math.sin(time * 3.0));
    pMat.size = radius * (0.2 + 0.1 * Math.sin(time * 4.0));
  } else if (truthValue === 'false') {
    pMat.opacity = 0.3 * confidence;
    pMat.size = radius * 0.2;
  } else if (truthValue === 'unknown') {
    pMat.opacity = 0.3 + 0.2 * Math.sin(time * 0.8);
    pMat.size = radius * 0.3;
  } else {
    pMat.opacity = 0.6 + 0.4 * Math.abs(Math.sin(time * 6.0));
    pMat.size = radius * (0.15 + 0.15 * Math.abs(Math.sin(time * 8.0)));
  }

  // --- Lightning (contradictory only) ---
  if (state.lightningVisible) {
    // Regenerate jagged geometry every ~0.15s (roughly every 9 frames at 60fps)
    const shouldRegen = Math.floor(time * 7) !== Math.floor((time - 0.016) * 7);
    if (shouldRegen) {
      for (const line of state.lightningLines) {
        const newPositions = generateLightningPositions(position, radius * 1.3);
        const attr = line.geometry.attributes.position as THREE.BufferAttribute;
        (attr.array as Float32Array).set(newPositions);
        attr.needsUpdate = true;
      }
    }

    // Flicker opacity
    const lightningOpacity = Math.random() > 0.3 ? 0.6 + Math.random() * 0.4 : 0;
    for (const line of state.lightningLines) {
      (line.material as THREE.LineBasicMaterial).opacity = lightningOpacity * confidence;
    }
  }
}
