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
import { Box } from '@mui/material';

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

    // ─── Update targets from chord shape ─────────────────────────────────
    const updateTargetsFromChord = (shape: ChordShape) => {
      // Sort the chord's fretted positions by fret ascending so the
      // assignment goes index → middle → ring → pinky.
      const fretted = shape.positions
        .filter((p) => p.fret > 0)
        .sort((a, b) => a.fret - b.fret || a.string - b.string);

      for (let i = 0; i < fingers.length; i++) {
        const finger = fingers[i];
        if (i < fretted.length) {
          const p = fretted[i];
          finger.target.copy(targetForFret(p.string, p.fret));
        } else {
          // Park unused fingers above the fretboard near the lowest fret.
          const restFret = Math.max(1, (fretted[0]?.fret ?? 1));
          const restString = 1 + i; // line them up over high strings
          finger.target.copy(targetForFret(restString, restFret));
          finger.target.y += 0.4 + i * 0.1; // float up a little
        }
      }

      // Wrist position: align under the centroid of fretted positions,
      // sitting on the low-E side of the neck so fingers reach UP + OVER.
      //
      // Previous offsets (Y=-0.4, Z=-0.6) put the wrist farther than the
      // total bone length (0.88 max for index = 0.42+0.26+0.20). FABRIK
      // then fell into its "target unreachable — point + stretch" branch
      // and drew fingers as straight lines that overshot the fretboard,
      // producing the "fingers across the neck" report. New offsets keep
      // every target reachable for the bundled CHORD_LIBRARY voicings.
      const cx = fretted.length > 0
        ? fretted.reduce((s, p) => s + fretX(p.fret), 0) / fretted.length
        : 0;
      const cz = fretted.length > 0
        ? fretted.reduce((s, p) => s + stringZ(p.string), 0) / fretted.length
        : 0;
      wrist.position.set(cx + 0.25, -0.05, cz - 0.35);

      // Initialize each finger's joint chain in a pre-curled "above
      // the fretboard" pose. FABRIK has no joint constraints — it
      // finds the shortest 3D path from MCP to target, which routes
      // UNDER the fretboard (Y < 0) for the natural straight-line
      // solution. Pre-curling biases the steady state UP-AND-OVER:
      // PIP arches up, DIP comes back down, TIP rests on the target.
      // Without this, the lerp-from-zero start meant FABRIK never
      // converged to a guitarist-shaped curl.
      wrist.updateMatrixWorld(true);
      for (const finger of fingers) {
        const mcp = finger.mcpLocal.clone().applyMatrix4(wrist.matrixWorld);
        const target = finger.target;
        // Vector from MCP to target in the horizontal plane.
        const dx = target.x - mcp.x;
        const dz = target.z - mcp.z;
        const horiz = Math.hypot(dx, dz);
        const fx = horiz > 1e-4 ? dx / horiz : 0;
        const fz = horiz > 1e-4 ? dz / horiz : 1;
        // Bone lengths.
        const [b0, b1, b2] = finger.bones;
        // PIP arched up + 40% of horizontal travel.
        finger.joints[0].copy(mcp);
        finger.joints[1].set(
          mcp.x + fx * (horiz * 0.4),
          mcp.y + b0 * 0.7,
          mcp.z + fz * (horiz * 0.4),
        );
        // DIP at peak of arc, ~75% of horizontal travel.
        finger.joints[2].set(
          mcp.x + fx * (horiz * 0.75),
          mcp.y + b0 * 0.7 + b1 * 0.4,
          mcp.z + fz * (horiz * 0.75),
        );
        // TIP on the target (so first frame already shows fingertip on fret).
        finger.joints[3].copy(target);
        // Knuckle / mesh transforms catch up the next animation frame.
      }
    };

    updateTargetsFromChord(chordRef.current);

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
    const tmpV = new THREE.Vector3();
    const tmpQ = new THREE.Quaternion();
    const yAxis = new THREE.Vector3(0, 1, 0);
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

      // Smoothed wrist orientation. Use a CONSTRAINED basis instead of
      // `setFromUnitVectors`: the latter aligns local +Z with the target
      // direction but leaves the local +X (knuckle-row axis) free to roll
      // arbitrarily, which used to swing fingers across the neck.
      //
      // Build an explicit basis anchored to the fretboard surface:
      //   local +Y = world +Y  (palm-back perpendicular to fretboard; flat)
      //   local +Z = horizontal direction toward the chord centroid (palm
      //              forward, toward strings — projected onto the XZ plane
      //              so the palm never tilts off the fretboard)
      //   local +X = +Y × +Z   (knuckle-row axis spans across strings)
      //
      // Then slerp toward that basis so chord changes still animate.
      tmpDir.set(
        fingers[0].target.x - wrist.position.x,
        0, // <-- zero the Y component to keep the palm flat
        fingers[0].target.z - wrist.position.z,
      );
      if (tmpDir.lengthSq() > 1e-6) {
        tmpDir.normalize();
        const palmForward = tmpDir;
        const palmUp = yAxis;
        const palmRight = new THREE.Vector3().crossVectors(palmUp, palmForward).normalize();
        const basis = new THREE.Matrix4().makeBasis(palmRight, palmUp, palmForward);
        tmpQ.setFromRotationMatrix(basis);
        wrist.quaternion.slerp(tmpQ, 0.08);
      }

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
