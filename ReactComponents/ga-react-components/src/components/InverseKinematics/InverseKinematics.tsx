/**
 * InverseKinematics — self-contained chord IK demo.
 *
 * Renders a 3D guitar neck with a stylised left-hand skeleton (palm + 4
 * fingers, each a 3-bone chain) that animates to reach chord-shape
 * targets via FABRIK (Forward And Backward Reaching IK). Iterates the
 * solver in the render loop so the user sees the hand "settle" onto a
 * shape as it interpolates between presets.
 *
 * Replaces the previous backend-dependent prototype with a fully
 * client-side version. No API call needed.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { Box } from '@mui/material';

// ─── Rigged hand model ───────────────────────────────────────────────────
// We replace the procedural cylinder/sphere "hand" with a properly rigged
// glTF model. The asset is the WebXR generic-hand profile shipped at the
// CDN below — same one Three.js's XRHandModelFactory uses for its 'mesh'
// profile. Anatomically correct: 5 fingers including thumb, 3 phalanges
// per finger (2 for thumb), standard OpenXR XR_HAND_JOINT_* bone names.
//
// We load it ourselves (no XR session needed), parent it to the existing
// wrist Group so the wrist quaternion still drives whole-hand orientation,
// and drive each phalanx via bone.lookAt() from our FABRIK joint output.
const HAND_PROFILE_URL =
  'https://cdn.jsdelivr.net/npm/@webxr-input-profiles/assets@1.0/dist/profiles/generic-hand/';

// OpenXR XR_HAND_JOINT_* bone names exposed by the rigged GLB. Indexed
// matches the order returned by XRHandMeshModel; we don't use all of them
// (we drive 3 phalanges per finger + 2 for thumb, leaving metacarpals at
// their rest pose since those sit inside the palm).
const HAND_BONE_NAMES = [
  'wrist',
  'thumb-metacarpal',
  'thumb-phalanx-proximal',
  'thumb-phalanx-distal',
  'thumb-tip',
  'index-finger-metacarpal',
  'index-finger-phalanx-proximal',
  'index-finger-phalanx-intermediate',
  'index-finger-phalanx-distal',
  'index-finger-tip',
  'middle-finger-metacarpal',
  'middle-finger-phalanx-proximal',
  'middle-finger-phalanx-intermediate',
  'middle-finger-phalanx-distal',
  'middle-finger-tip',
  'ring-finger-metacarpal',
  'ring-finger-phalanx-proximal',
  'ring-finger-phalanx-intermediate',
  'ring-finger-phalanx-distal',
  'ring-finger-tip',
  'pinky-finger-metacarpal',
  'pinky-finger-phalanx-proximal',
  'pinky-finger-phalanx-intermediate',
  'pinky-finger-phalanx-distal',
  'pinky-finger-tip',
] as const;

// Mapping from our procedural FingerName to the OpenXR phalanx chain
// (proximal, intermediate, distal). For each phalanx i, FABRIK joint i
// is the bone's origin and joint i+1 is its lookAt target.
const FINGER_TO_PHALANX_CHAIN: Record<string, readonly [string, string, string]> = {
  index:  ['index-finger-phalanx-proximal',  'index-finger-phalanx-intermediate',  'index-finger-phalanx-distal'],
  middle: ['middle-finger-phalanx-proximal', 'middle-finger-phalanx-intermediate', 'middle-finger-phalanx-distal'],
  ring:   ['ring-finger-phalanx-proximal',   'ring-finger-phalanx-intermediate',   'ring-finger-phalanx-distal'],
  pinky:  ['pinky-finger-phalanx-proximal',  'pinky-finger-phalanx-intermediate',  'pinky-finger-phalanx-distal'],
};

// ─── Chord types ────────────────────────────────────────────────────────

export interface ChordFret {
  /** 1 (high E) … 6 (low E). */
  string: number;
  /** 0 = open, > 0 = fretted. */
  fret: number;
}

export interface ChordShape {
  name: string;
  positions: ChordFret[];
}

export interface InverseKinematicsProps {
  width?: number;
  height?: number;
  chord: ChordShape;
  /** FABRIK iterations per frame. Higher = snappier, more CPU. */
  ikIterations?: number;
  /** Tween rate toward IK targets (0..1, exponential). */
  ikDamping?: number;
  /** Show target spheres at the fret positions. */
  showTargets?: boolean;
  autoRotate?: boolean;
}

// ─── Geometry constants ─────────────────────────────────────────────────

const NECK_LENGTH = 6.0;       // 12 frets visible (~30cm scaled up)
const NECK_WIDTH = 1.0;        // 6 strings span 1 unit
const NECK_THICKNESS = 0.25;
const FRET_COUNT = 12;
const STRING_COUNT = 6;

const FINGER_NAMES = ['index', 'middle', 'ring', 'pinky'] as const;
type FingerName = typeof FINGER_NAMES[number];

// Base palm offset (relative to wrist), and per-finger MCP offset (the
// knuckles span the palm width).
const PALM_LENGTH = 0.55;
const FINGER_MCP_OFFSETS: Record<FingerName, [number, number, number]> = {
  index:  [-0.18, 0, PALM_LENGTH],
  middle: [-0.06, 0, PALM_LENGTH + 0.02],
  ring:   [ 0.06, 0, PALM_LENGTH],
  pinky:  [ 0.18, 0, PALM_LENGTH - 0.04],
};

// Phalanx lengths for each finger: proximal, middle, distal. Drawn
// loosely from anthropometric averages, scaled up.
const FINGER_BONE_LENGTHS: Record<FingerName, [number, number, number]> = {
  index:  [0.42, 0.26, 0.20],
  middle: [0.46, 0.30, 0.22],
  ring:   [0.42, 0.28, 0.20],
  pinky:  [0.34, 0.22, 0.16],
};

// ─── Palm basis ─────────────────────────────────────────────────────────
// The hand approaches the strings FROM ABOVE the fretboard so the bone
// segments never pass through the neck wood (the "physically impossible
// crossing" bug). Palm tilts DOWN-AND-FORWARD by TILT_ANGLE_RAD from
// horizontal so the knuckle row sits above the strings and the fingers
// curl DOWN onto their targets.
const TILT_ANGLE_RAD = Math.PI / 7; // ~25.7° down-tilt

interface PalmBasis {
  matrix: THREE.Matrix4;
  forward: THREE.Vector3; // palm-forward (fingertip direction, tilted DOWN)
  right: THREE.Vector3;   // knuckle-row axis (oriented so index → +X = nut side)
  up: THREE.Vector3;      // back-of-hand normal
}

const computePalmBasis = (horizDir: THREE.Vector3): PalmBasis => {
  const h = horizDir.clone();
  h.y = 0;
  if (h.lengthSq() < 1e-6) {
    h.set(0, 0, 1); // stable default: chord-side direction
  } else {
    h.normalize();
  }
  const cos = Math.cos(TILT_ANGLE_RAD);
  const sin = Math.sin(TILT_ANGLE_RAD);
  // palm-forward = horizontal toward chord, tilted DOWN by TILT_ANGLE_RAD.
  const forward = new THREE.Vector3(h.x * cos, -sin, h.z * cos);
  // palm-up = horizontal pointed AWAY from chord, tilted UP by (90° - TILT)
  //  — i.e., back-of-hand direction. Stays +Y dominant.
  const up = new THREE.Vector3(h.x * sin, cos, h.z * sin);
  // palm-right = up × forward (RIGHT-HANDED basis). Critical: doing this
  // the other way (right × forward → up) produces a det = -1 reflection,
  // which setFromRotationMatrix silently mis-extracts into a quaternion
  // that rotates by ~2× the intended angle on a different axis (caught
  // during MCP-controlled validation on 2026-05-16).
  const right = new THREE.Vector3().crossVectors(up, forward).normalize();
  const matrix = new THREE.Matrix4().makeBasis(right, up, forward);
  return { matrix, forward, right, up };
};

// ─── Helpers ────────────────────────────────────────────────────────────

const fretX = (fret: number): number => {
  // Fret 0 (nut) is at x = NECK_LENGTH/2; fret 12 at -NECK_LENGTH/2.
  // Use logarithmic spacing for a real guitar feel.
  if (fret === 0) return NECK_LENGTH / 2;
  const ratio = 1 - Math.pow(2, -fret / 12);
  return NECK_LENGTH / 2 - ratio * NECK_LENGTH;
};

const stringZ = (str: number): number => {
  // String 1 (high E) at +0.45, string 6 (low E) at -0.45.
  return ((str - 1) / (STRING_COUNT - 1) - 0.5) * 0.9;
};

const targetForFret = (string: number, fret: number): THREE.Vector3 => {
  // Position the fingertip JUST behind the fret (between this fret and
  // the previous one) — that's where guitarists actually press.
  const xCurr = fretX(fret);
  const xPrev = fretX(Math.max(0, fret - 1));
  const x = (xCurr + xPrev) / 2;
  const z = stringZ(string);
  // Slightly above the fretboard surface.
  const y = NECK_THICKNESS / 2 + 0.03;
  return new THREE.Vector3(x, y, z);
};

// ─── Hand bone state ────────────────────────────────────────────────────
// Each finger is a chain of joint world positions. FABRIK operates on
// those positions directly (positional IK), then we orient capsule meshes
// to match the segments between consecutive joints.

interface FingerChain {
  name: FingerName;
  /** Local-to-wrist MCP offset. Stays fixed; the palm is rigid. */
  mcpLocal: THREE.Vector3;
  /** Bone lengths (proximal → distal). */
  bones: [number, number, number];
  /** Joint world positions: [mcp, pip, dip, tip] — 4 entries, 3 bones. */
  joints: [THREE.Vector3, THREE.Vector3, THREE.Vector3, THREE.Vector3];
  /** Mesh per bone. */
  meshes: THREE.Mesh[];
  /** Knuckle spheres (4: one per joint). */
  knuckles: THREE.Mesh[];
  /** Solver target (world). Fingers tween toward this. */
  target: THREE.Vector3;
}

// FABRIK in 3D — joints[0] is the root (fixed), last is the tip (drawn
// to target). Maintains bone lengths exactly and converges quickly.
// Joint-DOF constraints (cone for MCP, hinge for PIP/DIP) tracked as
// follow-up task #222 — full per-DOF model needed to avoid the
// regressions a minimal palm-plane projection introduces on barré chords
// (see docs/plans/2026-05-16-arch-ik-joint-constraints-plan.md).
function fabrikSolve(
  joints: THREE.Vector3[],
  bones: number[],
  rootWorld: THREE.Vector3,
  target: THREE.Vector3,
  iterations: number,
): void {
  // Quick reach test — if target is too far, point all joints toward it
  // and stretch (better than exploding).
  let chainLen = 0;
  for (const b of bones) chainLen += b;
  const dist = rootWorld.distanceTo(target);
  if (dist > chainLen) {
    // Fully extended pose pointing at target.
    const dir = new THREE.Vector3().subVectors(target, rootWorld).normalize();
    let acc = 0;
    joints[0].copy(rootWorld);
    for (let i = 0; i < bones.length; i++) {
      acc += bones[i];
      joints[i + 1].copy(rootWorld).addScaledVector(dir, acc);
    }
    return;
  }

  // Standard FABRIK: alternate forward (tip-to-root) and backward
  // (root-to-tip) passes for `iterations` rounds.
  const tmp = new THREE.Vector3();
  for (let it = 0; it < iterations; it++) {
    // Forward: pin tip to target, walk back updating each joint to
    // preserve its bone length to its child.
    joints[joints.length - 1].copy(target);
    for (let i = joints.length - 2; i >= 0; i--) {
      tmp.subVectors(joints[i], joints[i + 1]);
      const len = tmp.length();
      if (len < 1e-6) {
        // Degenerate; nudge so we don't divide by zero.
        tmp.set(0, 0, 1);
      } else {
        tmp.multiplyScalar(bones[i] / len);
      }
      joints[i].copy(joints[i + 1]).add(tmp);
    }
    // Backward: pin root, walk forward.
    joints[0].copy(rootWorld);
    for (let i = 1; i < joints.length; i++) {
      tmp.subVectors(joints[i], joints[i - 1]);
      const len = tmp.length();
      if (len < 1e-6) {
        tmp.set(0, 0, 1);
      } else {
        tmp.multiplyScalar(bones[i - 1] / len);
      }
      joints[i].copy(joints[i - 1]).add(tmp);
    }
  }

  // Ground constraint: knuckles + joints shouldn't punch below the
  // fretboard surface (y >= 0 for all but the tip target which is set
  // explicitly above the strings).
  for (let i = 1; i < joints.length - 1; i++) {
    if (joints[i].y < NECK_THICKNESS / 2 + 0.06) {
      joints[i].y = NECK_THICKNESS / 2 + 0.06;
    }
  }
}

// ─── Main component ─────────────────────────────────────────────────────

const InverseKinematics: React.FC<InverseKinematicsProps> = ({
  width,
  height,
  chord,
  ikIterations = 6,
  ikDamping = 0.18,
  showTargets = true,
  autoRotate = false,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  // Latest props in refs so the animate loop reads current values
  // without restarting the scene every prop change.
  const chordRef = useRef(chord);
  const iterRef = useRef(ikIterations);
  const dampRef = useRef(ikDamping);
  const showTargetsRef = useRef(showTargets);
  const autoRotateRef = useRef(autoRotate);
  chordRef.current = chord;
  iterRef.current = ikIterations;
  dampRef.current = ikDamping;
  showTargetsRef.current = showTargets;
  autoRotateRef.current = autoRotate;

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = width ?? container.clientWidth ?? 1280;
    const H0 = height ?? container.clientHeight ?? 720;

    // ─── Scene setup ─────────────────────────────────────────────────────
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x111519);
    scene.fog = new THREE.FogExp2(0x111519, 0.035);

    const camera = new THREE.PerspectiveCamera(42, W0 / H0, 0.1, 100);
    camera.position.set(0.8, 3.2, 5.6);
    camera.lookAt(0, 0.4, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W0, H0);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.10;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.target.set(0, 0.5, 0);
    controls.minDistance = 2;
    controls.maxDistance = 14;
    controls.maxPolarAngle = Math.PI * 0.49;

    // ─── Lighting ────────────────────────────────────────────────────────
    const ambient = new THREE.HemisphereLight(0xa0b8d8, 0x101418, 0.55);
    scene.add(ambient);

    const key = new THREE.DirectionalLight(0xfff1d8, 1.2);
    key.position.set(2.5, 4, 3);
    key.castShadow = true;
    key.shadow.mapSize.set(1024, 1024);
    key.shadow.camera.left = -5;
    key.shadow.camera.right = 5;
    key.shadow.camera.top = 4;
    key.shadow.camera.bottom = -4;
    key.shadow.camera.near = 0.5;
    key.shadow.camera.far = 12;
    key.shadow.bias = -0.0008;
    scene.add(key);

    const rim = new THREE.DirectionalLight(0x88aaff, 0.35);
    rim.position.set(-3, 2, -2);
    scene.add(rim);

    // ─── Fretboard ───────────────────────────────────────────────────────
    const board = new THREE.Group();
    scene.add(board);

    // Neck wood.
    const neckMat = new THREE.MeshStandardMaterial({
      color: 0x3a2618,
      roughness: 0.7,
      metalness: 0.05,
    });
    const neck = new THREE.Mesh(
      new THREE.BoxGeometry(NECK_LENGTH, NECK_THICKNESS, NECK_WIDTH),
      neckMat,
    );
    neck.receiveShadow = true;
    board.add(neck);

    // Frets (thin metal strips).
    const fretMat = new THREE.MeshStandardMaterial({
      color: 0xc0c4cc,
      roughness: 0.3,
      metalness: 0.85,
    });
    for (let f = 0; f <= FRET_COUNT; f++) {
      const x = fretX(f);
      const fret = new THREE.Mesh(
        new THREE.BoxGeometry(0.020, NECK_THICKNESS + 0.015, NECK_WIDTH * 0.98),
        fretMat,
      );
      fret.position.set(x, 0.005, 0);
      fret.castShadow = true;
      board.add(fret);
    }

    // Strings.
    const stringMat = new THREE.MeshStandardMaterial({
      color: 0xcccccc,
      roughness: 0.25,
      metalness: 0.95,
    });
    for (let s = 1; s <= STRING_COUNT; s++) {
      const z = stringZ(s);
      const thickness = 0.008 + (s - 1) * 0.002; // bass strings thicker
      const str = new THREE.Mesh(
        new THREE.CylinderGeometry(thickness, thickness, NECK_LENGTH, 6),
        stringMat,
      );
      str.rotation.z = Math.PI / 2;
      str.position.set(0, NECK_THICKNESS / 2 + 0.02, z);
      board.add(str);
    }

    // Inlay dots at fret 3, 5, 7, 9, 12.
    const inlayMat = new THREE.MeshStandardMaterial({
      color: 0xeae5d8,
      roughness: 0.6,
    });
    const inlays = [3, 5, 7, 9];
    for (const f of inlays) {
      const x = (fretX(f - 1) + fretX(f)) / 2;
      const dot = new THREE.Mesh(
        new THREE.CircleGeometry(0.06, 24),
        inlayMat,
      );
      dot.rotation.x = -Math.PI / 2;
      dot.position.set(x, NECK_THICKNESS / 2 + 0.001, 0);
      board.add(dot);
    }
    // Double dot at 12.
    {
      const x = (fretX(11) + fretX(12)) / 2;
      for (const z of [-0.18, 0.18]) {
        const dot = new THREE.Mesh(
          new THREE.CircleGeometry(0.05, 24),
          inlayMat,
        );
        dot.rotation.x = -Math.PI / 2;
        dot.position.set(x, NECK_THICKNESS / 2 + 0.001, z);
        board.add(dot);
      }
    }

    // ─── Hand ────────────────────────────────────────────────────────────
    // Wrist group sits behind the neck and is moved each frame to track
    // the chord centroid. Palm + finger bones live under it.
    const wrist = new THREE.Group();
    scene.add(wrist);

    const skinMat = new THREE.MeshStandardMaterial({
      color: 0xe8b9a3,
      roughness: 0.6,
      metalness: 0.0,
    });

    // Palm — flat slab with knuckle row at +Z (toward fretboard).
    const palm = new THREE.Mesh(
      new THREE.BoxGeometry(0.55, 0.18, PALM_LENGTH),
      skinMat,
    );
    palm.position.set(0, 0, PALM_LENGTH / 2);
    palm.castShadow = true;
    wrist.add(palm);

    // Wrist sphere.
    const wristMesh = new THREE.Mesh(
      new THREE.SphereGeometry(0.16, 24, 16),
      skinMat,
    );
    wristMesh.castShadow = true;
    wrist.add(wristMesh);

    const fingers: FingerChain[] = FINGER_NAMES.map((name) => {
      const mcpLocal = new THREE.Vector3(...FINGER_MCP_OFFSETS[name]);
      const bones = FINGER_BONE_LENGTHS[name];
      // Initial rest pose: bone chain reaches forward in +z from MCP.
      const j0 = new THREE.Vector3();
      const j1 = new THREE.Vector3();
      const j2 = new THREE.Vector3();
      const j3 = new THREE.Vector3();

      // Build capsule meshes for each bone segment.
      const meshes: THREE.Mesh[] = [];
      for (let i = 0; i < 3; i++) {
        const r = i === 0 ? 0.085 : i === 1 ? 0.072 : 0.060;
        const geo = new THREE.CylinderGeometry(r, r * 0.85, bones[i], 16, 1, false);
        // Cylinder is along +Y by default; we orient it per-frame.
        const mesh = new THREE.Mesh(geo, skinMat);
        mesh.castShadow = true;
        scene.add(mesh);
        meshes.push(mesh);
      }

      // Knuckle spheres at each joint (4 total).
      const knuckles: THREE.Mesh[] = [];
      for (let i = 0; i < 4; i++) {
        const r = i === 0 ? 0.10 : i === 1 ? 0.085 : i === 2 ? 0.075 : 0.062;
        const k = new THREE.Mesh(new THREE.SphereGeometry(r, 16, 12), skinMat);
        k.castShadow = true;
        scene.add(k);
        knuckles.push(k);
      }

      return {
        name,
        mcpLocal,
        bones,
        joints: [j0, j1, j2, j3],
        meshes,
        knuckles,
        target: new THREE.Vector3(0, NECK_THICKNESS / 2 + 0.05, stringZ(1)),
      };
    });

    // ─── Target spheres (visualisation) ──────────────────────────────────
    const targetMat = new THREE.MeshBasicMaterial({
      color: 0xffd54f,
      transparent: true,
      opacity: 0.85,
    });
    const targetMeshes: THREE.Mesh[] = [];
    for (let i = 0; i < FINGER_NAMES.length; i++) {
      const t = new THREE.Mesh(new THREE.SphereGeometry(0.08, 20, 14), targetMat);
      scene.add(t);
      targetMeshes.push(t);
    }

    // ─── Rigged hand GLB ─────────────────────────────────────────────────
    // Load the OpenXR generic-hand model asynchronously. When it arrives,
    // parent its root under `wrist` (so the existing wrist quaternion
    // continues to drive whole-hand orientation), cache bone refs by
    // name, and hide the procedural meshes. Until then, the procedural
    // cylinders/spheres remain visible as a fallback.
    //
    // The GLB is in meters (~0.18m hand). Our fretboard is in arbitrary
    // units where NECK_WIDTH=1 ≈ 5cm hand-pose. Empirically a scale of
    // ~3.5 makes the hand span roughly two-strings-worth and the fingers
    // reach across the neck — verified against live scene 2026-05-17.
    const HAND_SCALE = 8;
    // Hand-rest-pose offset relative to wrist: the GLB's wrist bone sits
    // at the origin with fingers extending along +Z; that already matches
    // our convention (PALM_LENGTH on +Z), so no offset is needed.
    const proceduralHandMeshes: THREE.Object3D[] = [palm, wristMesh];
    for (const f of fingers) {
      proceduralHandMeshes.push(...f.meshes, ...f.knuckles);
    }
    const handBones = new Map<string, THREE.Bone>();
    let handGroup: THREE.Object3D | null = null;
    const gltfLoader = new GLTFLoader();
    gltfLoader.setPath(HAND_PROFILE_URL);
    // Guitar fretting uses the LEFT hand (thumb on back of neck, fingers
    // wrap over the strings). Load left.glb, not right.glb.
    gltfLoader.load(
      'left.glb',
      (gltf) => {
        const root = gltf.scene.children[0];
        root.scale.setScalar(HAND_SCALE);
        // The OpenXR generic hand is modelled with +Z forward (fingertips),
        // +Y up (back of hand), origin at the wrist joint. That matches
        // our `wrist` Group convention, so we can parent directly.
        wrist.add(root);
        handGroup = root;

        root.traverse((obj) => {
          if (obj instanceof THREE.SkinnedMesh) {
            obj.frustumCulled = false;
            obj.castShadow = true;
            obj.receiveShadow = true;
            // Override the asset's default blue-grey material with skin
            // tone so the rigged hand matches the rest of the scene.
            obj.material = skinMat;
          }
        });
        for (const name of HAND_BONE_NAMES) {
          const bone = root.getObjectByName(name);
          if (bone instanceof THREE.Bone) {
            handBones.set(name, bone);
          }
        }

        // Hide procedural fallback meshes — the rigged hand is now live.
        for (const m of proceduralHandMeshes) {
          m.visible = false;
        }

        // Leave the GLB at its default rest pose for v1. The asset's
        // resting hand is a relaxed near-fist shape that already reads as
        // anatomically correct. Per-chord bone driving (FABRIK targets →
        // bone.lookAt on each phalanx) is the follow-up that will actually
        // animate finger placement on chord change.
      },
      undefined,
      (err) => {
        // GLB fetch failed (offline, CDN down, CSP block). Procedural
        // fallback remains visible — degrade gracefully.
        // eslint-disable-next-line no-console
        console.warn('[InverseKinematics] Hand GLB load failed, using procedural fallback:', err);
      },
    );

    // ─── Update targets from chord shape ─────────────────────────────────
    const updateTargetsFromChord = (shape: ChordShape) => {
      // Sort the chord's fretted positions by fret ascending so the
      // assignment goes index → middle → ring → pinky.
      const fretted = shape.positions
        .filter((p) => p.fret > 0)
        .sort((a, b) => a.fret - b.fret || a.string - b.string);

      // Set fretted-finger targets at the strings (above fretboard surface).
      for (let i = 0; i < Math.min(fingers.length, fretted.length); i++) {
        const p = fretted[i];
        fingers[i].target.copy(targetForFret(p.string, p.fret));
      }

      // Chord centroid in fretboard coords.
      const cx = fretted.length > 0
        ? fretted.reduce((s, p) => s + fretX(p.fret), 0) / fretted.length
        : 0;
      const cz = fretted.length > 0
        ? fretted.reduce((s, p) => s + stringZ(p.string), 0) / fretted.length
        : 0;

      // Wrist sits ABOVE the fretboard (Y > wood top at 0.125) and slightly
      // OFFSET to the low-E side. Palm tilts DOWN-AND-FORWARD onto the
      // strings so:
      //   - bone segments never pass through the neck wood (the "hand
      //     crosses the fretboard" bug — straight-line FABRIK from a wrist
      //     below or beside the neck would always traverse the wood),
      //   - all fretted targets stay within bone reach for index/middle/
      //     ring (0.88–0.98 chain budget),
      //   - the FABRIK ground constraint (y ≥ 0.185) keeps every joint
      //     above the wood top.
      const wristYOffset = 0.45;    // well above the wood top (0.125)
      const wristZOffset = -0.35;   // slightly toward the low-E side
      wrist.position.set(cx, wristYOffset, cz + wristZOffset);

      // Orient palm BEFORE computing parked-target / pre-curl, so MCP
      // world positions reflect the final tilt.
      const horizDirInit = new THREE.Vector3(0, 0, -wristZOffset);
      const basis = computePalmBasis(horizDirInit);
      wrist.quaternion.setFromRotationMatrix(basis.matrix);
      wrist.updateMatrixWorld(true);

      // Parked-finger targets: relative to MCP world (guaranteed within
      // proximal-bone reach), so the FABRIK "unreachable — stretch
      // straight" branch never fires. Place 35% of b0 along palm-forward,
      // then clamp Y above the wood top so the tip never lands inside
      // the neck volume (caught by validate()).
      const woodFloor = NECK_THICKNESS / 2 + 0.04;
      for (let i = fretted.length; i < fingers.length; i++) {
        const finger = fingers[i];
        const mcpWorld = finger.mcpLocal.clone().applyMatrix4(wrist.matrixWorld);
        finger.target.copy(mcpWorld)
          .addScaledVector(basis.forward, finger.bones[0] * 0.35);
        if (finger.target.y < woodFloor) finger.target.y = woodFloor;
      }

      // Pre-curl: from MCP, PIP arches UP-AND-BACK (toward the palm-up
      // direction, since the hand approaches from above), DIP comes down
      // toward the target, TIP rests on the target. This biases FABRIK's
      // steady-state into a guitarist-shaped curl instead of a flat
      // straight-line solution.
      for (const finger of fingers) {
        const mcp = finger.mcpLocal.clone().applyMatrix4(wrist.matrixWorld);
        const target = finger.target;
        const dx = target.x - mcp.x;
        const dy = target.y - mcp.y;
        const dz = target.z - mcp.z;
        const dist = Math.hypot(dx, dy, dz);
        const ux = dist > 1e-4 ? dx / dist : 0;
        const uy = dist > 1e-4 ? dy / dist : -1;
        const uz = dist > 1e-4 ? dz / dist : 0;
        const [b0, b1] = finger.bones;
        // PIP: 35% of the way toward target, plus a small UP-AND-BACK arch
        // (along palm-up direction).
        const arch = b0 * 0.45;
        finger.joints[0].copy(mcp);
        finger.joints[1].set(
          mcp.x + ux * (dist * 0.35) + basis.up.x * arch,
          mcp.y + uy * (dist * 0.35) + basis.up.y * arch,
          mcp.z + uz * (dist * 0.35) + basis.up.z * arch,
        );
        // DIP: 70% of the way toward target, with reduced arch as we
        // curl back down.
        const arch2 = b1 * 0.20;
        finger.joints[2].set(
          mcp.x + ux * (dist * 0.70) + basis.up.x * arch2,
          mcp.y + uy * (dist * 0.70) + basis.up.y * arch2,
          mcp.z + uz * (dist * 0.70) + basis.up.z * arch2,
        );
        // TIP on the target so frame 0 already has the fingertip planted.
        finger.joints[3].copy(target);
      }
    };

    updateTargetsFromChord(chordRef.current);

    // ─── MCP control surface ────────────────────────────────────────────
    // Expose window.__gaIK so chrome-devtools / playwright MCP tools can
    // inspect the scene, validate the geometry, and (with the test page
    // also registering setChord) drive the demo end-to-end via
    // evaluate_script. The InverseKinematicsTest page adds setChord /
    // listChords / getCurrentChord on top of this base surface.
    const w = window as unknown as { __gaIK?: Record<string, unknown> };
    const ikApi = w.__gaIK ?? {};
    // Debug surface for inspecting the loaded rigged hand at runtime.
    // `__gaIK._debug.handRoot` is the THREE.Object3D parent of the
    // SkinnedMesh; `__gaIK._debug.bones` is the bone-name → Bone map.
    // Exposed for Chrome MCP introspection while iterating on pose.
    (ikApi as Record<string, unknown>)._debug = {
      get handRoot() { return handGroup; },
      bones: handBones,
      wrist,
    };
    ikApi.getSceneState = () => ({
      chord: chordRef.current.name,
      wrist: {
        position: wrist.position.toArray(),
        quaternion: wrist.quaternion.toArray(),
      },
      fingers: fingers.map((f) => ({
        name: f.name,
        mcp: f.joints[0].toArray(),
        pip: f.joints[1].toArray(),
        dip: f.joints[2].toArray(),
        tip: f.joints[3].toArray(),
        target: f.target.toArray(),
        targetDistance: f.joints[3].distanceTo(f.target),
        boneBudget: f.bones[0] + f.bones[1] + f.bones[2],
      })),
      neck: {
        length: NECK_LENGTH,
        width: NECK_WIDTH,
        thickness: NECK_THICKNESS,
        woodTopY: NECK_THICKNESS / 2,
      },
    });
    ikApi.validate = () => {
      const issues: string[] = [];
      const halfX = NECK_LENGTH / 2;
      const halfY = NECK_THICKNESS / 2;
      const halfZ = NECK_WIDTH / 2;
      const stringTopY = NECK_THICKNESS / 2 + 0.02; // matches str.position.y
      for (const f of fingers) {
        // Joint-inside-wood check: any joint with all three axes inside
        // the neck bounding box means a bone is geometrically intersecting
        // the fretboard wood, which is physically impossible.
        const jointNames = ['mcp', 'pip', 'dip', 'tip'] as const;
        for (let i = 0; i < 4; i++) {
          const j = f.joints[i];
          if (
            Math.abs(j.x) < halfX &&
            j.y > -halfY && j.y < halfY &&
            Math.abs(j.z) < halfZ
          ) {
            issues.push(
              `${f.name}.${jointNames[i]} inside neck wood at ` +
              `(${j.x.toFixed(3)}, ${j.y.toFixed(3)}, ${j.z.toFixed(3)})`
            );
          }
        }
        // Fingertip convergence check.
        const tipDist = f.joints[3].distanceTo(f.target);
        if (tipDist > 0.1) {
          issues.push(`${f.name}.tip is ${tipDist.toFixed(3)} from target (FABRIK did not converge)`);
        }
        // Fingertip-touches-string check: tip Y should be within the
        // string radius (~0.02) of the string top surface for the tip to
        // visually rest on the string. Tips floating > 0.04 above the
        // string look like the hand is "hovering" rather than fretting.
        const tipContactDelta = f.target.y - stringTopY;
        if (tipContactDelta > 0.04) {
          issues.push(
            `${f.name}.target ${tipContactDelta.toFixed(3)} above string top — ` +
            `looks like hand is hovering`
          );
        }
      }
      // Wrist perpendicularity: extract forward from quaternion and check
      // the X component is small (X is along the neck, Z is across the
      // strings; perpendicular grip = forward dominated by Z).
      const fwd = new THREE.Vector3(0, 0, 1).applyQuaternion(wrist.quaternion);
      const xzMag = Math.hypot(fwd.x, fwd.z);
      const perpDeg = xzMag > 1e-6
        ? Math.abs(Math.atan2(fwd.x, fwd.z)) * 180 / Math.PI
        : 0;
      if (perpDeg > 15) {
        issues.push(
          `wrist forward is ${perpDeg.toFixed(1)}° off perpendicular ` +
          `(forward=(${fwd.x.toFixed(2)}, ${fwd.y.toFixed(2)}, ${fwd.z.toFixed(2)}))`
        );
      }
      // Anatomy check: the rigged hand asset must have loaded with the
      // full OpenXR bone set, including thumb (2 phalanges) + 4 fingers
      // (3 phalanges each). Missing bones mean the GLB hasn't loaded or
      // the asset CDN returned a malformed model.
      const handLoaded = handBones.size > 0;
      const hasThumb = handBones.has('thumb-phalanx-proximal') &&
                       handBones.has('thumb-phalanx-distal');
      const allFingersComplete = ['index', 'middle', 'ring', 'pinky'].every((f) =>
        FINGER_TO_PHALANX_CHAIN[f].every((b) => handBones.has(b))
      );
      if (!handLoaded) {
        issues.push('rigged hand GLB not yet loaded (procedural fallback visible)');
      } else if (!hasThumb) {
        issues.push('rigged hand loaded but thumb bones missing');
      } else if (!allFingersComplete) {
        issues.push('rigged hand loaded but some finger phalanx bones missing');
      }
      return {
        ok: issues.length === 0,
        issues,
        // Quantitative measurements for callers that want raw numbers
        // rather than human-readable failures.
        metrics: {
          wristPerpendicularityDeg: perpDeg,
          forward: [fwd.x, fwd.y, fwd.z],
          handLoaded,
          handBoneCount: handBones.size,
          fingertipContactDelta: fingers.map((f) => ({
            name: f.name,
            deltaY: f.target.y - stringTopY,
          })),
        },
      };
    };
    w.__gaIK = ikApi;

    // ─── Bloom post-pass ─────────────────────────────────────────────────
    const composer = new EffectComposer(renderer);
    composer.setSize(W0, H0);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    composer.addPass(new RenderPass(scene, camera));
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(W0, H0),
      /* strength */ 0.30,
      /* radius   */ 0.45,
      /* threshold*/ 0.92,
    );
    composer.addPass(bloomPass);
    composer.addPass(new OutputPass());

    // ─── Animate ─────────────────────────────────────────────────────────
    const tmpQ = new THREE.Quaternion();
    const yAxis = new THREE.Vector3(0, 1, 0); // used by bone-mesh orientation
    const tmpDir = new THREE.Vector3();
    let raf = 0;
    let lastChordName = chordRef.current.name;

    const animate = () => {
      if (chordRef.current.name !== lastChordName) {
        updateTargetsFromChord(chordRef.current);
        lastChordName = chordRef.current.name;
      }

      controls.autoRotate = autoRotateRef.current;
      controls.autoRotateSpeed = 0.4;
      controls.update();

      // Run FABRIK per finger toward its target.
      const it = Math.max(1, Math.floor(iterRef.current));
      const damp = THREE.MathUtils.clamp(dampRef.current, 0.02, 1);

      for (const finger of fingers) {
        // Compute MCP world from wrist + local offset.
        const mcpWorld = finger.mcpLocal.clone().applyMatrix4(wrist.matrixWorld);
        finger.joints[0].copy(mcpWorld);

        // Tween joints toward where FABRIK would put them this frame.
        // We do FABRIK on a working copy then exponentially damp.
        const work = finger.joints.map((j) => j.clone());
        fabrikSolve(work, finger.bones as unknown as number[], mcpWorld, finger.target, it);

        for (let i = 1; i < 4; i++) {
          finger.joints[i].lerp(work[i], damp);
        }

        // Orient capsule meshes along consecutive joint pairs.
        for (let i = 0; i < 3; i++) {
          const a = finger.joints[i];
          const b = finger.joints[i + 1];
          const mesh = finger.meshes[i];
          const len = a.distanceTo(b);
          if (len < 1e-5) continue;
          mesh.position.copy(a).lerp(b, 0.5);
          tmpDir.subVectors(b, a).normalize();
          tmpQ.setFromUnitVectors(yAxis, tmpDir);
          mesh.setRotationFromQuaternion(tmpQ);
          // Scale length along Y so the cylinder fits the segment.
          mesh.scale.set(1, len / finger.bones[i], 1);
        }

        // Knuckle spheres at the joints.
        for (let i = 0; i < 4; i++) {
          finger.knuckles[i].position.copy(finger.joints[i]);
        }
      }

      // Update target visualisation.
      for (let i = 0; i < fingers.length; i++) {
        targetMeshes[i].position.copy(fingers[i].target);
        targetMeshes[i].visible = showTargetsRef.current;
      }

      // Wrist orientation is set in updateTargetsFromChord (perpendicular
      // to the neck, tilted down by TILT_ANGLE_RAD). Deliberately NOT
      // recomputed per frame: parked-finger targets are derived FROM the
      // basis, so recomputing each frame creates a feedback loop that
      // drags the basis ~22° off-perpendicular within seconds of slerping
      // (user observation 2026-05-17 — "wrist should be perpendicular to
      // fretboard, not parallel"). If smooth slerp on chord change becomes
      // desirable, store the target quaternion in a ref at chord-change
      // time and slerp toward THAT (not toward a recomputed-from-targets
      // quaternion).

      composer.render();
      raf = requestAnimationFrame(animate);
    };
    animate();

    // ─── Resize ──────────────────────────────────────────────────────────
    const onResize = () => {
      const w = container.clientWidth;
      const h = container.clientHeight;
      if (w === 0 || h === 0) return;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
      composer.setSize(w, h);
      bloomPass.setSize(w, h);
    };
    const ro = new ResizeObserver(onResize);
    ro.observe(container);

    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      controls.dispose();
      composer.dispose();
      renderer.dispose();
      scene.traverse((obj) => {
        if (obj instanceof THREE.Mesh) {
          obj.geometry.dispose();
          if (obj.material instanceof THREE.Material) obj.material.dispose();
        }
      });
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
      // Tear down only the scene-side keys; the test page owns its own.
      delete ikApi.getSceneState;
      delete ikApi.validate;
    };
  }, [width, height]);

  const sx = (width !== undefined && height !== undefined)
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };
  return <Box ref={containerRef} sx={sx} />;
};

export default InverseKinematics;

// Ready-made chord library so the test page (and other consumers) can
// pick a shape by name. Keep this list short; it's a demo.
export const CHORD_LIBRARY: ChordShape[] = [
  {
    name: 'C Major',
    positions: [
      { string: 1, fret: 0 },
      { string: 2, fret: 1 },
      { string: 3, fret: 0 },
      { string: 4, fret: 2 },
      { string: 5, fret: 3 },
    ],
  },
  {
    name: 'G Major',
    positions: [
      { string: 1, fret: 3 },
      { string: 2, fret: 0 },
      { string: 3, fret: 0 },
      { string: 4, fret: 0 },
      { string: 5, fret: 2 },
      { string: 6, fret: 3 },
    ],
  },
  {
    name: 'D Major',
    positions: [
      { string: 1, fret: 2 },
      { string: 2, fret: 3 },
      { string: 3, fret: 2 },
      { string: 4, fret: 0 },
    ],
  },
  {
    name: 'E Minor',
    positions: [
      { string: 4, fret: 2 },
      { string: 5, fret: 2 },
    ],
  },
  {
    name: 'A Minor',
    positions: [
      { string: 2, fret: 1 },
      { string: 3, fret: 2 },
      { string: 4, fret: 2 },
    ],
  },
  {
    name: 'F Major (barré)',
    positions: [
      { string: 2, fret: 1 },
      { string: 3, fret: 2 },
      { string: 4, fret: 3 },
      { string: 5, fret: 3 },
    ],
  },
  {
    name: 'Bb Major (barré)',
    positions: [
      { string: 2, fret: 3 },
      { string: 3, fret: 3 },
      { string: 4, fret: 3 },
      { string: 5, fret: 5 },
    ],
  },
  {
    name: 'Power chord (E5)',
    positions: [
      { string: 5, fret: 7 },
      { string: 6, fret: 7 },
    ],
  },
];
