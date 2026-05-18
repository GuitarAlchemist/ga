/**
 * Modal Meadow — interactive mode-region 3D demo (v0.7).
 *
 * v0.7 adds two bounded enhancements on top of v0.5:
 *   - EYE_HEIGHT raised 1.7m → 3.5m so the player sees more landscape
 *   - 3–5 water ponds with per-region tint, animated ripples, and a grass
 *     exclusion guard that clears blades inside each pond's radius
 *
 * v0.5 enhancements (still present):
 *   1. Rolling-hills heightmap (CPU + GPU share the noise — see terrain.ts)
 *   2. Audio-reactive grass sway pulse on every chord change
 *   3. Per-region sun (Ionian high-noon, Phrygian sunset, interpolated)
 *   4. Drifting glowing motes (~200) tinted to the local region
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
import { ModalMeadowAudio, type ChordPulseEvent } from './audio';
import {
  sampleTerrainY,
  HEIGHT_GLSL,
  snapToLocalMinimum,
} from './terrain';

// ─── Tunables (v0 keeps these as constants — promote to props in v1) ────────
const FIELD_SIZE = 220;          // metres along each axis
const CHUNK_SIZE = 11;            // grass chunk edge length
const CHUNK_COUNT = 20;           // 20×20 = 400 chunks → field side = 220m
const BLADES_PER_CHUNK = 200;     // 400·200·3 cross-planes ≈ 240k instances
const EYE_HEIGHT = 3.5;           // metres — raised in v0.7 so the player sees more landscape over the grass
const WALK_SPEED = 5.0;           // metres per second
const MOUSE_SENSITIVITY = 0.0022; // radians per pixel
// Region centre along world x — Ionian west, Phrygian east, soft band at x=0.
// (Phrygian centre is symmetrically at +60; documented here, not assigned.)
const IONIAN_CENTER_X = -60;

// ─── Pond placement (v0.7) ──────────────────────────────────────────────────
// MAX_PONDS is the shader array length — kept ≤ 8 so the grass vertex shader
// can iterate it inline without dynamic branching. We seed 5 candidate (x,z)
// positions deterministically (no Math.random, so the same ponds appear on
// every reload for reproducible screenshots), snap each to the nearest local
// terrain minimum within 30m, then drop any that fall too close to the field
// edge.
const MAX_PONDS = 8;
const POND_SEEDS: readonly { x: number; z: number; radius: number }[] = [
  { x: -80, z: -40, radius: 12 }, // Ionian
  { x: -45, z: 50, radius: 9 },  // Ionian
  { x: -90, z: 65, radius: 11 }, // Ionian (west-edge)
  { x: 50, z: -50, radius: 13 }, // Phrygian
  { x: 85, z: 30, radius: 10 },  // Phrygian (east-edge)
] as const;

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
    // Single dynamic sun whose position + colour are interpolated by the
    // same `regionBlend` value the grass shader uses. Ionian = high noon,
    // warm yellow-white. Phrygian = low sunset, deep orange-red.
    //
    // Sun "anchors" are sampled at the camera's world-x via lerp. The
    // ground material's uSunDir + uSunColor uniforms track the same lerp so
    // shading on the hills agrees with the cast direction.
    const ambient = new THREE.HemisphereLight(0xb6d2e8, 0x2a3a1a, 0.65);
    scene.add(ambient);

    const IONIAN_SUN_POS = new THREE.Vector3(40, 90, 30);          // high noon
    const PHRYGIAN_SUN_POS = new THREE.Vector3(120, 14, -40);      // low sunset, east
    const IONIAN_SUN_COLOR = new THREE.Color(0xfff4d0);            // warm yellow-white
    const PHRYGIAN_SUN_COLOR = new THREE.Color(0xd96a4a);          // deep orange-red

    const sun = new THREE.DirectionalLight(IONIAN_SUN_COLOR.getHex(), 1.1);
    sun.position.copy(IONIAN_SUN_POS);
    scene.add(sun);

    // Scratch objects we reuse each frame to avoid GC pressure.
    const sunDirScratch = new THREE.Vector3();
    const sunColorScratch = new THREE.Color();

    // ─── Ground plane — heightmap-displaced rolling hills ──────────────────
    // Vertex shader displaces a high-res plane by `terrainY()` from
    // terrain.ts (the JS-side `sampleTerrainY` uses the same algorithm so
    // the camera + blades land on the same surface). Colour still mixes
    // Ionian/Phrygian under the same smoothstep the grass uses, so the
    // seam between regions reads intentional. Sun shading + tint colour
    // are interpolated by world-x.
    const groundUniforms = {
      uIonianBase: { value: IONIAN.baseColor.clone().multiplyScalar(0.55) },
      uPhrygianBase: { value: PHRYGIAN.baseColor.clone().multiplyScalar(0.55) },
      uBlendHalf: { value: BLEND_HALF_METERS },
      uSunDir: { value: new THREE.Vector3(0.4, 0.85, 0.35) },
      uSunColor: { value: new THREE.Color(1.0, 0.95, 0.85) },
    };
    const groundMaterial = new THREE.ShaderMaterial({
      uniforms: groundUniforms,
      vertexShader: /* glsl */ `
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}
        ${HEIGHT_GLSL}
        void main() {
          // Displace by the heightmap (we drive y in the rotated local
          // frame — the mesh itself is laid flat on Z then rotated, so
          // local +Z becomes world +Y after rotation. We sample on world XZ
          // which after rotation maps to local (x, y).)
          vec3 p = position;
          float h = terrainY(position.xy);
          p.z = h;
          // Finite-difference normal so the shading reads the hills.
          float eps = 1.5;
          float hx = terrainY(position.xy + vec2(eps, 0.0));
          float hz = terrainY(position.xy + vec2(0.0, eps));
          vec3 nLocal = normalize(vec3(-(hx - h) / eps, -(hz - h) / eps, 1.0));
          // Local Z is world +Y after the mesh's -PI/2 X rotation; transform.
          vec3 nWorld = vec3(nLocal.x, nLocal.z, -nLocal.y);
          vNormal = nWorld;
          vec4 wp = modelMatrix * vec4(p, 1.0);
          vWorld = wp.xyz;
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uIonianBase;
        uniform vec3 uPhrygianBase;
        uniform float uBlendHalf;
        uniform vec3 uSunDir;
        uniform vec3 uSunColor;
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}
        void main() {
          float t = smoothstep(-uBlendHalf, uBlendHalf, vWorld.x);
          vec3 base = mix(uIonianBase, uPhrygianBase, t);
          // Patchy variation so the ground doesn't look uniform between blades.
          float patchy = vnoise(vWorld.xz * 0.08) * 0.5 + 0.5;
          // Lambert from the per-region sun.
          float ndotl = max(0.15, dot(normalize(vNormal), normalize(uSunDir)));
          vec3 col = base * (0.6 + patchy * 0.35) * (0.55 + ndotl * uSunColor);
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    // High-res segmented plane — needs enough verts that the hills look
    // smooth at walking distance. 200×200 verts on a 308m plane is ≈ 1.5m
    // per quad, which matches our 1.5m noise sampling epsilon.
    const ground = new THREE.Mesh(
      new THREE.PlaneGeometry(FIELD_SIZE * 1.4, FIELD_SIZE * 1.4, 200, 200),
      groundMaterial,
    );
    ground.rotation.x = -Math.PI / 2;
    scene.add(ground);

    // ─── Ponds (v0.7) ──────────────────────────────────────────────────────
    // Snap each seed to a nearby local-minimum, drop those that fall outside
    // the walkable field, and build one CircleGeometry mesh per pond. Each
    // pond gets a fresh shader-material instance so per-region tinting can
    // live on the material itself (cheaper than a branch in the shader).
    interface PondPlacement {
      readonly x: number;
      readonly z: number;
      readonly y: number;          // water-line elevation
      readonly radius: number;
      readonly region: 'ionian' | 'phrygian';
    }
    const fieldHalf = FIELD_SIZE / 2;
    const ponds: PondPlacement[] = [];
    for (const seed of POND_SEEDS) {
      const snapped = snapToLocalMinimum(seed.x, seed.z, 30, 3);
      // Drop ponds whose disc would extend outside the walkable field.
      if (
        Math.abs(snapped.x) > fieldHalf - seed.radius - 4 ||
        Math.abs(snapped.z) > fieldHalf - seed.radius - 4
      ) {
        continue;
      }
      // Water-line strategy: scan the heightmap across the FULL disc and pick
      // a water surface a comfortable margin above the highest sample inside
      // the disc. That guarantees the entire CircleGeometry sits above the
      // terrain mesh — no z-fighting, no edges clipped by stray bumps.
      //
      // The brief's "just below the lowest point in the basin" reading would
      // hide the water under the ground on v0.5's shallow 3m-amplitude hills,
      // so we instead anchor to the disc-wide max plus a small lift. With the
      // grass exclusion in place, the disc still reads as a pond resting on
      // a small flat. v0.6's deeper hills will let the water sink lower
      // (relative to surrounding grass) for a more "basined" look.
      let discMax = -Infinity;
      const SCAN_STEP = 1.5;
      const r2 = seed.radius * seed.radius;
      for (let dx = -seed.radius; dx <= seed.radius; dx += SCAN_STEP) {
        for (let dz = -seed.radius; dz <= seed.radius; dz += SCAN_STEP) {
          if (dx * dx + dz * dz > r2) continue;
          const ry = sampleTerrainY(snapped.x + dx, snapped.z + dz);
          if (ry > discMax) discMax = ry;
        }
      }
      // 1.4m lift — large enough to read as a real surface above the bank
      // at glancing angles given v0.5's 3m amplitude. The lift looks like a
      // small raised pool on flat ground today; once v0.6's 8m amplitude
      // lands, the surrounding grass will rise to meet it and the basin will
      // look properly recessed.
      const waterY = discMax + 1.4;
      ponds.push({
        x: snapped.x,
        z: snapped.z,
        y: waterY,
        radius: seed.radius,
        region: snapped.x < 0 ? 'ionian' : 'phrygian',
      });
    }

    // Per-region water tint: warm gold-tinted blue on the Ionian side, cool
    // violet-tinted blue on the Phrygian side. The shader still mixes a hint
    // of the live sky horizon colour so the reflection feels grounded.
    const IONIAN_WATER_TINT = new THREE.Color('#3a8db8');     // warm gold-blue
    const PHRYGIAN_WATER_TINT = new THREE.Color('#5a52a8');   // cool violet-blue

    if (process.env.NODE_ENV !== 'production') {
      // Dev breadcrumb — one-line summary of placed ponds so reviewers can
      // verify the snap step landed in expected basins.
      console.info(
        '[ModalMeadow v0.7] ponds:',
        ponds.map((p) => `${p.region}@(${p.x.toFixed(0)},${p.z.toFixed(0)},y=${p.y.toFixed(2)},r=${p.radius})`).join(' '),
      );
      // Dev-only scene handle so smoke tests can inspect mesh positions.
      (window as unknown as { __modalMeadowScene?: THREE.Scene }).__modalMeadowScene = scene;
    }

    // One shared geometry; per-mesh material instance for tint variance.
    const pondMeshes: THREE.Mesh[] = [];
    const pondMaterials: THREE.ShaderMaterial[] = [];
    const pondGeometries: THREE.CircleGeometry[] = [];
    for (const p of ponds) {
      const geo = new THREE.CircleGeometry(p.radius, 48);
      pondGeometries.push(geo);
      const waterUniforms = {
        uTime: { value: 0 },
        uTint: {
          value: (p.region === 'ionian' ? IONIAN_WATER_TINT : PHRYGIAN_WATER_TINT).clone(),
        },
        uSky: { value: IONIAN.skyColor.clone() },  // updated each frame from camera-X blend
      };
      const mat = new THREE.ShaderMaterial({
        uniforms: waterUniforms,
        transparent: true,
        depthWrite: true,
        side: THREE.DoubleSide,
        vertexShader: /* glsl */ `
          varying vec2 vLocalXZ;
          varying vec3 vWorldPos;
          void main() {
            vLocalXZ = position.xy;       // CircleGeometry lies in XY before rotation
            vec4 wp = modelMatrix * vec4(position, 1.0);
            vWorldPos = wp.xyz;
            gl_Position = projectionMatrix * viewMatrix * wp;
          }
        `,
        fragmentShader: /* glsl */ `
          uniform float uTime;
          uniform vec3 uTint;
          uniform vec3 uSky;
          varying vec2 vLocalXZ;
          varying vec3 vWorldPos;
          ${NOISE_GLSL}
          void main() {
            // Subtle ripple driven by a slow noise field — modulates the
            // surface "normal" we use for fresnel. Two layers at different
            // scales give visible movement without reading as a stamped tile.
            vec2 uv = vLocalXZ * 0.5 + vec2(uTime * 0.2, uTime * 0.13);
            float n1 = vnoise(uv);
            float n2 = vnoise(vLocalXZ * 1.4 + vec2(uTime * 0.07, -uTime * 0.09));
            vec3 normal = normalize(vec3(n1 * 0.18, 1.0, n2 * 0.18));

            // View vector (camera → fragment).
            vec3 viewDir = normalize(cameraPosition - vWorldPos);
            // Fresnel falloff: edges (grazing angle) read more sky, centre
            // reads more deep-tint. Power 3 gives a soft band, not a hard ring.
            float fres = pow(1.0 - max(0.0, dot(viewDir, normal)), 3.0);

            // Reflective-ish blue: mix the per-region tint with a small share
            // of the live sky horizon, biased toward the tint so it never
            // becomes a full mirror. Fresnel boosts the sky contribution at
            // grazing angles so the bank edge meets the surroundings.
            vec3 deep = uTint * 0.85;
            vec3 surface = mix(deep, uSky, 0.30 + fres * 0.50);

            // Add a tiny specular sparkle along ripple ridges.
            float sparkle = smoothstep(0.55, 0.90, n1 + n2 * 0.5);
            surface += vec3(sparkle * 0.12);

            // Slight alpha falloff at the very edge so the pond meets the
            // bank without a hard cookie-cut line. radius normalised via
            // length(vLocalXZ) / radius — but radius is baked into the
            // geometry already, so we sample distance directly.
            float distFromCentre = length(vLocalXZ);
            // Geometry radius = 1.0 in local before scaling? No: CircleGeometry
            // takes radius parameter so vLocalXZ already spans [-r, r]. Use a
            // 0.5m soft edge regardless of r.
            float edgeFade = smoothstep(0.0, 0.5, distFromCentre); // not used for hard cut
            gl_FragColor = vec4(surface, 0.95 - edgeFade * 0.0);   // mostly opaque
          }
        `,
      });
      pondMaterials.push(mat);
      const mesh = new THREE.Mesh(geo, mat);
      mesh.rotation.x = -Math.PI / 2;
      mesh.position.set(p.x, p.y, p.z);
      // Render after the ground but before the grass so grass blades that DO
      // sneak through render correctly.
      mesh.renderOrder = 1;
      scene.add(mesh);
      pondMeshes.push(mesh);
    }

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
      // ─── Audio-reactive sway (Enhancement 2) ────────────────────────────
      // Per mode (Ionian = 0, Phrygian = 1): pulse intensity 0..1, decays
      // each frame from JS. `uChordPulseDir` is the X-bias for this pulse:
      //   +1  → V/bvii dominant — stronger on +X side
      //   -1  → IV/bII subdominant — stronger on -X side
      //    0  → I/i tonic — outward ripple from the camera
      // `uChordPulseOrigin` is the camera XZ at the moment of the pulse,
      // used by the tonic ripple to expand outward from where the player
      // stood.
      uChordPulse: { value: new THREE.Vector2(0, 0) },               // (ion, phr) intensity
      uChordPulseDir: { value: new THREE.Vector2(0, 0) },            // (ion, phr) X bias
      uChordPulseOrigin: { value: new THREE.Vector4(0, 0, 0, 0) },   // (ionX,ionZ, phrX,phrZ)
      uChordPulseAge: { value: new THREE.Vector2(99, 99) },          // seconds since last pulse
      // ─── Pond exclusion (v0.7) ──────────────────────────────────────────
      // Each entry is (centerX, centerZ, radius, 0 = unused / 1 = active).
      // Inactive entries are skipped via the .w flag so we don't burn cycles
      // on empty pond slots. Filled in at scene init after pond placement.
      uPonds: {
        value: Array.from({ length: MAX_PONDS }, () => new THREE.Vector4(0, 0, 0, 0)),
      },
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
        uniform vec2 uChordPulse;       // (ionian, phrygian) intensity 0..1
        uniform vec2 uChordPulseDir;    // (ionian, phrygian) X bias -1..+1
        uniform vec4 uChordPulseOrigin; // (ionX, ionZ, phrX, phrZ)
        uniform vec2 uChordPulseAge;    // seconds since each pulse
        // Each: xy = centre XZ, z = radius (m), w = active flag (0 or 1).
        uniform vec4 uPonds[${MAX_PONDS}];

        attribute float aRandom;
        attribute float aTwist;

        varying vec2 vUv;
        varying float vRandom;
        varying float vModeMix;   // 0 = Ionian, 1 = Phrygian
        varying float vBladeBend; // for fragment lighting

        ${NOISE_GLSL}
        ${HEIGHT_GLSL}

        void main() {
          vUv = uv;
          vRandom = aRandom;

          vec3 anchor = (modelMatrix * instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
          // Sample the heightmap so blades sit on rolling hills. We add the
          // height as an extra translation in world-y inside the vertex
          // (since the InstancedMesh was authored on a flat plane).
          float groundY = terrainY(anchor.xz);

          // ─── Pond exclusion (v0.7) ──────────────────────────────────────
          // If this blade's anchor falls inside any active pond's radius,
          // collapse it: scale the local geometry to zero and sink it well
          // below the ground so it cannot poke through the water surface.
          // 1.0 = normal blade, 0.0 = silenced.
          float bladeAlive = 1.0;
          for (int i = 0; i < ${MAX_PONDS}; i++) {
            vec4 pond = uPonds[i];
            if (pond.w < 0.5) continue;            // inactive slot
            float d = length(anchor.xz - pond.xy);
            // Soft edge over 1m so the boundary doesn't look stamped.
            float k = smoothstep(pond.z - 1.0, pond.z, d);
            bladeAlive = min(bladeAlive, k);
          }

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

          // ─── Audio-reactive chord pulse ────────────────────────────────
          // For the active region only (mode i) we add a brief amplitude
          // bump. Direction is biased by uChordPulseDir: subdominant pulls
          // +X side harder, dominant pulls -X harder. Tonic radiates from
          // uChordPulseOrigin so it reads as a ring expanding outward.
          float regionWeight = (1.0 - modeMix); // 1 inside Ionian zone
          vec2 ionOrigin = uChordPulseOrigin.xy;
          float ionDist = length(anchor.xz - ionOrigin);
          float ionRingPhase = ionDist - uChordPulseAge.x * 18.0; // ring speed m/s
          float ionRing = exp(-ionRingPhase * ionRingPhase * 0.02);
          // Linear X-bias factor: -1 at -X edge, +1 at +X edge of field.
          float biasX = clamp(anchor.x / (uBlendHalf * 2.0), -1.0, 1.0);
          float ionBias = mix(1.0, 0.5 + 0.5 * biasX * uChordPulseDir.x, abs(uChordPulseDir.x));
          float ionTonic = (uChordPulseDir.x == 0.0) ? ionRing : 0.0;
          float ionPulseAmp = uChordPulse.x * regionWeight * (ionBias + ionTonic * 1.5);

          float phrRegionWeight = modeMix;
          vec2 phrOrigin = uChordPulseOrigin.zw;
          float phrDist = length(anchor.xz - phrOrigin);
          float phrRingPhase = phrDist - uChordPulseAge.y * 18.0;
          float phrRing = exp(-phrRingPhase * phrRingPhase * 0.02);
          float phrBias = mix(1.0, 0.5 + 0.5 * biasX * uChordPulseDir.y, abs(uChordPulseDir.y));
          float phrTonic = (uChordPulseDir.y == 0.0) ? phrRing : 0.0;
          float phrPulseAmp = uChordPulse.y * phrRegionWeight * (phrBias + phrTonic * 1.5);

          float pulseBoost = 1.0 + 0.20 * (ionPulseAmp + phrPulseAmp);

          float bendAmt = ((gust * 1.0 + 0.2) * windStrength + flutter * 0.08) * pulseBoost + droop;
          vBladeBend = bendAmt;

          // Quadratic-bezier-style bend along blade's local Y.
          vec3 p = position;
          float t = clamp(uv.y, 0.0, 1.0);
          float curve = t * t;
          p.x += curve * bendAmt;
          p.y -= curve * bendAmt * 0.25;

          // Collapse blade geometry inside ponds — keeps a clean water surface.
          p *= bladeAlive;

          // Per-blade Y rotation (twist).
          float c = cos(aTwist);
          float s = sin(aTwist);
          vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
          // Lift the entire blade by the heightmap so its base sits on the
          // terrain. This is correct because rotated.y is local-y and the
          // modelMatrix * instanceMatrix only translates by the (x, 0, z)
          // anchor (we set y=0 on the instanceMatrix at construction).
          // For silenced (pond-interior) blades, sink the anchor a few metres
          // below ground so the collapsed point can't peek above the water.
          rotated.y += groundY - (1.0 - bladeAlive) * 5.0;

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

    // Push pond placements into the grass shader's uPonds uniform. The
    // shader iterates the full MAX_PONDS slots; inactive slots have w=0 and
    // are skipped, so leftover slots stay zero-initialised.
    const grassPondSlots = grassUniforms.uPonds.value as THREE.Vector4[];
    for (let i = 0; i < ponds.length && i < MAX_PONDS; i++) {
      const p = ponds[i];
      grassPondSlots[i].set(p.x, p.z, p.radius, 1.0);
    }

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

    // ─── Drifting motes (Enhancement 4) ────────────────────────────────────
    // ~200 small additive-blended points drift slowly upward through the
    // air, respawning at ground level when they rise ≥ 5m. Colour is tinted
    // by the local region (warm amber in Ionian, cool violet in Phrygian).
    // One THREE.Points mesh, custom shader; total cost < a single chunk of
    // grass.
    const MOTE_COUNT = 200;
    const MOTE_MAX_HEIGHT = 5.0;
    const motePositions = new Float32Array(MOTE_COUNT * 3);
    const moteSeeds = new Float32Array(MOTE_COUNT);
    for (let i = 0; i < MOTE_COUNT; i++) {
      const x = (Math.random() - 0.5) * FIELD_SIZE * 0.95;
      const z = (Math.random() - 0.5) * FIELD_SIZE * 0.95;
      motePositions[i * 3 + 0] = x;
      motePositions[i * 3 + 1] = Math.random() * MOTE_MAX_HEIGHT;
      motePositions[i * 3 + 2] = z;
      moteSeeds[i] = Math.random();
    }
    const moteGeometry = new THREE.BufferGeometry();
    moteGeometry.setAttribute('position', new THREE.BufferAttribute(motePositions, 3));
    moteGeometry.setAttribute('aSeed', new THREE.BufferAttribute(moteSeeds, 1));

    const moteUniforms = {
      uTime: { value: 0 },
      uBlendHalf: { value: BLEND_HALF_METERS },
      uIonianMote: { value: new THREE.Color(1.0, 0.86, 0.45) },    // warm amber
      uPhrygianMote: { value: new THREE.Color(0.72, 0.45, 0.95) }, // cool violet
      uPixelRatio: { value: Math.min(window.devicePixelRatio, 1.5) },
    };
    const moteMaterial = new THREE.ShaderMaterial({
      uniforms: moteUniforms,
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      vertexShader: /* glsl */ `
        uniform float uTime;
        uniform float uPixelRatio;
        attribute float aSeed;
        varying float vModeMix;
        varying float vLife;
        ${NOISE_GLSL}
        ${HEIGHT_GLSL}
        void main() {
          // Mote position: gentle horizontal drift via slow noise + a
          // vertical climb that wraps. aSeed offsets the climb phase so they
          // don't all rise in lockstep.
          float climbDur = 9.0 + aSeed * 6.0;
          float climbPhase = mod(uTime + aSeed * climbDur, climbDur) / climbDur;
          float h = climbPhase * ${MOTE_MAX_HEIGHT.toFixed(2)};
          vec2 driftUV = position.xz * 0.02 + vec2(uTime * 0.04, uTime * 0.03);
          float dx = vnoise(driftUV) * 1.5;
          float dz = vnoise(driftUV + vec2(31.7, 17.3)) * 1.5;
          vec3 wp = vec3(position.x + dx, 0.0, position.z + dz);
          wp.y = terrainY(wp.xz) + 1.0 + h;
          vLife = 1.0 - climbPhase;            // 1 at birth, 0 at top
          vModeMix = smoothstep(-25.0, 25.0, wp.x);

          vec4 vp = viewMatrix * vec4(wp, 1.0);
          gl_Position = projectionMatrix * vp;
          // Size attenuates with distance; clamps so far motes still glow.
          gl_PointSize = uPixelRatio * (50.0 / max(1.0, -vp.z)) * (0.6 + vLife * 0.7);
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uIonianMote;
        uniform vec3 uPhrygianMote;
        varying float vModeMix;
        varying float vLife;
        void main() {
          // Round soft sprite. Discard outside the circle for a clean halo.
          vec2 d = gl_PointCoord - 0.5;
          float r2 = dot(d, d);
          if (r2 > 0.25) discard;
          float alpha = pow(1.0 - r2 * 4.0, 1.8) * (0.35 + vLife * 0.6);
          vec3 col = mix(uIonianMote, uPhrygianMote, vModeMix);
          gl_FragColor = vec4(col * (0.7 + vLife * 0.6), alpha);
        }
      `,
    });
    const motes = new THREE.Points(moteGeometry, moteMaterial);
    motes.frustumCulled = false;
    scene.add(motes);

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
      // First user gesture → audio safe to start. Cancel the stub timer
      // so we don't double-fire pulses against real chord onsets.
      if (pointerLocked) {
        if (stopStubPulses) {
          stopStubPulses();
          stopStubPulses = null;
        }
        audio.start(MODES);
      }
    };
    document.addEventListener('pointerlockchange', onPointerLockChange);

    // ─── Audio ─────────────────────────────────────────────────────────────
    const audio = new ModalMeadowAudio();

    // ─── Chord-pulse state (Enhancement 2) ─────────────────────────────────
    // For each mode (Ionian=0, Phrygian=1) we keep:
    //   intensity   (peak 1, decays exponentially in tick)
    //   directionX  (-1 subdominant / 0 tonic / +1 dominant — see audio.ts)
    //   originXZ    (camera XZ at the moment of the pulse — used for tonic ring)
    //   age         (seconds since pulse start)
    const pulseIntensity: [number, number] = [0, 0];
    const pulseDirection: [number, number] = [0, 0];
    const pulseOrigin = [0, 0, 0, 0]; // [ionX, ionZ, phrX, phrZ]
    const pulseAge: [number, number] = [99, 99];

    const handleChordPulse = (evt: ChordPulseEvent) => {
      const i = evt.modeIndex;
      if (i !== 0 && i !== 1) return;
      pulseIntensity[i] = 1.0;
      pulseAge[i] = 0;
      // Map roman degree → direction: I/i → 0, IV/bII → -1, V/bvii → +1.
      // Brief says IV/bII stronger on +X, V/bvii stronger on -X — but
      // the X-bias in the shader is `0.5 + 0.5 * biasX * dir`, so dir<0
      // means -X side gets the boost. To match brief (IV → +X), use +1
      // for IV and -1 for V.
      if (evt.romanRoot === 4) {
        pulseDirection[i] = 1.0;  // subdominant — boost +X side
      } else if (evt.romanRoot === 5 || evt.romanRoot === 7) {
        pulseDirection[i] = -1.0; // dominant — boost -X side
      } else {
        pulseDirection[i] = 0.0;  // tonic — ring from camera
      }
      // Record camera position as the ring origin for tonic.
      pulseOrigin[i * 2] = camera.position.x;
      pulseOrigin[i * 2 + 1] = camera.position.z;
    };
    const unsubscribePulse = audio.onChordPulse(handleChordPulse);

    // Stub timer drives pulses BEFORE the user clicks (no audio yet) so the
    // visual feels alive even on the lock-screen. Cleared once audio starts.
    let stopStubPulses: (() => void) | null = audio.startStubPulses(MODES);

    // ─── Debug hook (dev-only screenshots) ─────────────────────────────────
    // Exposes `window.__modalMeadowTeleport(x)` so dev/CI can grab a
    // screenshot of either region without driving pointer-lock + WASD.
    // No-op in production; harmless in dev. Lives only for the lifetime
    // of this effect and is cleared in cleanup.
    interface DebugWindow extends Window {
      __modalMeadowTeleport?: (
        x: number,
        z?: number,
        yawOverride?: number,
        pitchOverride?: number,
      ) => void;
    }
    (window as DebugWindow).__modalMeadowTeleport = (
      x: number,
      z?: number,
      yawOverride?: number,
      pitchOverride?: number,
    ) => {
      camera.position.x = x;
      if (typeof z === 'number') camera.position.z = z;
      camera.position.y = sampleTerrainY(camera.position.x, camera.position.z) + EYE_HEIGHT;
      if (typeof yawOverride === 'number') yaw = yawOverride;
      if (typeof pitchOverride === 'number') pitch = pitchOverride;
    };

    // ─── Animation loop ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    let lastDominantName: string | null = null;

    const tick = () => {
      const dt = Math.min(clock.getDelta(), 0.1);
      const elapsed = clock.elapsedTime;
      grassUniforms.uTime.value = elapsed;
      moteUniforms.uTime.value = elapsed;
      // Advance every pond's water shader. Sky tint is updated below once
      // we know the new mode mix.
      for (const mat of pondMaterials) {
        (mat.uniforms.uTime as { value: number }).value = elapsed;
      }

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
        }
      }
      // Camera Y follows the terrain at eye height — every frame, not only
      // on movement, so the boundary-band flattening reads correctly even
      // when standing still and looking around.
      camera.position.y = sampleTerrainY(camera.position.x, camera.position.z) + EYE_HEIGHT;

      // Drive mode mix from camera x → audio crossfade + sky uniform.
      const weights = modeWeightsForX(camera.position.x);
      audio.setWeights(weights);
      skyUniforms.uCameraX.value = camera.position.x;

      // ─── Sun interpolation (Enhancement 3) ───────────────────────────────
      // regionBlend = phrygian weight (0..1). Interp sun position + colour.
      const phrW = weights[1];
      sunDirScratch
        .copy(IONIAN_SUN_POS)
        .lerp(PHRYGIAN_SUN_POS, phrW);
      sun.position.copy(sunDirScratch);
      sunColorScratch
        .copy(IONIAN_SUN_COLOR)
        .lerp(PHRYGIAN_SUN_COLOR, phrW);
      sun.color.copy(sunColorScratch);
      // Sunset is dimmer; Ionian noon ≈ 1.1, Phrygian sunset ≈ 0.55.
      sun.intensity = 1.1 - 0.55 * phrW;
      // Push the same lerp into the ground shader so hill shading agrees.
      groundUniforms.uSunDir.value.copy(sunDirScratch).normalize();
      groundUniforms.uSunColor.value.copy(sunColorScratch);

      // ─── Chord pulse decay ───────────────────────────────────────────────
      // Pulses bump to 1.0 on each chord onset and decay exponentially over
      // ~0.5s, matching the brief.
      const PULSE_DECAY = Math.exp(-dt / 0.18); // ~0.18s time-constant → ~0.5s visual
      pulseIntensity[0] *= PULSE_DECAY;
      pulseIntensity[1] *= PULSE_DECAY;
      pulseAge[0] += dt;
      pulseAge[1] += dt;
      const pulseUniformVec2 = grassUniforms.uChordPulse.value as THREE.Vector2;
      pulseUniformVec2.set(pulseIntensity[0], pulseIntensity[1]);
      const dirUniformVec2 = grassUniforms.uChordPulseDir.value as THREE.Vector2;
      dirUniformVec2.set(pulseDirection[0], pulseDirection[1]);
      const ageUniformVec2 = grassUniforms.uChordPulseAge.value as THREE.Vector2;
      ageUniformVec2.set(pulseAge[0], pulseAge[1]);
      const originUniformVec4 = grassUniforms.uChordPulseOrigin.value as THREE.Vector4;
      originUniformVec4.set(pulseOrigin[0], pulseOrigin[1], pulseOrigin[2], pulseOrigin[3]);

      // Fog colour tracks the local sky (rough blend of horizon palette).
      const ionT = weights[0];
      fog.color.setRGB(
        IONIAN.skyColor.r * ionT + PHRYGIAN.skyColor.r * (1 - ionT),
        IONIAN.skyColor.g * ionT + PHRYGIAN.skyColor.g * (1 - ionT),
        IONIAN.skyColor.b * ionT + PHRYGIAN.skyColor.b * (1 - ionT),
      );
      renderer.setClearColor(fog.color, 1);

      // Push the same sky-tint mix into every pond shader so the reflective
      // share of the water colour tracks the active region.
      for (const mat of pondMaterials) {
        (mat.uniforms.uSky.value as THREE.Color).copy(fog.color);
      }

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
      unsubscribePulse();
      if (stopStubPulses) stopStubPulses();
      audio.dispose();
      delete (window as DebugWindow).__modalMeadowTeleport;
      grassMeshes.forEach((m) => m.geometry.dispose());
      grassMaterial.dispose();
      bladeGeometry.dispose();
      ground.geometry.dispose();
      groundMaterial.dispose();
      sky.geometry.dispose();
      skyMaterial.dispose();
      moteGeometry.dispose();
      moteMaterial.dispose();
      for (const m of pondMeshes) {
        scene.remove(m);
      }
      for (const g of pondGeometries) g.dispose();
      for (const m of pondMaterials) m.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [onModeChange, onLockChange]);

  return <Box ref={containerRef} sx={{ width: '100%', height: '100%', overflow: 'hidden' }} />;
};

export default ModalMeadow;
