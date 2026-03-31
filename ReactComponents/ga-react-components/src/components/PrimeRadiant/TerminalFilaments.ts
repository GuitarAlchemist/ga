// src/components/PrimeRadiant/TerminalFilaments.ts
// Bioluminescent filaments for terminal (leaf) governance nodes.
// Thin glowing tendrils extend outward with pulsing lit dots at the tips,
// suggesting depth and connectivity beyond the visible graph.
// Designed via Octopus UI/UX: Biomimetic Organic + Fiber Optic patterns.

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface FilamentConfig {
  /** Number of filaments per terminal node. Default: 3 */
  count?: number;
  /** Length range [min, max] in world units. Default: [2, 6] */
  length?: [number, number];
  /** Tip dot size. Default: 0.15 */
  tipSize?: number;
  /** Base opacity. Default: 0.6 */
  opacity?: number;
  /** Animation speed multiplier. Default: 1.0 */
  speed?: number;
}

interface FilamentData {
  line: THREE.Line;
  tip: THREE.Mesh;
  basePositions: Float32Array;  // original curve positions (relative to origin)
  origin: THREE.Vector3;        // initial origin position
  direction: THREE.Vector3;     // filament direction
  nodeId: string;               // track which node this belongs to
  phase: number;                // animation phase offset
  swaySpeed: number;            // individual sway rate
  swayAmplitude: number;        // individual sway magnitude
  pulseSpeed: number;           // tip pulse rate
  length: number;
}

export interface TerminalFilamentsHandle {
  group: THREE.Group;
  /** Call each frame with elapsed time. Optionally pass current node positions for tracking. */
  update: (time: number, nodePositions?: Map<string, THREE.Vector3>) => void;
  dispose: () => void;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

function randomRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

/** Generate a curved filament path RELATIVE to origin (origin = 0,0,0) */
function generateFilamentCurveRelative(
  direction: THREE.Vector3,
  length: number,
  segments: number,
): Float32Array {
  const positions = new Float32Array((segments + 1) * 3);

  const perpA = new THREE.Vector3().crossVectors(direction, new THREE.Vector3(0, 1, 0)).normalize();
  if (perpA.lengthSq() < 0.01) perpA.crossVectors(direction, new THREE.Vector3(1, 0, 0)).normalize();
  const perpB = new THREE.Vector3().crossVectors(direction, perpA).normalize();

  const wobbleA = randomRange(0.3, 0.8);
  const wobbleB = randomRange(0.3, 0.8);
  const wobbleFreq = randomRange(1.5, 3.0);

  for (let i = 0; i <= segments; i++) {
    const t = i / segments;
    const taper = 1.0 - t * 0.3;
    const wobble = Math.sin(t * Math.PI * wobbleFreq) * taper;

    // Relative to origin (0,0,0) — will be offset by node position in update()
    positions[i * 3]     = direction.x * length * t + perpA.x * wobble * wobbleA + perpB.x * wobble * wobbleB * 0.5;
    positions[i * 3 + 1] = direction.y * length * t + perpA.y * wobble * wobbleA + perpB.y * wobble * wobbleB * 0.5;
    positions[i * 3 + 2] = direction.z * length * t + perpA.z * wobble * wobbleA + perpB.z * wobble * wobbleB * 0.5;
  }

  return positions;
}

// ---------------------------------------------------------------------------
// Create filaments for a set of terminal nodes
// ---------------------------------------------------------------------------

/**
 * Create bioluminescent filaments for terminal nodes in the force graph.
 * Call this after the graph is initialized, passing the terminal node positions.
 *
 * @param terminalNodes — array of { position, color } for each leaf node
 * @param config — optional configuration
 */
export function createTerminalFilaments(
  terminalNodes: { position: THREE.Vector3; color: THREE.Color; nodeId: string }[],
  config: FilamentConfig = {},
): TerminalFilamentsHandle {
  const {
    count = 3,
    length: lengthRange = [2, 6],
    tipSize = 0.15,
    opacity = 0.6,
    speed = 1.0,
  } = config;

  const group = new THREE.Group();
  group.name = 'terminal-filaments';
  const filaments: FilamentData[] = [];

  const segments = 20;

  for (const node of terminalNodes) {
    for (let f = 0; f < count; f++) {
      // Random outward direction (biased away from center)
      const dir = new THREE.Vector3(
        (Math.random() - 0.5) * 2,
        (Math.random() - 0.5) * 2,
        (Math.random() - 0.5) * 2,
      ).normalize();

      // Bias direction away from graph center (0,0,0)
      const toCenter = node.position.clone().normalize();
      dir.add(toCenter.multiplyScalar(0.5)).normalize();

      const filLength = randomRange(lengthRange[0], lengthRange[1]);
      const positions = generateFilamentCurveRelative(dir, filLength, segments);

      // Line geometry
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.BufferAttribute(positions.slice(), 3));

      // Gradient color: node color at base → pale bioluminescent at tip
      const colors = new Float32Array((segments + 1) * 3);
      const tipColor = new THREE.Color(0x88DDFF); // bioluminescent blue-white
      for (let i = 0; i <= segments; i++) {
        const t = i / segments;
        colors[i * 3]     = lerp(node.color.r, tipColor.r, t * 0.7);
        colors[i * 3 + 1] = lerp(node.color.g, tipColor.g, t * 0.7);
        colors[i * 3 + 2] = lerp(node.color.b, tipColor.b, t * 0.7);
      }
      geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

      const lineMat = new THREE.LineBasicMaterial({
        vertexColors: true,
        transparent: true,
        opacity: opacity * 0.7,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
        linewidth: 1,
      });

      const line = new THREE.Line(geo, lineMat);

      // Tip dot — small glowing sphere at the end
      const tipGeo = new THREE.SphereGeometry(tipSize, 6, 6);
      const tipMat = new THREE.MeshBasicMaterial({
        color: tipColor,
        transparent: true,
        opacity: opacity,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      });
      const tip = new THREE.Mesh(tipGeo, tipMat);

      // Position tip at end of filament (relative + node origin)
      const endIdx = segments * 3;
      tip.position.set(
        node.position.x + positions[endIdx],
        node.position.y + positions[endIdx + 1],
        node.position.z + positions[endIdx + 2],
      );

      group.add(line);
      group.add(tip);

      filaments.push({
        line,
        tip,
        basePositions: positions,
        origin: node.position.clone(),
        direction: dir.clone(),
        nodeId: node.nodeId,
        phase: Math.random() * Math.PI * 2,
        swaySpeed: randomRange(0.3, 0.8) * speed,
        swayAmplitude: randomRange(0.1, 0.4),
        pulseSpeed: randomRange(0.8, 2.0) * speed,
        length: filLength,
      });
    }
  }

  // ---------------------------------------------------------------------------
  // Animation update
  // ---------------------------------------------------------------------------
  function update(time: number, nodePositions?: Map<string, THREE.Vector3>): void {
    for (const fil of filaments) {
      // Track current node position (force graph moves nodes)
      const currentOrigin = nodePositions?.get(fil.nodeId) ?? fil.origin;

      const posAttr = fil.line.geometry.attributes.position as THREE.BufferAttribute;
      const positions = posAttr.array as Float32Array;

      // Organic sway — relative positions + current node origin
      for (let i = 0; i <= segments; i++) {
        const t = i / segments;
        const swayFactor = t * t * fil.swayAmplitude;
        const swayX = Math.sin(time * fil.swaySpeed + fil.phase + t * 3.0) * swayFactor;
        const swayY = Math.cos(time * fil.swaySpeed * 0.7 + fil.phase + t * 2.5) * swayFactor * 0.6;
        const swayZ = Math.sin(time * fil.swaySpeed * 0.5 + fil.phase * 1.3 + t * 2.0) * swayFactor * 0.4;

        positions[i * 3]     = currentOrigin.x + fil.basePositions[i * 3]     + swayX;
        positions[i * 3 + 1] = currentOrigin.y + fil.basePositions[i * 3 + 1] + swayY;
        positions[i * 3 + 2] = currentOrigin.z + fil.basePositions[i * 3 + 2] + swayZ;
      }
      posAttr.needsUpdate = true;

      // Update tip position to follow the end of the filament
      const endIdx = segments * 3;
      fil.tip.position.set(positions[endIdx], positions[endIdx + 1], positions[endIdx + 2]);

      // Pulse tip brightness — breathing bioluminescent glow
      const pulse = 0.4 + 0.6 * (0.5 + 0.5 * Math.sin(time * fil.pulseSpeed + fil.phase));
      (fil.tip.material as THREE.MeshBasicMaterial).opacity = pulse;

      // Subtle scale pulse on tip
      const scale = 0.8 + 0.4 * (0.5 + 0.5 * Math.sin(time * fil.pulseSpeed * 1.3 + fil.phase));
      fil.tip.scale.setScalar(scale);
    }
  }

  // ---------------------------------------------------------------------------
  // Cleanup
  // ---------------------------------------------------------------------------
  function dispose(): void {
    for (const fil of filaments) {
      fil.line.geometry.dispose();
      (fil.line.material as THREE.Material).dispose();
      fil.tip.geometry.dispose();
      (fil.tip.material as THREE.Material).dispose();
    }
    group.clear();
  }

  return { group, update, dispose };
}
