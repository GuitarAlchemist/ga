// src/components/PrimeRadiant/SignalAura.ts
// Pain/pleasure aura ripples and signal filaments for algedonic governance signals.
// When an algedonic signal fires, its source node emits an expanding ripple ring.
// Active signals grow pulsing dashed filaments connecting source to governance nodes.

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type SignalType = 'pain' | 'pleasure';

export interface SignalAuraHandle {
  group: THREE.Group;
  update: (time: number) => void;
  emitRipple: (nodeId: string, type: SignalType, intensity: number) => void;
  addFilament: (fromNodeId: string, toNodeId: string, type: SignalType) => string;
  removeFilament: (filamentId: string) => void;
  dispose: () => void;
}

// ---------------------------------------------------------------------------
// Internal types
// ---------------------------------------------------------------------------

interface RippleState {
  mesh: THREE.Mesh;
  active: boolean;
  elapsed: number;
  duration: number;
  startRadius: number;
  endRadius: number;
  type: SignalType;
  intensity: number;
}

interface FilamentState {
  id: string;
  line: THREE.Line;
  fromNodeId: string;
  toNodeId: string;
  type: SignalType;
  dashOffset: number;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const MAX_RIPPLES = 20;
const MAX_FILAMENTS = 10;
const RIPPLE_DURATION = 2.0; // seconds
const RIPPLE_EXPANSION = 5.0; // expand to 5x start radius
const PAIN_COLOR = new THREE.Color(0xff2222);
const PLEASURE_COLOR = new THREE.Color(0x22ff66);
const RING_INNER_RATIO = 0.92; // thin ring: inner = 92% of outer
const RING_SEGMENTS = 48;

// ---------------------------------------------------------------------------
// Shared geometry — one RingGeometry reused across all ripple meshes
// ---------------------------------------------------------------------------

function createRingGeometry(): THREE.RingGeometry {
  // Unit ring (inner=0.92, outer=1.0) — scaled at runtime
  return new THREE.RingGeometry(RING_INNER_RATIO, 1.0, RING_SEGMENTS);
}

// ---------------------------------------------------------------------------
// Ripple material factory
// ---------------------------------------------------------------------------

function createRippleMaterial(): THREE.MeshBasicMaterial {
  return new THREE.MeshBasicMaterial({
    color: 0xffffff,
    transparent: true,
    opacity: 0,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
}

// ---------------------------------------------------------------------------
// Filament helpers
// ---------------------------------------------------------------------------

function createFilamentLine(from: THREE.Vector3, to: THREE.Vector3, type: SignalType): THREE.Line {
  const positions = new Float32Array(6);
  positions[0] = from.x; positions[1] = from.y; positions[2] = from.z;
  positions[3] = to.x;   positions[4] = to.y;   positions[5] = to.z;

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));

  const color = type === 'pain' ? PAIN_COLOR : PLEASURE_COLOR;
  const mat = new THREE.LineDashedMaterial({
    color,
    transparent: true,
    opacity: 0.8,
    dashSize: 0.4,
    gapSize: 0.2,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });

  const line = new THREE.Line(geo, mat);
  line.computeLineDistances();
  return line;
}

let nextFilamentId = 0;

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

export function createSignalAura(
  nodePositions: Map<string, THREE.Vector3>,
  nodeRadii: Map<string, number>,
): SignalAuraHandle {
  const group = new THREE.Group();
  group.name = 'signal-aura';

  // ── Shared ring geometry ──
  const sharedRingGeo = createRingGeometry();

  // ── Ripple pool ──
  const ripples: RippleState[] = [];
  for (let i = 0; i < MAX_RIPPLES; i++) {
    const mat = createRippleMaterial();
    const mesh = new THREE.Mesh(sharedRingGeo, mat);
    mesh.visible = false;
    group.add(mesh);
    ripples.push({
      mesh,
      active: false,
      elapsed: 0,
      duration: RIPPLE_DURATION,
      startRadius: 1,
      endRadius: 5,
      type: 'pain',
      intensity: 1,
    });
  }

  // ── Filaments ──
  const filaments: FilamentState[] = [];

  // ── emitRipple ──
  function emitRipple(nodeId: string, type: SignalType, intensity: number): void {
    const pos = nodePositions.get(nodeId);
    if (!pos) return;

    const baseRadius = nodeRadii.get(nodeId) ?? 0.5;

    // Find an inactive ripple from the pool
    const ripple = ripples.find(r => !r.active);
    if (!ripple) return; // pool exhausted

    ripple.active = true;
    ripple.elapsed = 0;
    ripple.duration = RIPPLE_DURATION;
    ripple.startRadius = baseRadius;
    ripple.endRadius = baseRadius * RIPPLE_EXPANSION;
    ripple.type = type;
    ripple.intensity = Math.min(Math.max(intensity, 0), 1);

    const color = type === 'pain' ? PAIN_COLOR : PLEASURE_COLOR;
    const mat = ripple.mesh.material as THREE.MeshBasicMaterial;
    mat.color.copy(color);
    mat.opacity = ripple.intensity;

    ripple.mesh.position.copy(pos);
    // Orient ring to face camera (billboard-ish, face +Z by default; Three.js lookAt works)
    ripple.mesh.rotation.set(0, 0, 0);
    ripple.mesh.scale.setScalar(ripple.startRadius);
    ripple.mesh.visible = true;
  }

  // ── addFilament ──
  function addFilament(fromNodeId: string, toNodeId: string, type: SignalType): string {
    if (filaments.length >= MAX_FILAMENTS) {
      // Evict oldest filament
      const oldest = filaments.shift();
      if (oldest) {
        oldest.line.geometry.dispose();
        (oldest.line.material as THREE.Material).dispose();
        group.remove(oldest.line);
      }
    }

    const from = nodePositions.get(fromNodeId) ?? new THREE.Vector3();
    const to = nodePositions.get(toNodeId) ?? new THREE.Vector3();

    const id = `filament-${nextFilamentId++}`;
    const line = createFilamentLine(from, to, type);
    group.add(line);

    filaments.push({ id, line, fromNodeId, toNodeId, type, dashOffset: 0 });
    return id;
  }

  // ── removeFilament ──
  function removeFilament(filamentId: string): void {
    const idx = filaments.findIndex(f => f.id === filamentId);
    if (idx < 0) return;

    const fil = filaments[idx];
    fil.line.geometry.dispose();
    (fil.line.material as THREE.Material).dispose();
    group.remove(fil.line);
    filaments.splice(idx, 1);
  }

  // ── update ──
  function update(time: number): void {
    // We need delta time; use a closure variable to track last time
    const dt = updateState.lastTime > 0 ? Math.min(time - updateState.lastTime, 0.1) : 0.016;
    updateState.lastTime = time;

    // ── Ripples ──
    for (const ripple of ripples) {
      if (!ripple.active) continue;

      ripple.elapsed += dt;
      const t = ripple.elapsed / ripple.duration;

      if (t >= 1.0) {
        // Return to pool
        ripple.active = false;
        ripple.mesh.visible = false;
        continue;
      }

      // Expand ring
      const currentRadius = ripple.startRadius + (ripple.endRadius - ripple.startRadius) * t;
      ripple.mesh.scale.setScalar(currentRadius);

      // Fade opacity: full at start, 0 at end
      const opacity = ripple.intensity * (1.0 - t);
      (ripple.mesh.material as THREE.MeshBasicMaterial).opacity = opacity;

      // Update position if node moved
      const nodeId = findNodeForPosition(ripple.mesh.position);
      if (nodeId) {
        const pos = nodePositions.get(nodeId);
        if (pos) ripple.mesh.position.copy(pos);
      }
    }

    // ── Filaments ──
    for (const fil of filaments) {
      // Update endpoint positions if nodes moved
      const from = nodePositions.get(fil.fromNodeId);
      const to = nodePositions.get(fil.toNodeId);
      if (from && to) {
        const posAttr = fil.line.geometry.attributes.position as THREE.BufferAttribute;
        const arr = posAttr.array as Float32Array;
        arr[0] = from.x; arr[1] = from.y; arr[2] = from.z;
        arr[3] = to.x;   arr[4] = to.y;   arr[5] = to.z;
        posAttr.needsUpdate = true;
        fil.line.computeLineDistances();
      }

      // Animate dash offset for pulsing effect
      fil.dashOffset += dt * 2.0;
      const mat = fil.line.material as THREE.LineDashedMaterial;
      mat.dashOffset = -fil.dashOffset;

      // Pulse opacity
      mat.opacity = 0.5 + 0.3 * Math.sin(time * 3.0);
    }
  }

  const updateState = { lastTime: 0 };

  // Helper: we don't track which node a ripple came from, so skip position tracking
  // (ripples stay where emitted — acceptable since they last only 2s)
  function findNodeForPosition(_pos: THREE.Vector3): string | null {
    // Ripples are short-lived; position tracking is not critical
    return null;
  }

  // ── dispose ──
  function dispose(): void {
    // Dispose ripple materials (geometry is shared)
    for (const ripple of ripples) {
      (ripple.mesh.material as THREE.Material).dispose();
    }
    sharedRingGeo.dispose();

    // Dispose filaments
    for (const fil of filaments) {
      fil.line.geometry.dispose();
      (fil.line.material as THREE.Material).dispose();
    }
    filaments.length = 0;

    group.clear();
  }

  return { group, update, emitRipple, addFilament, removeFilament, dispose };
}
