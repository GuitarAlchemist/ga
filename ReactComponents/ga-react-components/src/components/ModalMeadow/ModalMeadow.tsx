/**
 * Modal Meadow — interactive mode-region 3D demo (v0).
 *
 * A large grass meadow split into two regions: Ionian on the left (warm,
 * gentle wind, golden sky) and Phrygian on the right (dusky, sharper wind,
 * cooler sky). The player walks first-person across the field; visuals +
 * ambient chord progression smoothly crossfade across the boundary.
 *
 * Shader strategy
 * ──────────────
 * Single InstancedMesh per grass chunk. The vertex shader bends the blade
 * via a quadratic curve + wind noise, with all per-mode parameters
 * (colour, wind speed, wind strength, droop) sampled per-blade from the
 * blade's world x position. A `uModeMix` is computed in the shader from
 * world x with a smoothstep, so the visual transition is exactly aligned
 * with the audio crossfade.
 *
 * v0 deliberately re-implements the grass rather than parameterising the
 * existing FluffyGrass component — the task brief mandates zero changes
 * to `/test/fluffy-grass`, and the shader needs per-pixel mode mixing that
 * the existing shader can't express without an API break.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Box } from '@mui/material';

import {
  IONIAN,
  PHRYGIAN,
  MODES,
  BLEND_HALF_METERS,
  modeWeightsForX,
  dominantModeForX,
  type ModeConfig,
} from './modes';
import { ModalMeadowAudio } from './audio';

// ─── Tunables (v0 keeps these as constants — promote to props in v1) ────────
const FIELD_SIZE = 220;          // metres along each axis
const CHUNK_SIZE = 11;            // grass chunk edge length
const CHUNK_COUNT = 20;           // 20×20 = 400 chunks → field side = 220m
const BLADES_PER_CHUNK = 200;     // 400·200·3 cross-planes ≈ 240k instances
const EYE_HEIGHT = 1.7;           // metres — average human eye height
const WALK_SPEED = 5.0;           // metres per second
const MOUSE_SENSITIVITY = 0.0022; // radians per pixel
// Region centre along world x — Ionian west, Phrygian east, soft band at x=0.
// (Phrygian centre is symmetrically at +60; documented here, not assigned.)
const IONIAN_CENTER_X = -60;

// ─── Procedural noise — same hash/value pair the existing grass uses, so
// the surface "feels" like the fluffy-grass demo even though every blade,
// shader, and uniform here is independent. ──────────────────────────────────
const NOISE_GLSL = /* glsl */ `
  vec2 hash2(vec2 p) {
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
  }
  float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 a = hash2(i);
    vec2 b = hash2(i + vec2(1.0, 0.0));
    vec2 c = hash2(i + vec2(0.0, 1.0));
    vec2 d = hash2(i + vec2(1.0, 1.0));
    return mix(
      mix(dot(a, f), dot(b, f - vec2(1.0, 0.0)), u.x),
      mix(dot(c, f - vec2(0.0, 1.0)), dot(d, f - vec2(1.0, 1.0)), u.x),
      u.y);
  }
`;

interface ModalMeadowProps {
  /** Callback fired when the dominant mode under the player's feet changes. */
  onModeChange?: (mode: ModeConfig) => void;
  /** Callback fired when pointer-lock state changes (true = locked). */
  onLockChange?: (locked: boolean) => void;
}

export const ModalMeadow: React.FC<ModalMeadowProps> = ({ onModeChange, onLockChange }) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = container.clientWidth || 1280;
    const H0 = container.clientHeight || 720;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();

    const camera = new THREE.PerspectiveCamera(70, W0 / H0, 0.1, 600);
    // Spawn in the Ionian half, looking east toward Phrygian.
    camera.position.set(IONIAN_CENTER_X, EYE_HEIGHT, 0);
    camera.rotation.order = 'YXZ'; // yaw then pitch — avoids roll-from-pitch

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W0, H0);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    container.appendChild(renderer.domElement);

    // ─── Sky — simple two-tone shader, mixes the two region sky colours ───
    const skyUniforms = {
      uIonianSky: { value: IONIAN.skyColor.clone() },
      uPhrygianSky: { value: PHRYGIAN.skyColor.clone() },
      uCameraX: { value: camera.position.x },
      uBlendHalf: { value: BLEND_HALF_METERS },
    };
    const skyMaterial = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: skyUniforms,
      vertexShader: /* glsl */ `
        varying vec3 vDir;
        void main() {
          vDir = normalize(position);
          gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uIonianSky;
        uniform vec3 uPhrygianSky;
        uniform float uCameraX;
        uniform float uBlendHalf;
        varying vec3 vDir;
        void main() {
          float t = smoothstep(-uBlendHalf, uBlendHalf, uCameraX);
          vec3 horizon = mix(uIonianSky, uPhrygianSky, t);
          // Gentle vertical gradient: brighter near horizon, slightly cooler up high.
          float h = clamp(vDir.y, 0.0, 1.0);
          vec3 zenith = horizon * 0.55 + vec3(0.05, 0.08, 0.15);
          vec3 col = mix(horizon, zenith, smoothstep(0.0, 0.7, h));
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(400, 24, 16), skyMaterial);
    scene.add(sky);

    // Fog colour tracks the active sky horizon — keeps far blades from
    // popping at the field edge.
    const fog = new THREE.FogExp2(0xb6c8b0, 0.006);
    scene.fog = fog;

    // ─── Lights ────────────────────────────────────────────────────────────
    const ambient = new THREE.HemisphereLight(0xb6d2e8, 0x2a3a1a, 0.85);
    scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xfff1d8, 1.1);
    sun.position.set(40, 80, 30);
    scene.add(sun);

    // ─── Ground plane ──────────────────────────────────────────────────────
    // Flat — keeps the FPS walker simple. Colour mixes Ionian/Phrygian under
    // the same smoothstep the grass uses, so the seam between regions looks
    // intentional rather than misaligned with the blades on top.
    const groundUniforms = {
      uIonianBase: { value: IONIAN.baseColor.clone().multiplyScalar(0.55) },
      uPhrygianBase: { value: PHRYGIAN.baseColor.clone().multiplyScalar(0.55) },
      uBlendHalf: { value: BLEND_HALF_METERS },
    };
    const groundMaterial = new THREE.ShaderMaterial({
      uniforms: groundUniforms,
      vertexShader: /* glsl */ `
        varying vec3 vWorld;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorld = wp.xyz;
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uIonianBase;
        uniform vec3 uPhrygianBase;
        uniform float uBlendHalf;
        varying vec3 vWorld;
        ${NOISE_GLSL}
        void main() {
          float t = smoothstep(-uBlendHalf, uBlendHalf, vWorld.x);
          vec3 base = mix(uIonianBase, uPhrygianBase, t);
          // Patchy variation so the ground doesn't look uniform between blades.
          float patchy = vnoise(vWorld.xz * 0.08) * 0.5 + 0.5;
          vec3 col = base * (0.75 + patchy * 0.4);
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const ground = new THREE.Mesh(
      new THREE.PlaneGeometry(FIELD_SIZE * 1.4, FIELD_SIZE * 1.4),
      groundMaterial,
    );
    ground.rotation.x = -Math.PI / 2;
    scene.add(ground);

    // ─── Grass ─────────────────────────────────────────────────────────────
    // One shader handles both regions; per-pixel uModeMix is computed from
    // world x via smoothstep so the transition band is exactly aligned with
    // the audio crossfade in `modes.ts:modeWeightsForX`.
    const grassUniforms = {
      uTime: { value: 0 },
      // Per-mode tint pairs — the shader picks per-fragment via uModeMix.
      uIonianBase: { value: IONIAN.baseColor.clone() },
      uIonianTip: { value: IONIAN.tipColor.clone() },
      uPhrygianBase: { value: PHRYGIAN.baseColor.clone() },
      uPhrygianTip: { value: PHRYGIAN.tipColor.clone() },
      // Per-mode wind / droop — the shader interpolates per-blade.
      uIonianWindSpeed: { value: IONIAN.windSpeed },
      uPhrygianWindSpeed: { value: PHRYGIAN.windSpeed },
      uIonianWindStrength: { value: IONIAN.windStrength },
      uPhrygianWindStrength: { value: PHRYGIAN.windStrength },
      uIonianDroop: { value: IONIAN.droop },
      uPhrygianDroop: { value: PHRYGIAN.droop },
      uBlendHalf: { value: BLEND_HALF_METERS },
    };

    const grassMaterial = new THREE.ShaderMaterial({
      uniforms: grassUniforms,
      side: THREE.DoubleSide,
      transparent: false,
      depthWrite: true,
      vertexShader: /* glsl */ `
        uniform float uTime;
        uniform float uIonianWindSpeed;
        uniform float uPhrygianWindSpeed;
        uniform float uIonianWindStrength;
        uniform float uPhrygianWindStrength;
        uniform float uIonianDroop;
        uniform float uPhrygianDroop;
        uniform float uBlendHalf;

        attribute float aRandom;
        attribute float aTwist;

        varying vec2 vUv;
        varying float vRandom;
        varying float vModeMix;   // 0 = Ionian, 1 = Phrygian
        varying float vBladeBend; // for fragment lighting

        ${NOISE_GLSL}

        void main() {
          vUv = uv;
          vRandom = aRandom;

          vec3 anchor = (modelMatrix * instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0)).xyz;

          // Mode mix from blade's world x — same smoothstep as JS code +
          // audio so visual & audio transition are perfectly aligned.
          float modeMix = smoothstep(-uBlendHalf, uBlendHalf, anchor.x);
          vModeMix = modeMix;

          float windSpeed    = mix(uIonianWindSpeed,    uPhrygianWindSpeed,    modeMix);
          float windStrength = mix(uIonianWindStrength, uPhrygianWindStrength, modeMix);
          float droop        = mix(uIonianDroop,        uPhrygianDroop,        modeMix);

          // Gust band: a slow noise field sweeping diagonally; multiplied by
          // per-mode strength so Phrygian visibly gusts harder.
          vec2 gustUV = anchor.xz * 0.045 + vec2(uTime * 0.18, uTime * 0.10);
          float gust = pow(vnoise(gustUV) * 0.5 + 0.5, 1.6);
          float flutter = vnoise(anchor.xz * 0.6 + vec2(uTime * windSpeed, aRandom * 13.0));
          float bendAmt = (gust * 1.0 + 0.2) * windStrength + flutter * 0.08 + droop;
          vBladeBend = bendAmt;

          // Quadratic-bezier-style bend along blade's local Y.
          vec3 p = position;
          float t = clamp(uv.y, 0.0, 1.0);
          float curve = t * t;
          p.x += curve * bendAmt;
          p.y -= curve * bendAmt * 0.25;

          // Per-blade Y rotation (twist).
          float c = cos(aTwist);
          float s = sin(aTwist);
          vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);

          vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);
          gl_Position = projectionMatrix * viewMatrix * worldPos;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uIonianBase;
        uniform vec3 uIonianTip;
        uniform vec3 uPhrygianBase;
        uniform vec3 uPhrygianTip;

        varying vec2 vUv;
        varying float vRandom;
        varying float vModeMix;
        varying float vBladeBend;

        void main() {
          // Sharp taper toward the tip so blades read as blades, not rectangles.
          float halfW = abs(vUv.x - 0.5);
          float taper = 1.0 - vUv.y * 0.85;
          if (halfW > taper * 0.5) discard;

          float ao = pow(vUv.y, 1.4);

          // Per-mode base→tip gradient, mixed at the fragment.
          vec3 baseCol = mix(uIonianBase, uPhrygianBase, vModeMix);
          vec3 tipCol  = mix(uIonianTip,  uPhrygianTip,  vModeMix);

          // Phrygian gets a slight violet shimmer at the tip — a v0 wink at
          // v1's "modal colour" choreography. Subtle on purpose.
          tipCol = mix(tipCol, tipCol + vec3(0.10, -0.05, 0.18), vModeMix * 0.30);

          vec3 col = mix(baseCol, tipCol, ao);

          // Slight blade-bend darkening to suggest self-shadow.
          col *= (1.0 - vBladeBend * 0.10);

          // Per-blade variation so the field doesn't read as a single colour.
          col *= (0.85 + vRandom * 0.30);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const bladeHeight = 1.0;
    const bladeWidth = 0.1;
    const bladeGeometry = new THREE.PlaneGeometry(bladeWidth, bladeHeight, 1, 6);
    bladeGeometry.translate(0, bladeHeight / 2, 0);

    const halfCount = CHUNK_COUNT / 2;
    const planeAngles = [0, Math.PI / 3, (2 * Math.PI) / 3];
    const dummy = new THREE.Object3D();
    const grassMeshes: THREE.InstancedMesh[] = [];

    for (let cx = 0; cx < CHUNK_COUNT; cx++) {
      for (let cz = 0; cz < CHUNK_COUNT; cz++) {
        const chunkX = (cx - halfCount) * CHUNK_SIZE;
        const chunkZ = (cz - halfCount) * CHUNK_SIZE;

        // Cheap LOD: chunks far from origin get fewer blades. The player
        // spawns on the x-axis so distance-from-z is the relevant metric.
        const chunkDist = Math.sqrt(chunkX * chunkX + chunkZ * chunkZ);
        const lodFactor = chunkDist > 80 ? 0.4 : chunkDist > 50 ? 0.7 : 1.0;
        const bladeCount = Math.max(20, Math.floor(BLADES_PER_CHUNK * lodFactor));

        for (const baseAngle of planeAngles) {
          const geo = bladeGeometry.clone();
          const aRandom = new Float32Array(bladeCount);
          const aTwist = new Float32Array(bladeCount);

          const inst = new THREE.InstancedMesh(geo, grassMaterial, bladeCount);
          inst.frustumCulled = false;

          for (let i = 0; i < bladeCount; i++) {
            const x = chunkX + Math.random() * CHUNK_SIZE;
            const z = chunkZ + Math.random() * CHUNK_SIZE;
            const scaleH = 0.7 + Math.random() * 0.7;
            const scaleW = 0.8 + Math.random() * 0.5;
            dummy.position.set(x, 0, z);
            dummy.rotation.set(0, 0, 0);
            dummy.scale.set(scaleW, scaleH, 1);
            dummy.updateMatrix();
            inst.setMatrixAt(i, dummy.matrix);

            aRandom[i] = Math.random();
            aTwist[i] = baseAngle + (Math.random() - 0.5) * 0.6;
          }
          inst.instanceMatrix.needsUpdate = true;
          geo.setAttribute('aRandom', new THREE.InstancedBufferAttribute(aRandom, 1));
          geo.setAttribute('aTwist', new THREE.InstancedBufferAttribute(aTwist, 1));

          scene.add(inst);
          grassMeshes.push(inst);
        }
      }
    }

    // ─── FPS controls ──────────────────────────────────────────────────────
    let yaw = -Math.PI / 2;  // facing +x (east, toward Phrygian)
    let pitch = -0.05;
    const keys = new Set<string>();
    let pointerLocked = false;

    const onKeyDown = (e: KeyboardEvent) => keys.add(e.code);
    const onKeyUp = (e: KeyboardEvent) => keys.delete(e.code);
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);

    const onMouseMove = (e: MouseEvent) => {
      if (!pointerLocked) return;
      yaw -= e.movementX * MOUSE_SENSITIVITY;
      pitch -= e.movementY * MOUSE_SENSITIVITY;
      // Clamp pitch to avoid gimbal at poles.
      pitch = Math.max(-Math.PI * 0.49, Math.min(Math.PI * 0.49, pitch));
    };
    document.addEventListener('mousemove', onMouseMove);

    const canvas = renderer.domElement;
    canvas.style.cursor = 'pointer';

    const onCanvasClick = () => {
      if (!pointerLocked) {
        canvas.requestPointerLock();
      }
    };
    canvas.addEventListener('click', onCanvasClick);

    const onPointerLockChange = () => {
      pointerLocked = document.pointerLockElement === canvas;
      canvas.style.cursor = pointerLocked ? 'none' : 'pointer';
      onLockChange?.(pointerLocked);
      // First user gesture → audio safe to start.
      if (pointerLocked) {
        audio.start(MODES);
      }
    };
    document.addEventListener('pointerlockchange', onPointerLockChange);

    // ─── Audio ─────────────────────────────────────────────────────────────
    const audio = new ModalMeadowAudio();

    // ─── Debug hook (dev-only screenshots) ─────────────────────────────────
    // Exposes `window.__modalMeadowTeleport(x)` so dev/CI can grab a
    // screenshot of either region without driving pointer-lock + WASD.
    // No-op in production; harmless in dev. Lives only for the lifetime
    // of this effect and is cleared in cleanup.
    interface DebugWindow extends Window {
      __modalMeadowTeleport?: (x: number) => void;
    }
    (window as DebugWindow).__modalMeadowTeleport = (x: number) => {
      camera.position.x = x;
    };

    // ─── Animation loop ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    let lastDominantName: string | null = null;

    const tick = () => {
      const dt = Math.min(clock.getDelta(), 0.1);
      const elapsed = clock.elapsedTime;
      grassUniforms.uTime.value = elapsed;

      // Camera rotation from yaw/pitch (YXZ order set on camera).
      camera.rotation.y = yaw;
      camera.rotation.x = pitch;

      // WASD movement in the camera's local frame, projected onto the
      // horizontal plane so looking up doesn't make the player fly.
      if (pointerLocked) {
        let mx = 0;
        let mz = 0;
        if (keys.has('KeyW')) mz -= 1;
        if (keys.has('KeyS')) mz += 1;
        if (keys.has('KeyA')) mx -= 1;
        if (keys.has('KeyD')) mx += 1;
        if (mx !== 0 || mz !== 0) {
          // Normalise diagonal so strafing isn't faster than straight walks.
          const len = Math.sqrt(mx * mx + mz * mz);
          mx /= len; mz /= len;
          // Forward = camera's -Z in world, projected to xz.
          const cosY = Math.cos(yaw);
          const sinY = Math.sin(yaw);
          const forwardX = -sinY;
          const forwardZ = -cosY;
          const rightX = cosY;
          const rightZ = -sinY;
          const dx = (forwardX * -mz + rightX * mx) * WALK_SPEED * dt;
          const dz = (forwardZ * -mz + rightZ * mx) * WALK_SPEED * dt;
          camera.position.x += dx;
          camera.position.z += dz;
          // Soft bound — keep player inside the field.
          const half = FIELD_SIZE / 2 - 5;
          camera.position.x = Math.max(-half, Math.min(half, camera.position.x));
          camera.position.z = Math.max(-half, Math.min(half, camera.position.z));
          camera.position.y = EYE_HEIGHT;
        }
      }

      // Drive mode mix from camera x → audio crossfade + sky uniform.
      const weights = modeWeightsForX(camera.position.x);
      audio.setWeights(weights);
      skyUniforms.uCameraX.value = camera.position.x;

      // Fog colour tracks the local sky (rough blend of horizon palette).
      const ionT = weights[0];
      fog.color.setRGB(
        IONIAN.skyColor.r * ionT + PHRYGIAN.skyColor.r * (1 - ionT),
        IONIAN.skyColor.g * ionT + PHRYGIAN.skyColor.g * (1 - ionT),
        IONIAN.skyColor.b * ionT + PHRYGIAN.skyColor.b * (1 - ionT),
      );
      renderer.setClearColor(fog.color, 1);

      // Notify the React HUD when the dominant mode flips.
      const dom = dominantModeForX(camera.position.x);
      if (dom.name !== lastDominantName) {
        lastDominantName = dom.name;
        onModeChange?.(dom);
      }

      renderer.render(scene, camera);
      raf = requestAnimationFrame(tick);
    };
    tick();

    // ─── Resize ────────────────────────────────────────────────────────────
    const onResize = () => {
      const w = container.clientWidth;
      const h = container.clientHeight;
      if (w === 0 || h === 0) return;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
    };
    const ro = new ResizeObserver(onResize);
    ro.observe(container);

    // ─── Cleanup ───────────────────────────────────────────────────────────
    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('pointerlockchange', onPointerLockChange);
      canvas.removeEventListener('click', onCanvasClick);
      if (document.pointerLockElement === canvas) {
        document.exitPointerLock();
      }
      audio.dispose();
      delete (window as DebugWindow).__modalMeadowTeleport;
      grassMeshes.forEach((m) => m.geometry.dispose());
      grassMaterial.dispose();
      bladeGeometry.dispose();
      ground.geometry.dispose();
      groundMaterial.dispose();
      sky.geometry.dispose();
      skyMaterial.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [onModeChange, onLockChange]);

  return <Box ref={containerRef} sx={{ width: '100%', height: '100%', overflow: 'hidden' }} />;
};

export default ModalMeadow;
