/**
 * TonalOrbit — music-theory hierarchy reimagined as a planetary system.
 *
 * Visual concept
 * ──────────────
 *   - Centre: a glowing tonic "star" that pulses with the focused root.
 *   - Inner orbit (12 bodies): pitch classes in circle-of-fifths order.
 *   - Mid orbit (8 bodies): chord families around the focused pitch.
 *   - Outer orbit (7 bodies): scale / modal families around the focused
 *     chord.
 *
 * All bodies orbit slowly. Tapping a body focuses it: the camera glides,
 * the audio drone gains a harmonic layer, and the next-deeper ring fades
 * in around the focused body. A breadcrumb (rendered by the parent) and
 * the `?tour=auto` URL param drive a TV-friendly auto-tour.
 *
 * Tech
 * ────
 *   - Three.js (WebGL2), instanced-friendly geometry, OrbitControls for
 *     touch-friendly camera (pinch zoom, drag rotate, tap focus).
 *   - Optional bloom post-processing (`perf=high|medium`).
 *   - Particle motes between orbits (perf=high only).
 *   - All audio in raw Web Audio (../audio.ts); ../data.ts is the
 *     music-theory model.
 *
 * Performance
 * ───────────
 *   perf=high   → bloom + motes + shadow-free rim-light + full DPR
 *   perf=medium → bloom only, no motes, DPR capped at 1.25
 *   perf=low    → no post-processing, no motes, flat shaded materials
 *
 * The auto-detect (low on mobile, high on desktop) lives in
 * pages/TonalOrbitTest.tsx; URL param overrides it.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Box } from '@mui/material';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';

import {
  PITCH_BODIES,
  chordsForPitch,
  scalesForChord,
  keyChordsForPitch,
  keyModesForPitch,
  focusDepth,
  type Body,
  type PitchBody,
  type ChordBody,
  type ScaleBody,
  type FocusState,
  type ChordMode,
} from './data';
import { SPACE_TOP, SPACE_BOTTOM } from './palette';
import { TonalOrbitAudio } from './audio';

export type PerfMode = 'high' | 'medium' | 'low';

export interface TonalOrbitProps {
  /** Performance tier — controls post-processing, motes, DPR cap. */
  perf?: PerfMode;
  /** When true the camera auto-cycles through the hierarchy (Cast-friendly). */
  tour?: boolean;
  /**
   * Which chord/scale system the orbit shows:
   *   - 'root' (default): mid-orbit = 8 chord families on the focused
   *     pitch as root (Cmaj, Cm, C7, ...); outer-orbit = 7 scales
   *     curated per chord family. All share the focused pitch's root.
   *   - 'key': mid-orbit = 7 diatonic triads of the focused pitch's
   *     major key (I, ii, iii, IV, V, vi, vii° — each with its own
   *     root); outer-orbit = 7 modes of that major key, each rooted
   *     on its scale degree (Ionian, Dorian, Phrygian, ...).
   */
  chordMode?: ChordMode;
  /** Fires when the focus state changes (parent renders the HUD/breadcrumb). */
  onFocusChange?: (focus: FocusState) => void;
  /**
   * Imperative hook — parent calls this with a function that sets focus
   * manually (used by the breadcrumb's "step back" links). The function
   * accepts a depth: 0 = root, 1 = pitch only, 2 = pitch+chord, 3 = full.
   */
  onReady?: (api: TonalOrbitApi) => void;
}

export interface TonalOrbitApi {
  /** Step focus back to the given depth (0..2). */
  popTo: (depth: 0 | 1 | 2) => void;
  /** Toggle the slow orbital animation. */
  setOrbiting: (on: boolean) => void;
  /** Toggle audio drone. */
  setAudioEnabled: (on: boolean) => void;
  /**
   * Swap chord-mode in place. Rebuilds the chord (+ scale, in Mode B)
   * orbit around the focused pitch without tearing down the renderer,
   * star, lights, or starfield. If no pitch is focused yet this just
   * stores the new mode for the next focus.
   */
  setChordMode: (mode: ChordMode) => void;
  /**
   * Programmatic focus by pitch class (0..11), used by headless test
   * harnesses to capture the chord-orbit state without simulating
   * mouse coordinates onto a small planet target.
   */
  focusPitchByPc: (pc: number) => void;
}

// ─── Layout tunables ──────────────────────────────────────────────────────────
const PITCH_RADIUS = 7.0;
const CHORD_RADIUS = 12.5;
const SCALE_RADIUS = 17.5;

const PITCH_BODY_SIZE = 0.55;
const CHORD_BODY_SIZE = 0.45;
const SCALE_BODY_SIZE = 0.40;

// Orbital periods (seconds for one full revolution). Slow so the eye can rest.
const PITCH_PERIOD_S  = 220;
const CHORD_PERIOD_S  =  90;
const SCALE_PERIOD_S  =  55;

// Camera positions (radius from origin) for each focus depth. The camera
// moves to these on focus change with a smooth ease.
const CAMERA_DIST_BY_DEPTH = [42, 30, 22, 16];

// Tour timings — how long to dwell at each step before moving on.
const TOUR_DWELL_MS = {
  tonic: 6000,
  pitch: 5000,
  chord: 4500,
};

interface OrbitMesh {
  mesh: THREE.Mesh;
  body: Body;
  /** Anchor angle (radians) — base position on the orbit. */
  baseAngle: number;
  /** Parent body — null for pitches, pitch for chords, chord for scales. */
  parent: Body | null;
}

export const TonalOrbit: React.FC<TonalOrbitProps> = ({
  perf = 'high',
  tour = false,
  chordMode = 'root',
  onFocusChange,
  onReady,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  // Latest prop callbacks captured in a ref so the effect doesn't tear down
  // when the parent re-renders (the parent re-renders on focus change, which
  // would otherwise rebuild the entire scene).
  const callbacksRef = useRef({ onFocusChange, onReady });
  callbacksRef.current = { onFocusChange, onReady };
  const tourRef = useRef<boolean>(tour);
  tourRef.current = tour;
  // Mode is read via ref inside the scene closure so a prop change can
  // flow through without re-mounting Three. The actual orbit rebuild on
  // mode change happens in `api.setChordMode()` below.
  const chordModeRef = useRef<ChordMode>(chordMode);
  chordModeRef.current = chordMode;

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = container.clientWidth || window.innerWidth;
    const H0 = container.clientHeight || window.innerHeight;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(55, W0 / H0, 0.1, 500);
    camera.position.set(0, 14, CAMERA_DIST_BY_DEPTH[0]);

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
    renderer.setSize(W0, H0);
    const dprCap = perf === 'high' ? 1.75 : perf === 'medium' ? 1.25 : 1.0;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, dprCap));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.1;
    renderer.setClearColor(SPACE_BOTTOM, 1);
    container.appendChild(renderer.domElement);

    // Touch-friendly camera controls. OrbitControls supports pinch-zoom +
    // one-finger drag rotate out of the box. We disable pan so the tonic
    // always stays roughly centred.
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.enablePan = false;
    controls.minDistance = 8;
    controls.maxDistance = 80;
    controls.minPolarAngle = 0.15 * Math.PI;
    controls.maxPolarAngle = 0.65 * Math.PI;
    controls.target.set(0, 0, 0);

    // ─── Skybox — radial gradient deep space ───────────────────────────────
    const skyGeo = new THREE.SphereGeometry(220, 32, 24);
    const skyMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: {
        uTop:    { value: SPACE_TOP },
        uBottom: { value: SPACE_BOTTOM },
      },
      vertexShader: /* glsl */ `
        varying vec3 vDir;
        void main() {
          vDir = normalize(position);
          gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uTop;
        uniform vec3 uBottom;
        varying vec3 vDir;
        // Hash-based starfield — cheap, no texture.
        float hash21(vec2 p) {
          p = fract(p * vec2(123.34, 456.21));
          p += dot(p, p + 45.32);
          return fract(p.x * p.y);
        }
        void main() {
          float h = clamp(vDir.y * 0.5 + 0.5, 0.0, 1.0);
          vec3 col = mix(uBottom, uTop, smoothstep(0.0, 0.85, h));
          // Procedural stars — three frequency bands. Pixel-stable using
          // direction quantisation; not perfect-uniform but good enough for
          // a moving background. We add a soft glow around each star
          // (smoothstep against cell-local distance) so they look round
          // instead of pixel-squared in low-perf mode (no bloom).
          vec2 dirUV = vec2(atan(vDir.z, vDir.x), asin(vDir.y));
          for (int i = 0; i < 3; i++) {
            float scale = pow(2.0, float(i)) * 90.0;
            vec2 sUV = dirUV * scale;
            vec2 g = floor(sUV);
            vec2 f = fract(sUV);
            float r = hash21(g);
            if (r > 0.9985) {
              // Per-star local centre and softness — radial falloff.
              vec2 starCentre = vec2(0.5);
              float d = length(f - starCentre);
              float core = smoothstep(0.18, 0.0, d);
              float bri = (r - 0.9985) * 600.0 * core;
              // Subtle blue / white tint variation per star.
              vec3 tint = mix(vec3(0.85, 0.9, 1.0), vec3(1.0, 0.96, 0.86), fract(r * 17.0));
              col += tint * bri;
            }
          }
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const sky = new THREE.Mesh(skyGeo, skyMat);
    scene.add(sky);

    // ─── Lighting ──────────────────────────────────────────────────────────
    const ambient = new THREE.AmbientLight(0xffffff, 0.4);
    scene.add(ambient);

    // The "tonic star" doubles as a key point-light so bodies near it
    // get a soft rim.
    const starLight = new THREE.PointLight(0xfff2c4, 1.5, 50, 1.6);
    starLight.position.set(0, 0, 0);
    scene.add(starLight);

    // ─── Tonic star ────────────────────────────────────────────────────────
    // Two-layer: bright sphere + larger halo sprite. The sphere uses a
    // shader that pulses with the audio root.
    const starUniforms = {
      uPulse: { value: 0.0 },
      uColor: { value: new THREE.Color('#fff2c4') },
    };
    const starMat = new THREE.ShaderMaterial({
      uniforms: starUniforms,
      transparent: true,
      depthWrite: false,
      vertexShader: /* glsl */ `
        varying vec3 vNorm;
        void main() {
          vNorm = normalize(normal);
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: /* glsl */ `
        uniform float uPulse;
        uniform vec3 uColor;
        varying vec3 vNorm;
        void main() {
          float rim = pow(1.0 - max(0.0, vNorm.z), 2.5);
          float pulseBoost = 0.5 + 0.5 * uPulse;
          vec3 col = uColor * (0.85 + 0.6 * rim) * pulseBoost;
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const star = new THREE.Mesh(new THREE.SphereGeometry(1.2, 36, 24), starMat);
    scene.add(star);

    // Halo sprite — bigger soft glow around the star, also pulses.
    const haloCanvas = document.createElement('canvas');
    haloCanvas.width = 256;
    haloCanvas.height = 256;
    const haloCtx = haloCanvas.getContext('2d');
    if (haloCtx) {
      const grad = haloCtx.createRadialGradient(128, 128, 0, 128, 128, 128);
      grad.addColorStop(0,   'rgba(255, 240, 200, 0.85)');
      grad.addColorStop(0.3, 'rgba(255, 200, 140, 0.35)');
      grad.addColorStop(1,   'rgba(255, 200, 140, 0.0)');
      haloCtx.fillStyle = grad;
      haloCtx.fillRect(0, 0, 256, 256);
    }
    const haloTex = new THREE.CanvasTexture(haloCanvas);
    const haloMat = new THREE.SpriteMaterial({
      map: haloTex,
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    const halo = new THREE.Sprite(haloMat);
    halo.scale.set(6, 6, 1);
    scene.add(halo);

    // ─── Build pitch-orbit bodies (12 planets) ─────────────────────────────
    const orbitMeshes: OrbitMesh[] = [];
    const labelSprites = new Map<THREE.Mesh, THREE.Sprite>();

    // Anchor planes for the orbit rings — faint circles so the eye can
    // see the path each body sweeps.
    const addOrbitRing = (radius: number, color: number, opacity: number) => {
      const geom = new THREE.RingGeometry(radius - 0.025, radius + 0.025, 96, 1);
      const mat = new THREE.MeshBasicMaterial({
        color, transparent: true, opacity, side: THREE.DoubleSide, depthWrite: false,
      });
      const ring = new THREE.Mesh(geom, mat);
      ring.rotation.x = -Math.PI / 2;
      scene.add(ring);
      return ring;
    };
    const pitchRing = addOrbitRing(PITCH_RADIUS, 0x6488c8, 0.18);
    let chordRing: THREE.Mesh | null = null;
    let scaleRing: THREE.Mesh | null = null;

    const buildBody = (
      body: Body,
      size: number,
      baseAngle: number,
      orbitCentre: THREE.Vector3,
      parent: Body | null,
    ): OrbitMesh => {
      const geom = new THREE.SphereGeometry(size, 24, 18);
      // Shader material with rim-light + emissive base — cheap, looks great.
      const mat = new THREE.ShaderMaterial({
        uniforms: {
          uColor: { value: body.color.clone() },
          uHover: { value: 0.0 },
        },
        vertexShader: /* glsl */ `
          varying vec3 vNorm;
          varying vec3 vView;
          void main() {
            vNorm = normalize(normalMatrix * normal);
            vec4 mv = modelViewMatrix * vec4(position, 1.0);
            vView = normalize(-mv.xyz);
            gl_Position = projectionMatrix * mv;
          }
        `,
        fragmentShader: /* glsl */ `
          uniform vec3 uColor;
          uniform float uHover;
          varying vec3 vNorm;
          varying vec3 vView;
          void main() {
            float NdotV = max(0.0, dot(vNorm, vView));
            float rim = pow(1.0 - NdotV, 2.0);
            vec3 base = uColor * (0.55 + 0.35 * NdotV);
            vec3 col = base + uColor * rim * (0.6 + uHover * 0.6);
            // Boost overall brightness on hover for a clear affordance.
            col *= (1.0 + uHover * 0.35);
            gl_FragColor = vec4(col, 1.0);
          }
        `,
      });
      const mesh = new THREE.Mesh(geom, mat);
      mesh.position.copy(orbitCentre);
      mesh.userData.body = body;
      mesh.userData.parent = parent;
      mesh.userData.baseAngle = baseAngle;
      scene.add(mesh);

      // Label sprite — drawn to a canvas. Position is updated each frame
      // because the body's position moves.
      const sprite = makeLabelSprite(body.kind === 'pitch' ? (body as PitchBody).name
                                  : body.kind === 'chord' ? (body as ChordBody).displayName
                                  : (body as ScaleBody).scale.display);
      scene.add(sprite);
      labelSprites.set(mesh, sprite);

      return { mesh, body, baseAngle, parent };
    };

    // Build inner orbit (always present).
    PITCH_BODIES.forEach((pb, i) => {
      const baseAngle = (i / PITCH_BODIES.length) * Math.PI * 2;
      const x = Math.cos(baseAngle) * PITCH_RADIUS;
      const z = Math.sin(baseAngle) * PITCH_RADIUS;
      orbitMeshes.push(buildBody(pb, PITCH_BODY_SIZE, baseAngle, new THREE.Vector3(x, 0, z), null));
    });

    // ─── Particle motes (perf=high only) ───────────────────────────────────
    let motes: THREE.Points | null = null;
    if (perf === 'high') {
      const MOTE_COUNT = 600;
      const positions = new Float32Array(MOTE_COUNT * 3);
      const sizes = new Float32Array(MOTE_COUNT);
      for (let i = 0; i < MOTE_COUNT; i++) {
        // Sample within a torus-ish region around the orbits.
        const r = 5 + Math.random() * (SCALE_RADIUS + 4);
        const theta = Math.random() * Math.PI * 2;
        const y = (Math.random() - 0.5) * 2.2;
        positions[3 * i + 0] = Math.cos(theta) * r;
        positions[3 * i + 1] = y;
        positions[3 * i + 2] = Math.sin(theta) * r;
        sizes[i] = 0.6 + Math.random() * 1.4;
      }
      const moteGeom = new THREE.BufferGeometry();
      moteGeom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      moteGeom.setAttribute('aSize', new THREE.BufferAttribute(sizes, 1));
      const moteMat = new THREE.ShaderMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        uniforms: { uTime: { value: 0 } },
        vertexShader: /* glsl */ `
          attribute float aSize;
          uniform float uTime;
          varying float vTwinkle;
          void main() {
            vec4 mv = modelViewMatrix * vec4(position, 1.0);
            // Per-mote twinkle frequency derived from position hash, so
            // they all twinkle at slightly different rates.
            float phase = position.x * 3.1 + position.z * 2.7 + position.y * 5.0;
            vTwinkle = 0.55 + 0.45 * sin(uTime * 1.4 + phase);
            gl_PointSize = aSize * (40.0 / -mv.z);
            gl_Position = projectionMatrix * mv;
          }
        `,
        fragmentShader: /* glsl */ `
          varying float vTwinkle;
          void main() {
            vec2 c = gl_PointCoord - vec2(0.5);
            float d = length(c);
            float a = smoothstep(0.5, 0.0, d);
            gl_FragColor = vec4(vec3(1.0, 0.95, 0.85), a * vTwinkle * 0.7);
          }
        `,
      });
      motes = new THREE.Points(moteGeom, moteMat);
      scene.add(motes);
    }

    // ─── Post-processing (perf >= medium) ──────────────────────────────────
    let composer: EffectComposer | null = null;
    let bloomPass: UnrealBloomPass | null = null;
    if (perf !== 'low') {
      composer = new EffectComposer(renderer);
      composer.setPixelRatio(Math.min(window.devicePixelRatio, dprCap));
      composer.setSize(W0, H0);
      composer.addPass(new RenderPass(scene, camera));
      bloomPass = new UnrealBloomPass(
        new THREE.Vector2(W0, H0),
        perf === 'high' ? 0.65 : 0.4, // strength
        0.7,                          // radius
        0.55,                         // threshold — bodies + star bloom, not the rings
      );
      composer.addPass(bloomPass);
      composer.addPass(new OutputPass());
    }

    // ─── Focus state ───────────────────────────────────────────────────────
    const focus: FocusState = { pitch: null, chord: null, scale: null };
    let cameraTargetPos = camera.position.clone();
    let cameraTargetLook = new THREE.Vector3(0, 0, 0);
    // True only while a focus-driven camera transition is in flight. The
    // animation loop only lerps camera.position toward cameraTargetPos
    // when this is true; otherwise OrbitControls owns the camera (so user
    // wheel-zoom / pinch / drag-rotate aren't snapped back next frame).
    // Set true by updateCameraTarget(); cleared either when the lerp
    // converges (within CAMERA_SETTLE_EPS) or when the user grabs
    // controls (the 'start' listener below).
    let cameraAnimating = false;
    const CAMERA_SETTLE_EPS = 0.05;

    // OrbitControls fires 'start' the moment the user touches the canvas
    // with a gesture that affects the camera (wheel, pinch, drag). That's
    // our signal to release the focus-driven ease — the user has spoken.
    controls.addEventListener('start', () => { cameraAnimating = false; });

    const removeMeshes = (meshes: OrbitMesh[]) => {
      for (const m of meshes) {
        scene.remove(m.mesh);
        m.mesh.geometry.dispose();
        (m.mesh.material as THREE.Material).dispose();
        const sprite = labelSprites.get(m.mesh);
        if (sprite) {
          scene.remove(sprite);
          (sprite.material as THREE.SpriteMaterial).map?.dispose();
          (sprite.material as THREE.SpriteMaterial).dispose();
          labelSprites.delete(m.mesh);
        }
      }
    };

    const removeRing = (ring: THREE.Mesh | null) => {
      if (!ring) return;
      scene.remove(ring);
      ring.geometry.dispose();
      (ring.material as THREE.Material).dispose();
    };

    // Update the camera target on focus change. Doesn't snap — the
    // animation loop eases position + lookAt toward target each frame.
    const updateCameraTarget = () => {
      const depth = focusDepth(focus);
      const dist = CAMERA_DIST_BY_DEPTH[depth];

      // Look-at: focused body's anchor on its orbit. Falls back to origin.
      let look = new THREE.Vector3(0, 0, 0);
      if (focus.scale && focus.chord && focus.pitch) {
        const p = pitchAnchor(focus.pitch);
        const c = chordAnchor(focus.chord, focus.pitch);
        const s = scaleAnchor(focus.scale, focus.chord, focus.pitch);
        look = new THREE.Vector3().addVectors(p, c).add(s);
      } else if (focus.chord && focus.pitch) {
        const p = pitchAnchor(focus.pitch);
        const c = chordAnchor(focus.chord, focus.pitch);
        look = new THREE.Vector3().addVectors(p, c);
      } else if (focus.pitch) {
        look = pitchAnchor(focus.pitch);
      }
      cameraTargetLook = look;

      // Position: pull camera out along the (look + slight elevation) direction.
      const dir = look.clone();
      const dirLen = dir.length();
      if (dirLen < 1e-3) {
        cameraTargetPos = new THREE.Vector3(0, dist * 0.32, dist);
      } else {
        dir.normalize();
        const elev = 0.28;
        cameraTargetPos = look.clone()
          .add(dir.clone().multiplyScalar(dist * 0.8))
          .add(new THREE.Vector3(0, dist * elev, 0));
      }
      // Arm the focus-driven ease. The animation loop will lerp until
      // it converges (or the user grabs OrbitControls and the 'start'
      // listener cancels us).
      cameraAnimating = true;
    };

    // ─── Audio ──────────────────────────────────────────────────────────────
    const audio = new TonalOrbitAudio();
    let audioEnabled = true; // user-toggleable

    const updateAudio = () => {
      if (!audioEnabled) {
        audio.setState({ rootMidi: null, chordIntervals: null, shimmerSemitone: null });
        return;
      }
      if (!focus.pitch) {
        audio.setState({ rootMidi: null, chordIntervals: null, shimmerSemitone: null });
        return;
      }
      // Drone root midi:
      //   - If a chord is focused, use the CHORD's root. In Mode A the
      //     chord shares the pitch's root (no audible difference), but in
      //     Mode B every diatonic chord has its own root (Dm under C,
      //     etc.) so the drone follows the chord — sounds like a
      //     progression in the key, not different qualities on one root.
      //   - Else fall back to the focused pitch's midi.
      // PITCH_BODIES sets midi = 60 + pc, so (midi - pc) = 60 (C4) for
      // every pitch; adding chord.rootPc gives the chord's root MIDI in
      // the same octave the pitch drone uses, keeping the audio in a
      // consistent register across chord changes.
      const octaveBaseMidi = focus.pitch.midi - focus.pitch.pc; // = 60
      const rootMidi = focus.chord
        ? octaveBaseMidi + focus.chord.rootPc
        : focus.pitch.midi;
      // Shimmer = scale's "characteristic" interval — the semitone farthest
      // from the chord's notes (i.e. the most colourful tone). Mode B: the
      // shimmer pitch lives on the SCALE's root, not the chord's; we
      // express it as a semitone offset above the rootMidi (chord root)
      // so the audio engine adds it directly.
      let shimmer: number | null = null;
      if (focus.scale && focus.chord) {
        // Offset between scale-root and chord-root, normalised to 0..11.
        const offset = ((focus.scale.rootPc - focus.chord.rootPc) % 12 + 12) % 12;
        const chordSet = new Set(focus.chord.family.intervals);
        // Look for a characteristic scale tone not already in the chord
        // — measured relative to the chord root by adding `offset` to
        // each scale interval. Use the middle candidate for stable feel.
        const candidates = focus.scale.scale.intervals
          .map((i) => (i + offset) % 12)
          .filter((i) => !chordSet.has(i));
        if (candidates.length > 0) {
          shimmer = candidates[Math.floor(candidates.length / 2)];
        }
      }
      audio.setState({
        rootMidi,
        chordIntervals: focus.chord ? focus.chord.family.intervals : null,
        shimmerSemitone: shimmer,
      });
    };

    // ─── Focus mutators ─────────────────────────────────────────────────────
    const setPitchFocus = (pitch: PitchBody | null) => {
      // Wipe deeper orbits.
      const chordMeshes = orbitMeshes.filter((m) => m.body.kind === 'chord');
      const scaleMeshes = orbitMeshes.filter((m) => m.body.kind === 'scale');
      removeMeshes(chordMeshes);
      removeMeshes(scaleMeshes);
      for (let i = orbitMeshes.length - 1; i >= 0; i--) {
        if (orbitMeshes[i].body.kind !== 'pitch') orbitMeshes.splice(i, 1);
      }
      removeRing(chordRing); chordRing = null;
      removeRing(scaleRing); scaleRing = null;

      focus.pitch = pitch;
      focus.chord = null;
      focus.scale = null;

      if (pitch) {
        // Build chord orbit around the focused pitch. Source depends on
        // chord-mode: Mode A = 8 chord families on the pitch as root;
        // Mode B = 7 diatonic triads of the pitch's major key.
        chordRing = addOrbitRing(CHORD_RADIUS, 0xb88c4c, 0.14);
        const chords = chordModeRef.current === 'key' ? keyChordsForPitch(pitch) : chordsForPitch(pitch);
        chords.forEach((cb, i) => {
          const baseAngle = (i / chords.length) * Math.PI * 2;
          // Chord bodies orbit in their own ring around the origin (not
          // around the pitch); the pitch is just the harmonic anchor.
          // This keeps the scene legible — three concentric rings.
          const x = Math.cos(baseAngle) * CHORD_RADIUS;
          const z = Math.sin(baseAngle) * CHORD_RADIUS;
          orbitMeshes.push(buildBody(cb, CHORD_BODY_SIZE, baseAngle, new THREE.Vector3(x, 0, z), pitch));
        });

        // In Mode B the outer scale-ring is the SAME for every chord on
        // this pitch — all 7 modes belong to the same parent key. We
        // build it up-front so it's visible alongside the chord ring;
        // tapping a chord doesn't rebuild the modes (just changes the
        // audio + camera focus).
        if (chordModeRef.current === 'key') {
          scaleRing = addOrbitRing(SCALE_RADIUS, 0x9c4cb8, 0.12);
          const modes = keyModesForPitch(pitch);
          modes.forEach((sb, i) => {
            const baseAngle = (i / modes.length) * Math.PI * 2;
            const x = Math.cos(baseAngle) * SCALE_RADIUS;
            const z = Math.sin(baseAngle) * SCALE_RADIUS;
            orbitMeshes.push(buildBody(sb, SCALE_BODY_SIZE, baseAngle, new THREE.Vector3(x, 0, z), pitch));
          });
        }

        // Adjust star colour to the focused pitch.
        starUniforms.uColor.value.copy(pitch.color.clone().lerp(new THREE.Color('#fff2c4'), 0.5));
        starLight.color.copy(starUniforms.uColor.value);
      } else {
        starUniforms.uColor.value.set('#fff2c4');
        starLight.color.set(0xfff2c4);
      }

      updateCameraTarget();
      updateAudio();
      callbacksRef.current.onFocusChange?.({ ...focus });
    };

    const setChordFocus = (chord: ChordBody | null) => {
      // In Mode A the scale ring is per-chord, so we rebuild it on every
      // chord change. In Mode B the scale ring belongs to the parent
      // pitch (same 7 modes for any chord on that pitch); we leave it
      // intact and only update audio + camera.
      if (chordModeRef.current === 'root') {
        const scaleMeshes = orbitMeshes.filter((m) => m.body.kind === 'scale');
        removeMeshes(scaleMeshes);
        for (let i = orbitMeshes.length - 1; i >= 0; i--) {
          if (orbitMeshes[i].body.kind === 'scale') orbitMeshes.splice(i, 1);
        }
        removeRing(scaleRing); scaleRing = null;
      }

      focus.chord = chord;
      focus.scale = null;

      if (chord && chordModeRef.current === 'root') {
        scaleRing = addOrbitRing(SCALE_RADIUS, 0x9c4cb8, 0.12);
        const scales = scalesForChord(chord);
        scales.forEach((sb, i) => {
          const baseAngle = (i / scales.length) * Math.PI * 2;
          const x = Math.cos(baseAngle) * SCALE_RADIUS;
          const z = Math.sin(baseAngle) * SCALE_RADIUS;
          orbitMeshes.push(buildBody(sb, SCALE_BODY_SIZE, baseAngle, new THREE.Vector3(x, 0, z), chord));
        });
      }

      updateCameraTarget();
      updateAudio();
      callbacksRef.current.onFocusChange?.({ ...focus });
    };

    const setScaleFocus = (scale: ScaleBody | null) => {
      focus.scale = scale;
      updateCameraTarget();
      updateAudio();
      callbacksRef.current.onFocusChange?.({ ...focus });
    };

    // Imperative API for the parent (breadcrumb navigation, etc.).
    const api: TonalOrbitApi = {
      popTo: (depth) => {
        if (depth === 0) setPitchFocus(null);
        else if (depth === 1) setChordFocus(null);
        else if (depth === 2) setScaleFocus(null);
      },
      setOrbiting: (on) => { orbitingRef.value = on; },
      setAudioEnabled: (on) => {
        audioEnabled = on;
        if (on) audio.start();
        updateAudio();
      },
      setChordMode: (mode) => {
        if (mode === chordModeRef.current) return;
        chordModeRef.current = mode;
        // Re-run the focus pipeline against the same pitch so the chord +
        // scale orbits get rebuilt under the new mode. If no pitch is
        // focused yet, the next setPitchFocus call will pick up the new
        // mode automatically.
        const currentPitch = focus.pitch;
        if (currentPitch) {
          setPitchFocus(currentPitch);
        }
      },
      focusPitchByPc: (pc) => {
        const pitch = PITCH_BODIES.find((p) => p.pc === ((pc % 12) + 12) % 12);
        if (pitch) setPitchFocus(pitch);
      },
    };
    const orbitingRef = { value: true };
    callbacksRef.current.onReady?.(api);

    // ─── Anchor helpers (current orbit positions, with rotation) ────────────
    // These are computed without time so they represent the body's BASE
    // anchor — the per-frame rotation is added during animation.
    function pitchAnchor(p: PitchBody): THREE.Vector3 {
      const i = PITCH_BODIES.findIndex((x) => x.pc === p.pc);
      const a = (i / PITCH_BODIES.length) * Math.PI * 2;
      return new THREE.Vector3(Math.cos(a) * PITCH_RADIUS, 0, Math.sin(a) * PITCH_RADIUS);
    }
    function chordAnchor(c: ChordBody, _p: PitchBody): THREE.Vector3 {
      // Derive the chord ring from the SAME source the ring was built from
      // (see setPitchFocus). In Mode A (root chords) the chord set is
      // chordsForPitch — 8 unique family.key entries. In Mode B (key
      // chords) it's keyChordsForPitch — 7 diatonic chords whose families
      // can repeat (I/IV/V are all Major). Matching on family.key alone
      // would land on the first repeat, sending the camera to the wrong
      // body. Match on (rootPc, family.key) so it's unique in both modes.
      const chords = chordModeRef.current === 'key'
        ? keyChordsForPitch(_p)
        : chordsForPitch(_p);
      const i = chords.findIndex((x) => x.family.key === c.family.key && x.rootPc === c.rootPc);
      const a = (i / chords.length) * Math.PI * 2;
      return new THREE.Vector3(Math.cos(a) * CHORD_RADIUS, 0, Math.sin(a) * CHORD_RADIUS);
    }
    function scaleAnchor(s: ScaleBody, c: ChordBody, _p: PitchBody): THREE.Vector3 {
      // Mode A: the outer scale ring is scalesForChord(c) (per-chord).
      // Mode B: the outer scale ring is keyModesForPitch(_p) — the same
      // seven modes for every chord on the focused pitch. Pull from the
      // matching source so the look-target lines up with the rendered ring.
      const scales = chordModeRef.current === 'key'
        ? keyModesForPitch(_p)
        : scalesForChord(c);
      const i = scales.findIndex((x) => x.scale.key === s.scale.key);
      const a = (i / scales.length) * Math.PI * 2;
      return new THREE.Vector3(Math.cos(a) * SCALE_RADIUS, 0, Math.sin(a) * SCALE_RADIUS);
    }

    // ─── Picking — pointer down/up within a small drag tolerance counts as tap ───
    const raycaster = new THREE.Raycaster();
    const pointerNdc = new THREE.Vector2();
    let pointerDownAt: { x: number; y: number; t: number } | null = null;
    let hoveredMesh: THREE.Mesh | null = null;

    const screenToNdc = (clientX: number, clientY: number): THREE.Vector2 => {
      const rect = renderer.domElement.getBoundingClientRect();
      const x = ((clientX - rect.left) / rect.width) * 2 - 1;
      const y = -((clientY - rect.top) / rect.height) * 2 + 1;
      return new THREE.Vector2(x, y);
    };

    const pickAt = (clientX: number, clientY: number): OrbitMesh | null => {
      pointerNdc.copy(screenToNdc(clientX, clientY));
      raycaster.setFromCamera(pointerNdc, camera);
      const meshes = orbitMeshes.map((om) => om.mesh);
      const hits = raycaster.intersectObjects(meshes, false);
      if (hits.length === 0) return null;
      const hit = hits[0].object as THREE.Mesh;
      return orbitMeshes.find((om) => om.mesh === hit) ?? null;
    };

    const onPointerDown = (e: PointerEvent) => {
      pointerDownAt = { x: e.clientX, y: e.clientY, t: performance.now() };
      // First user gesture — start audio + tour cancellation.
      audio.start();
      if (tourActive) cancelTour();
    };
    const onPointerUp = (e: PointerEvent) => {
      if (!pointerDownAt) return;
      const dx = e.clientX - pointerDownAt.x;
      const dy = e.clientY - pointerDownAt.y;
      const dt = performance.now() - pointerDownAt.t;
      pointerDownAt = null;
      // Tap if movement is small and fast enough (otherwise OrbitControls
      // is being used to rotate / pinch).
      if (Math.sqrt(dx * dx + dy * dy) < 8 && dt < 500) {
        const om = pickAt(e.clientX, e.clientY);
        if (om) focusOn(om);
      }
    };
    const onPointerMove = (e: PointerEvent) => {
      // Only update hover state when the pointer is not dragging.
      if (pointerDownAt) return;
      const om = pickAt(e.clientX, e.clientY);
      const newMesh = om ? om.mesh : null;
      if (newMesh !== hoveredMesh) {
        if (hoveredMesh) {
          (hoveredMesh.material as THREE.ShaderMaterial).uniforms.uHover.value = 0;
        }
        hoveredMesh = newMesh;
        if (hoveredMesh) {
          (hoveredMesh.material as THREE.ShaderMaterial).uniforms.uHover.value = 1;
        }
      }
    };

    const focusOn = (om: OrbitMesh) => {
      if (om.body.kind === 'pitch') setPitchFocus(om.body);
      else if (om.body.kind === 'chord') setChordFocus(om.body);
      else if (om.body.kind === 'scale') setScaleFocus(om.body);
    };

    renderer.domElement.addEventListener('pointerdown', onPointerDown);
    renderer.domElement.addEventListener('pointerup',   onPointerUp);
    renderer.domElement.addEventListener('pointermove', onPointerMove);

    // ─── Tour mode ─────────────────────────────────────────────────────────
    let tourActive = false;
    let tourTimers: number[] = [];
    const startTour = () => {
      tourActive = true;
      let pitchIdx = 0;
      const visitNextPitch = () => {
        const pitch = PITCH_BODIES[pitchIdx % PITCH_BODIES.length];
        setPitchFocus(pitch);
        const chords = chordsForPitch(pitch);
        // After the pitch dwell, visit two chords on this pitch, each for
        // a chord dwell. Then move to the next pitch.
        tourTimers.push(window.setTimeout(() => {
          if (!tourActive) return;
          setChordFocus(chords[0]);
          tourTimers.push(window.setTimeout(() => {
            if (!tourActive) return;
            setChordFocus(chords[2 % chords.length]);
            tourTimers.push(window.setTimeout(() => {
              if (!tourActive) return;
              pitchIdx++;
              visitNextPitch();
            }, TOUR_DWELL_MS.chord));
          }, TOUR_DWELL_MS.chord));
        }, TOUR_DWELL_MS.pitch));
      };
      // Begin with a tonic dwell, then start the cycle.
      setPitchFocus(null);
      tourTimers.push(window.setTimeout(visitNextPitch, TOUR_DWELL_MS.tonic));
    };
    const cancelTour = () => {
      tourActive = false;
      for (const t of tourTimers) window.clearTimeout(t);
      tourTimers = [];
    };
    if (tourRef.current) startTour();

    // ─── Resize ────────────────────────────────────────────────────────────
    // Watch BOTH the window (orientation / browser-chrome changes) AND
    // the container element (MUI styles / parent flexbox can settle a
    // few frames after mount, so the initial clientHeight read in the
    // useEffect was undersized — the canvas would stay short until the
    // next window resize). ResizeObserver fires whenever the container
    // box changes for any reason.
    const onResize = () => {
      const W = container.clientWidth || window.innerWidth;
      const H = container.clientHeight || window.innerHeight;
      if (W < 1 || H < 1) return;
      camera.aspect = W / H;
      camera.updateProjectionMatrix();
      renderer.setSize(W, H);
      composer?.setSize(W, H);
      bloomPass?.setSize(W, H);
    };
    window.addEventListener('resize', onResize);
    const resizeObserver = new ResizeObserver(onResize);
    resizeObserver.observe(container);

    // ─── Animation loop ────────────────────────────────────────────────────
    let lastT = performance.now();
    let rafId = 0;
    const startT = performance.now();
    const animate = () => {
      const now = performance.now();
      const dt = Math.min(0.05, (now - lastT) / 1000);
      lastT = now;
      const elapsed = (now - startT) / 1000;

      // Orbit each body at its tier's angular rate.
      if (orbitingRef.value) {
        for (const om of orbitMeshes) {
          const r = om.body.kind === 'pitch' ? PITCH_RADIUS
                  : om.body.kind === 'chord' ? CHORD_RADIUS
                  : SCALE_RADIUS;
          const period = om.body.kind === 'pitch' ? PITCH_PERIOD_S
                       : om.body.kind === 'chord' ? CHORD_PERIOD_S
                       : SCALE_PERIOD_S;
          const angle = om.baseAngle + (elapsed / period) * Math.PI * 2;
          om.mesh.position.set(Math.cos(angle) * r, 0, Math.sin(angle) * r);
        }
      }

      // Update label positions so they hang above their bodies.
      for (const [mesh, sprite] of labelSprites) {
        sprite.position.set(mesh.position.x, mesh.position.y + 0.95, mesh.position.z);
      }

      // Star pulse — gentle sine; bumps when audio is on + a pitch is focused.
      const pulseSrc = focus.pitch ? 0.55 + 0.45 * Math.sin(elapsed * 1.6) : 0.4 + 0.1 * Math.sin(elapsed * 0.7);
      starUniforms.uPulse.value = pulseSrc;
      const haloScale = 6.0 + 0.6 * pulseSrc;
      halo.scale.set(haloScale, haloScale, 1);

      // Ease camera toward target — only when a focus transition is
      // active. Once converged (or the user grabs OrbitControls), we
      // hand the camera back to controls entirely so wheel-zoom / pinch
      // / drag-rotate persist instead of snapping back next frame.
      if (cameraAnimating) {
        const easeFactor = 1 - Math.pow(0.001, dt);
        camera.position.lerp(cameraTargetPos, easeFactor);
        controls.target.lerp(cameraTargetLook, easeFactor);
        if (
          camera.position.distanceTo(cameraTargetPos) < CAMERA_SETTLE_EPS &&
          controls.target.distanceTo(cameraTargetLook) < CAMERA_SETTLE_EPS
        ) {
          cameraAnimating = false;
        }
      }
      controls.update();

      // Motes time uniform.
      if (motes) {
        (motes.material as THREE.ShaderMaterial).uniforms.uTime.value = elapsed;
        motes.rotation.y = elapsed * 0.02;
      }
      // Slow sky rotation so starfield drifts.
      sky.rotation.y = elapsed * 0.005;

      if (composer) composer.render();
      else renderer.render(scene, camera);

      // Test-harness instrumentation (opt-in via `?test=zoom` URL param).
      // TonalOrbitTest sets `data-test=zoom` on the canvas when the URL
      // param is present; we then expose camera state in DOM attributes
      // so a headless --dump-dom probe can verify the zoom-stays fix.
      // Zero overhead on the production code path.
      if (renderer.domElement.getAttribute('data-test') === 'zoom') {
        renderer.domElement.setAttribute('data-camera-dist', camera.position.length().toFixed(2));
        renderer.domElement.setAttribute('data-camera-animating', String(cameraAnimating));
      }

      rafId = requestAnimationFrame(animate);
    };
    rafId = requestAnimationFrame(animate);

    // ─── Cleanup ───────────────────────────────────────────────────────────
    return () => {
      cancelTour();
      cancelAnimationFrame(rafId);
      window.removeEventListener('resize', onResize);
      resizeObserver.disconnect();
      renderer.domElement.removeEventListener('pointerdown', onPointerDown);
      renderer.domElement.removeEventListener('pointerup', onPointerUp);
      renderer.domElement.removeEventListener('pointermove', onPointerMove);
      controls.dispose();
      audio.dispose();
      removeMeshes(orbitMeshes);
      orbitMeshes.length = 0;
      removeRing(pitchRing);
      removeRing(chordRing);
      removeRing(scaleRing);
      scene.remove(star);
      starMat.dispose();
      star.geometry.dispose();
      scene.remove(halo);
      haloTex.dispose();
      haloMat.dispose();
      scene.remove(sky);
      skyMat.dispose();
      sky.geometry.dispose();
      if (motes) {
        scene.remove(motes);
        motes.geometry.dispose();
        (motes.material as THREE.Material).dispose();
      }
      composer?.dispose();
      bloomPass?.dispose();
      renderer.dispose();
      if (renderer.domElement.parentElement === container) {
        container.removeChild(renderer.domElement);
      }
    };
    // Effect intentionally has no dependencies — the scene lives for the
    // lifetime of the component. Prop changes (perf, tour) require remount,
    // which is appropriate: changing perf tier or tour mode rebuilds the
    // post-processing pipeline.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [perf]); // tour read via tourRef inside

  return (
    <Box
      ref={containerRef}
      sx={{
        width: '100%',
        height: '100%',
        position: 'relative',
        touchAction: 'none', // let OrbitControls handle gestures
      }}
    />
  );
};

// ─── Label sprite helpers ───────────────────────────────────────────────────
// Monospace labels with a subtle glow — matches the existing demos' HUD
// typography style (see ModalMeadowTest etc.). Drawn once to a canvas
// then reused as a sprite texture.
function makeLabelSprite(text: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 64;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.font = 'bold 28px Menlo, Consolas, monospace';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.shadowColor = 'rgba(160, 220, 255, 0.85)';
    ctx.shadowBlur = 14;
    ctx.fillStyle = '#eafaff';
    ctx.fillText(text, canvas.width / 2, canvas.height / 2);
  }
  const tex = new THREE.CanvasTexture(canvas);
  tex.minFilter = THREE.LinearFilter;
  tex.magFilter = THREE.LinearFilter;
  const mat = new THREE.SpriteMaterial({
    map: tex,
    transparent: true,
    depthTest: false,
    depthWrite: false,
  });
  const sprite = new THREE.Sprite(mat);
  // Size in world units — width derives from char count rough heuristic.
  const widthFactor = Math.max(1.4, Math.min(3.2, text.length * 0.22));
  sprite.scale.set(widthFactor, widthFactor * 0.25, 1);
  sprite.renderOrder = 999;
  return sprite;
}

export default TonalOrbit;
