/**
 * Modal Meadow — interactive mode-region 3D demo (v1.0 cinematic).
 *
 * v1.0 — cinematic visual pass. Five additions on top of v0.6, all purely
 * visual, none touching audio.ts or the music-theory content:
 *   1. EffectComposer pipeline with UnrealBloomPass so sun + motes glow
 *   2. Real directional shadows from the per-region sun (PCF soft, 2048²)
 *   3. Improved sky shader — atmospheric bands, horizon haze, sun disc + halo
 *   4. Volumetric god rays in the Phrygian region (radial blur from sun)
 *   5. Performance mode toggle via `?perf=high|medium|low` URL query
 *
 * v0.6 added three changes on top of v0.5:
 *   1. Terrain amplitude 3m → 8m so hills are actually visible from spawn
 *      (see terrain.ts — user feedback "I don't see Hills?" on v0.5)
 *   2. Atmospheric fog tied to the per-region sky color so distant grass
 *      dissolves into the sky and the hill silhouette pops at the horizon
 *   3. Auto-walk default: camera moves on its own (sin-wave oscillation
 *      across the boundary band) until the user clicks for pointer-lock
 *      or hits a movement key — then auto-walk releases permanently
 *
 * v0.5 added on top of the flat-plane v0:
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
 * Fog in v0.6 is implemented via custom shader uniforms (uFogNear,
 * uFogFar, uFogColor) and an inline `fogFactor` computed in each
 * fragment shader from gl_FragCoord depth. The built-in THREE.Fog
 * chunks would require either MeshStandardMaterial (which loses the
 * per-pixel mode mixing) or onBeforeCompile patching of every
 * ShaderMaterial — inline is simpler and self-contained.
 *
 * v0 deliberately re-implements the grass rather than parameterising the
 * existing FluffyGrass component — the task brief mandates zero changes
 * to `/test/fluffy-grass`, and the shader needs per-pixel mode mixing that
 * the existing shader can't express without an API break.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Box } from '@mui/material';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/examples/jsm/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';

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
import { sampleTerrainY, HEIGHT_GLSL } from './terrain';

// ─── Performance mode (v1.0) ─────────────────────────────────────────────────
// URL ?perf=high|medium|low overrides — defaults to "high" on desktop.
// Determines which post-processing passes run + shadow map size.
//   high:   bloom + shadows@2048 + god rays
//   medium: bloom + shadows@1024 (no god rays)
//   low:    no post-processing (renderer.render fallback), no shadows
type PerfMode = 'high' | 'medium' | 'low';
const getPerfMode = (): PerfMode => {
  if (typeof window === 'undefined') return 'high';
  const q = new URLSearchParams(window.location.search).get('perf');
  if (q === 'low' || q === 'medium' || q === 'high') return q;
  return 'high';
};

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
  /**
   * Callback fired when the user takes control (pointer-lock or a movement
   * key) — auto-walk releases at this point and never resumes. Used by the
   * HUD to switch the hint from "Auto-walking · Click to take control" to
   * the regular WASD prompt.
   */
  onUserTakeover?: () => void;
}

export const ModalMeadow: React.FC<ModalMeadowProps> = ({
  onModeChange,
  onLockChange,
  onUserTakeover,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = container.clientWidth || 1280;
    const H0 = container.clientHeight || 720;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();

    const camera = new THREE.PerspectiveCamera(70, W0 / H0, 0.1, 600);
    // Spawn in the Ionian half, looking east toward Phrygian. With v0.6
    // auto-walk on, the tick() loop will immediately start oscillating X
    // around 0 — but we keep the spawn at IONIAN_CENTER_X so the FIRST
    // visible frame still shows the Ionian region, and the swing eases the
    // camera back toward the boundary over the first ~quarter cycle.
    camera.position.set(IONIAN_CENTER_X, EYE_HEIGHT, 0);
    camera.rotation.order = 'YXZ'; // yaw then pitch — avoids roll-from-pitch

    const perfMode = getPerfMode();
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W0, H0);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    // ─── Shadow map (v1.0) ────────────────────────────────────────────────
    // PCF soft shadows enabled outside low-perf mode. Sun configures its
    // shadow camera further below once it's instantiated.
    if (perfMode !== 'low') {
      renderer.shadowMap.enabled = true;
      renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    }
    container.appendChild(renderer.domElement);

    // ─── Sky — atmospheric shader with sun disc + halo (v1.0) ──────────────
    // Procedurally improved over v0.6's two-tone gradient sphere:
    //   • Three vertical bands: ground-haze → horizon → zenith with cooler
    //     blue at top approximating Rayleigh-tinted scatter
    //   • Sun disc with bright core + falloff halo, billboarded by direction
    //   • Position + colour driven by uSunDirWorld / uSunColor (lerped each
    //     frame from Ionian noon → Phrygian sunset)
    //   • Horizon haze under the sun direction so a low Phrygian sun
    //     bleeds warmth into the lower band — atmospheric scattering cheat
    //
    // Sky stays inside the renderer's clear color so the bloom pass can
    // pick the bright sun disc as its luminance source.
    const skyUniforms = {
      uIonianSky: { value: IONIAN.skyColor.clone() },
      uPhrygianSky: { value: PHRYGIAN.skyColor.clone() },
      uCameraX: { value: camera.position.x },
      uBlendHalf: { value: BLEND_HALF_METERS },
      uSunDirWorld: { value: new THREE.Vector3(0.4, 0.85, 0.35).normalize() },
      uSunColor: { value: new THREE.Color(1.0, 0.95, 0.85) },
      uSunIntensity: { value: 1.1 },
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
        uniform vec3 uSunDirWorld;
        uniform vec3 uSunColor;
        uniform float uSunIntensity;
        varying vec3 vDir;

        void main() {
          vec3 dir = normalize(vDir);

          // ─── Per-region horizon palette ──────────────────────────────────
          float t = smoothstep(-uBlendHalf, uBlendHalf, uCameraX);
          vec3 horizonCol = mix(uIonianSky, uPhrygianSky, t);

          // ─── Three-band vertical atmosphere ──────────────────────────────
          // Below horizon: warmer ground haze (matches fog). Horizon ring is
          // the brightest band. Zenith cools toward Rayleigh blue.
          float h = dir.y; // -1..1
          vec3 zenithCol = mix(
            vec3(0.42, 0.55, 0.78),  // ionian cyan-blue
            vec3(0.18, 0.16, 0.32),  // phrygian deep dusk
            t
          );
          vec3 groundHaze = horizonCol * 0.85 + vec3(0.05, 0.04, 0.03);

          // smoothstep bands. Horizon centred at h=0, width ~0.12 either side.
          float horizonBand = exp(-pow(h * 4.5, 2.0)); // sharp horizon glow
          vec3 col = mix(groundHaze, zenithCol, smoothstep(-0.05, 0.65, h));
          col += horizonCol * horizonBand * 0.35;

          // ─── Sun-direction warmth bleed (atmospheric scattering cheat) ──
          // Where you look toward the sun, the sky takes on the sun colour
          // proportional to the angle. Strongest near the horizon (low sun).
          float sunAngular = max(0.0, dot(dir, normalize(uSunDirWorld)));
          float sunWarmth = pow(sunAngular, 4.0) * (0.5 + (1.0 - smoothstep(0.0, 0.4, uSunDirWorld.y)) * 0.7);
          col += uSunColor * sunWarmth * 0.25;

          // ─── Sun disc + halo ────────────────────────────────────────────
          // disc: tight cosine falloff so it reads as a bright disc not a
          // smear. halo: wider falloff so bloom has something to pick up.
          float discCos = max(0.0, dot(dir, normalize(uSunDirWorld)));
          float disc = smoothstep(0.9985, 0.9995, discCos);     // bright core
          float halo = pow(max(0.0, discCos), 80.0) * 0.45;     // tight halo
          float wideHalo = pow(max(0.0, discCos), 12.0) * 0.12; // broad bloom
          // Sun core is HDR-bright so UnrealBloomPass picks it up above threshold.
          // Tone down from 6.0 → 3.0 so bloom doesn't oversaturate the whole sky.
          col += uSunColor * (disc * 3.0 * uSunIntensity + halo + wideHalo);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(400, 32, 24), skyMaterial);
    scene.add(sky);

    // ─── Fog (v0.6) ────────────────────────────────────────────────────────
    // Atmospheric fog that tracks the per-region-blended sky color. Hills in
    // the distance dissolve into the sky → silhouettes pop against the
    // lighter horizon band.
    //
    // We don't use scene.fog because the ground/grass/mote materials are
    // ShaderMaterial — wiring built-in THREE fog chunks into custom
    // shaders requires onBeforeCompile patching for each one. Instead, we
    // share `uFogNear`, `uFogFar`, `uFogColor` uniforms across all three
    // and inline a `fogFactor = smoothstep(near, far, viewZ)` computation
    // in each fragment shader.
    //
    // near = 60m: grass within 60m fully visible
    // far  = 220m: grass past 220m fully fogged to sky color
    const FOG_NEAR = 60.0;
    const FOG_FAR = 220.0;
    // Live fog color — mutated every frame from the smoothstep-blended sky
    // horizon (matches what the sky shader actually paints at the horizon
    // ring under the player). Seeded with the Ionian horizon for the first
    // frame so nothing flashes on mount.
    const fogColor = IONIAN.skyColor.clone();
    const fogUniforms = {
      uFogNear: { value: FOG_NEAR },
      uFogFar: { value: FOG_FAR },
      uFogColor: { value: fogColor },
    };

    // GLSL snippet that takes a view-space position and applies fog. Returns
    // mix(col, uFogColor, fogFactor). Inlined into each fragment shader
    // below so we can share one uniform set.
    const FOG_GLSL = /* glsl */ `
      uniform float uFogNear;
      uniform float uFogFar;
      uniform vec3 uFogColor;
      float computeFogFactor(float viewZ) {
        return smoothstep(uFogNear, uFogFar, viewZ);
      }
    `;

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
    scene.add(sun.target);

    // ─── Shadow setup (v1.0) ───────────────────────────────────────────────
    // Sun casts shadows onto the heightmap-displaced ground. We don't enable
    // grass castShadow because 240k instances casting shadows would tank
    // FPS — the heightmap-displaced ground IS the dominant occluder anyway,
    // and the hills cast onto themselves as the sun crosses the field.
    //
    // Shadow camera frustum: centred on the camera-projected onto ground,
    // covers ±SHADOW_HALF along x/z. We update the target each frame so the
    // shadow map follows the player.
    const SHADOW_HALF = 90;
    if (perfMode !== 'low') {
      sun.castShadow = true;
      const mapSize = perfMode === 'high' ? 2048 : 1024;
      sun.shadow.mapSize.set(mapSize, mapSize);
      sun.shadow.camera.left = -SHADOW_HALF;
      sun.shadow.camera.right = SHADOW_HALF;
      sun.shadow.camera.top = SHADOW_HALF;
      sun.shadow.camera.bottom = -SHADOW_HALF;
      sun.shadow.camera.near = 1;
      sun.shadow.camera.far = 260;
      sun.shadow.bias = -0.0008;
      sun.shadow.normalBias = 0.04;
    }

    // Scratch objects we reuse each frame to avoid GC pressure.
    const sunDirScratch = new THREE.Vector3();
    const sunColorScratch = new THREE.Color();
    const sunPosScratch = new THREE.Vector3(); // v1.0: project sun → screen UV

    // ─── Ground plane — heightmap-displaced rolling hills ──────────────────
    // Vertex shader displaces a high-res plane by `terrainY()` from
    // terrain.ts (the JS-side `sampleTerrainY` uses the same algorithm so
    // the camera + blades land on the same surface). Colour still mixes
    // Ionian/Phrygian under the same smoothstep the grass uses, so the
    // seam between regions reads intentional. Sun shading + tint colour
    // are interpolated by world-x.
    // Ground material uses Three.js's shadow uniform plumbing by setting
    // `lights: true` and merging in the relevant uniform groups. This lets
    // us sample `directionalShadowMap[0]` for receive-shadows while keeping
    // our custom per-region colour blending and fog. We don't use
    // MeshStandardMaterial because we'd lose the per-pixel ionian/phrygian
    // smoothstep tint (and the shader-side height displacement).
    const SHADOWS_ENABLED = perfMode !== 'low';
    const groundUniforms = THREE.UniformsUtils.merge([
      SHADOWS_ENABLED ? THREE.UniformsLib.lights : {},
      {
        uIonianBase: { value: IONIAN.baseColor.clone().multiplyScalar(0.55) },
        uPhrygianBase: { value: PHRYGIAN.baseColor.clone().multiplyScalar(0.55) },
        uBlendHalf: { value: BLEND_HALF_METERS },
        uSunDir: { value: new THREE.Vector3(0.4, 0.85, 0.35) },
        uSunColor: { value: new THREE.Color(1.0, 0.95, 0.85) },
        // Shared fog (v0.6). All three uniforms share Vector3/Color refs with
        // the grass + mote materials via `value:` aliasing below.
        uFogNear: fogUniforms.uFogNear,
        uFogFar: fogUniforms.uFogFar,
        uFogColor: fogUniforms.uFogColor,
      },
    ]);
    // Re-alias our shared values after UniformsUtils.merge (which deep-clones).
    groundUniforms.uFogNear = fogUniforms.uFogNear;
    groundUniforms.uFogFar = fogUniforms.uFogFar;
    groundUniforms.uFogColor = fogUniforms.uFogColor;

    const groundShadowDefines = SHADOWS_ENABLED
      ? { USE_SHADOWMAP: '', SHADOWMAP_TYPE_PCF_SOFT: '' }
      : {};

    const groundMaterial = new THREE.ShaderMaterial({
      uniforms: groundUniforms,
      defines: groundShadowDefines,
      lights: SHADOWS_ENABLED,
      vertexShader: /* glsl */ `
        varying vec3 vWorld;
        varying vec3 vNormal;
        varying float vViewZ;
        #include <common>
        #include <shadowmap_pars_vertex>
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
          vec4 vp = viewMatrix * wp;
          vViewZ = -vp.z;
          // Required for shadowmap_vertex chunk: supplies worldPosition.
          vec4 worldPosition = wp;
          // Required for shadowmap_vertex: supplies the lit normal.
          vec3 transformedNormal = nWorld;
          #include <shadowmap_vertex>
          gl_Position = projectionMatrix * vp;
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
        varying float vViewZ;
        #include <common>
        #include <packing>
        #include <shadowmap_pars_fragment>
        ${NOISE_GLSL}
        ${FOG_GLSL}
        void main() {
          float t = smoothstep(-uBlendHalf, uBlendHalf, vWorld.x);
          vec3 base = mix(uIonianBase, uPhrygianBase, t);
          // Patchy variation so the ground doesn't look uniform between blades.
          float patchy = vnoise(vWorld.xz * 0.08) * 0.5 + 0.5;
          // Lambert from the per-region sun.
          float ndotl = max(0.15, dot(normalize(vNormal), normalize(uSunDir)));

          // Shadow lookup (v1.0). When USE_SHADOWMAP is not defined this
          // collapses to 1.0 at compile time so low-perf mode pays nothing.
          float shadow = 1.0;
          #ifdef USE_SHADOWMAP
            #if NUM_DIR_LIGHT_SHADOWS > 0
              DirectionalLightShadow dShadow = directionalLightShadows[0];
              // Three.js 0.180 signature: (map, size, intensity, bias, radius, coord)
              shadow = getShadow(
                directionalShadowMap[0],
                dShadow.shadowMapSize,
                dShadow.shadowIntensity,
                dShadow.shadowBias,
                dShadow.shadowRadius,
                vDirectionalShadowCoord[0]
              );
              // Soften: hill shadows should darken to ~0.45 ambient floor.
              shadow = mix(0.45, 1.0, shadow);
            #endif
          #endif

          vec3 col = base * (0.6 + patchy * 0.35) * (0.55 + ndotl * uSunColor * shadow);
          float fogF = computeFogFactor(vViewZ);
          col = mix(col, uFogColor, fogF);
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
    // Ground is the shadow catcher — hills cast onto themselves and onto
    // adjacent terrain as the sun crosses the field.
    ground.receiveShadow = SHADOWS_ENABLED;
    ground.castShadow = SHADOWS_ENABLED;
    // The ground's vertex shader displaces vertices by terrainY(). Three.js
    // renders the shadow map via a separate `MeshDepthMaterial` that won't
    // see our displacement, so the depth values would be flat and the hill
    // shapes wouldn't actually self-occlude. We override with a custom depth
    // material that runs the same heightmap displacement. (Same maths,
    // depth-packing fragment shader.)
    if (SHADOWS_ENABLED) {
      ground.customDepthMaterial = new THREE.ShaderMaterial({
        defines: { DEPTH_PACKING: 3201 }, // RGBADepthPacking
        vertexShader: /* glsl */ `
          ${NOISE_GLSL}
          ${HEIGHT_GLSL}
          void main() {
            vec3 p = position;
            p.z = terrainY(position.xy);
            gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(p, 1.0);
          }
        `,
        fragmentShader: /* glsl */ `
          #include <packing>
          void main() {
            gl_FragColor = packDepthToRGBA(gl_FragCoord.z);
          }
        `,
      });
    }
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
      // Shared fog (v0.6) — same Color/value refs as the ground material.
      uFogNear: fogUniforms.uFogNear,
      uFogFar: fogUniforms.uFogFar,
      uFogColor: fogUniforms.uFogColor,
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

        attribute float aRandom;
        attribute float aTwist;

        varying vec2 vUv;
        varying float vRandom;
        varying float vModeMix;   // 0 = Ionian, 1 = Phrygian
        varying float vBladeBend; // for fragment lighting
        varying float vViewZ;     // distance from camera, for fog (v0.6)

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

          // Per-blade Y rotation (twist).
          float c = cos(aTwist);
          float s = sin(aTwist);
          vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
          // Lift the entire blade by the heightmap so its base sits on the
          // terrain. This is correct because rotated.y is local-y and the
          // modelMatrix * instanceMatrix only translates by the (x, 0, z)
          // anchor (we set y=0 on the instanceMatrix at construction).
          rotated.y += groundY;

          vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);
          vec4 viewPos = viewMatrix * worldPos;
          vViewZ = -viewPos.z;
          gl_Position = projectionMatrix * viewPos;
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
        varying float vViewZ;

        ${FOG_GLSL}

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

          // Atmospheric fog (v0.6) — distant blades dissolve into the sky.
          float fogF = computeFogFactor(vViewZ);
          col = mix(col, uFogColor, fogF);

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
      // Shared fog (v0.6) — same Color/value refs as the ground+grass.
      uFogNear: fogUniforms.uFogNear,
      uFogFar: fogUniforms.uFogFar,
      uFogColor: fogUniforms.uFogColor,
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
        varying float vViewZ;
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
          vViewZ = -vp.z;
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
        varying float vViewZ;
        ${FOG_GLSL}
        void main() {
          // Round soft sprite. Discard outside the circle for a clean halo.
          vec2 d = gl_PointCoord - 0.5;
          float r2 = dot(d, d);
          if (r2 > 0.25) discard;
          float alpha = pow(1.0 - r2 * 4.0, 1.8) * (0.35 + vLife * 0.6);
          vec3 col = mix(uIonianMote, uPhrygianMote, vModeMix);
          // Motes are additive-blended → fade alpha to 0 in the fog band so
          // they don't punch through the haze (mixing color toward grey-sky
          // would still leave a hot dot under ADD blending).
          float fogF = computeFogFactor(vViewZ);
          alpha *= (1.0 - fogF);
          gl_FragColor = vec4(col * (0.7 + vLife * 0.6), alpha);
        }
      `,
    });
    const motes = new THREE.Points(moteGeometry, moteMaterial);
    motes.frustumCulled = false;
    scene.add(motes);

    // ─── Post-processing pipeline (v1.0) ───────────────────────────────────
    // EffectComposer chain:
    //   RenderPass         → scene to render target
    //   GodRaysPass        → radial blur from sun screen position (Phrygian)
    //   UnrealBloomPass    → bright sun disc + motes glow
    //   OutputPass         → tone-map + output color space
    //
    // perf=low skips the composer entirely and falls back to direct
    // renderer.render() in the animation loop.
    const BLOOM_PASS_ENABLED = perfMode !== 'low';
    const GODRAYS_PASS_ENABLED = perfMode === 'high';

    // The composer's internal render target needs to keep linear color so
    // tone mapping happens in OutputPass (otherwise bloom runs on sRGB-encoded
    // values and looks wrong).
    let composer: EffectComposer | null = null;
    let bloomPass: UnrealBloomPass | null = null;
    let godRaysPass: ShaderPass | null = null;

    if (BLOOM_PASS_ENABLED) {
      composer = new EffectComposer(renderer);
      composer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
      composer.setSize(W0, H0);

      composer.addPass(new RenderPass(scene, camera));

      // ─── God rays pass (v1.0, perf=high only) ──────────────────────────
      // Radial blur from the sun's screen-space position. Strength + decay
      // are scaled by `uIntensity` (= phrygian weight, smoothed) so the
      // effect cross-fades in as the player approaches Phrygian and is
      // invisible in Ionian. The brief calls this "subtle: 'the air is full
      // of sunset light' not 'JJ Abrams lens flare'" — strength tuned low.
      if (GODRAYS_PASS_ENABLED) {
        godRaysPass = new ShaderPass({
          uniforms: {
            tDiffuse: { value: null },
            uSunScreen: { value: new THREE.Vector2(0.5, 0.5) },
            uIntensity: { value: 0.0 },   // 0..1 — driven by phrygian weight
            uSunColor: { value: new THREE.Color(0xd96a4a) },
            uSunBehind: { value: 0.0 },   // 1 when sun in front of camera
          },
          vertexShader: /* glsl */ `
            varying vec2 vUv;
            void main() {
              vUv = uv;
              gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
            }
          `,
          fragmentShader: /* glsl */ `
            uniform sampler2D tDiffuse;
            uniform vec2 uSunScreen;
            uniform float uIntensity;
            uniform vec3 uSunColor;
            uniform float uSunBehind;
            varying vec2 vUv;
            void main() {
              vec4 col = texture2D(tDiffuse, vUv);
              if (uIntensity < 0.01 || uSunBehind < 0.5) {
                gl_FragColor = col;
                return;
              }
              // 24-sample radial blur along the line from this pixel toward
              // the sun's screen position. Decay + density chosen so motes
              // and bright sky cells become directional light shafts but
              // don't smear the grass too much.
              const int SAMPLES = 24;
              float decay = 0.96;
              float density = 0.92;
              float weight = 0.35;
              vec2 delta = (uSunScreen - vUv) * (1.0 / float(SAMPLES)) * density;
              vec2 uv = vUv;
              vec3 accum = vec3(0.0);
              float illumDecay = 1.0;
              for (int i = 0; i < SAMPLES; i++) {
                uv += delta;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) break;
                vec3 sCol = texture2D(tDiffuse, uv).rgb;
                // Bias toward bright pixels — god rays come from luminance.
                float lum = dot(sCol, vec3(0.299, 0.587, 0.114));
                sCol *= smoothstep(0.45, 0.9, lum);
                accum += sCol * (illumDecay * weight);
                illumDecay *= decay;
              }
              // Tint accumulated light by the sun colour so Phrygian rays
              // read as warm sunset, not generic light bleed.
              vec3 rays = accum * uSunColor * uIntensity;
              // Additive blend on top of scene — subtle.
              gl_FragColor = vec4(col.rgb + rays, col.a);
            }
          `,
        });
        composer.addPass(godRaysPass);
      }

      bloomPass = new UnrealBloomPass(
        new THREE.Vector2(W0, H0),
        0.45, // strength: hot pixels (sun + motes) glow softly, grass stays grounded
        0.45, // radius: medium spread so the sun has a halo but doesn't bleed across
        0.95, // threshold: only the brightest pixels (sun disc core, brightest motes)
              //   bloom — keeps the warm sky bands grounded
      );
      composer.addPass(bloomPass);

      // Tone-mapping + sRGB encoding — UnrealBloom outputs linear, so we
      // need OutputPass at the end to land in display colour space.
      composer.addPass(new OutputPass());
    }

    // ─── FPS controls ──────────────────────────────────────────────────────
    let yaw = -Math.PI / 2;  // facing +x (east, toward Phrygian)
    let pitch = -0.05;
    const keys = new Set<string>();
    let pointerLocked = false;

    // ─── Auto-walk (v0.6) ──────────────────────────────────────────────────
    // On mount the camera moves autonomously: forward at ~2.5 m/s along the
    // mode-axis, with a slow sin-wave oscillation across the Ionian/Phrygian
    // boundary every ~25s. First user interaction — pointer-lock click OR a
    // movement key — releases auto-walk PERMANENTLY and never resumes.
    let autoWalk = true;
    const autoWalkStartTime = performance.now() / 1000;
    const AUTO_WALK_SPEED = 2.5;        // m/s forward
    const AUTO_WALK_SWING_AMP = 30.0;   // metres of x-oscillation
    const AUTO_WALK_SWING_PERIOD = 25.0; // seconds for a full cycle
    // Record the spawn z so the sin-curve oscillates symmetrically; spawn x
    // is the Ionian centre, but we want the swing to sweep across the
    // boundary band (which is at x = 0) so we phase the sine relative to
    // that goal — see tick() below.
    const AUTO_WALK_BASE_X = 0;          // centre the swing on the boundary
    // Movement-key codes that should trigger takeover (no pointer-lock).
    const MOVEMENT_KEYS = new Set([
      'KeyW', 'KeyA', 'KeyS', 'KeyD',
      'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight',
    ]);

    const releaseAutoWalk = () => {
      if (!autoWalk) return;
      autoWalk = false;
      onUserTakeover?.();
      // Movement-key takeover also counts as user gesture → audio is now
      // allowed to start. Pointer-lock takeover already calls audio.start()
      // in onPointerLockChange; this branch covers the keyboard-only case.
      if (!pointerLocked) {
        if (stopStubPulses) {
          stopStubPulses();
          stopStubPulses = null;
        }
        audio.start(MODES);
      }
    };

    const onKeyDown = (e: KeyboardEvent) => {
      keys.add(e.code);
      if (autoWalk && MOVEMENT_KEYS.has(e.code)) releaseAutoWalk();
    };
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
        // First pointer-lock also retires auto-walk permanently (v0.6).
        if (autoWalk) {
          autoWalk = false;
          onUserTakeover?.();
        }
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
      __modalMeadowTeleport?: (x: number, z?: number, yawDeg?: number, pitchDeg?: number) => void;
      __modalMeadowFreeze?: () => void;
    }
    (window as DebugWindow).__modalMeadowTeleport = (x: number, z?: number, yawDeg?: number, pitchDeg?: number) => {
      camera.position.x = x;
      if (typeof z === 'number') camera.position.z = z;
      camera.position.y = sampleTerrainY(camera.position.x, camera.position.z) + EYE_HEIGHT;
      if (typeof yawDeg === 'number') yaw = (yawDeg * Math.PI) / 180;
      if (typeof pitchDeg === 'number') pitch = (pitchDeg * Math.PI) / 180;
    };
    // Stop auto-walk so screenshots from teleport are stable.
    (window as DebugWindow).__modalMeadowFreeze = () => {
      autoWalk = false;
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

      // Camera rotation from yaw/pitch (YXZ order set on camera).
      camera.rotation.y = yaw;
      camera.rotation.x = pitch;

      // Auto-walk drift (v0.6) — runs only until the user takes over.
      // Path: forward along +Z at AUTO_WALK_SPEED; X sweeps a sin curve
      // around the boundary band so the camera crosses Ionian↔Phrygian once
      // per cycle. Camera yaw eases toward the direction of travel so it
      // feels intentional, not glitchy.
      //
      // First ~6s: smooth ease from the spawn X (Ionian centre, -60) toward
      // the swing curve so we don't snap on frame 1.
      if (!pointerLocked && autoWalk) {
        const tAuto = performance.now() / 1000 - autoWalkStartTime;
        const theta = (tAuto / AUTO_WALK_SWING_PERIOD) * Math.PI * 2;
        const targetX = AUTO_WALK_BASE_X + Math.sin(theta) * AUTO_WALK_SWING_AMP;
        // Ease-in over 6s (smoothstep) so the camera glides from spawn into
        // the swing instead of teleporting on the first frame.
        const easeT = Math.min(1, tAuto / 6.0);
        const easeS = easeT * easeT * (3 - 2 * easeT);
        const blendedX = IONIAN_CENTER_X * (1 - easeS) + targetX * easeS;
        const prevX = camera.position.x;
        const prevZ = camera.position.z;
        camera.position.x = blendedX;
        camera.position.z += AUTO_WALK_SPEED * dt;
        // Soft bound — wrap Z back to keep the walk inside the field.
        const half = FIELD_SIZE / 2 - 5;
        if (camera.position.z > half) camera.position.z = -half;
        // Smooth yaw to face the direction of travel. velocity = position
        // delta this frame; atan2 to derive heading. Slerp yaw toward it.
        const vx = camera.position.x - prevX;
        const vz = camera.position.z - prevZ;
        if (vx * vx + vz * vz > 1e-6) {
          // Match the WASD yaw convention: forward = camera's -Z, so the
          // heading yaw is atan2(-vx, -vz). (Yaw 0 → look -Z; yaw +π/2 → -X.)
          const desiredYaw = Math.atan2(-vx, -vz);
          // Shortest-arc lerp.
          let dy = desiredYaw - yaw;
          while (dy > Math.PI) dy -= Math.PI * 2;
          while (dy < -Math.PI) dy += Math.PI * 2;
          yaw += dy * Math.min(1, dt * 1.4);
        }
      }

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
      // Push the sun direction into the sky shader so the sun disc + halo
      // moves with the per-region interpolation. The sky uses world-direction
      // (normalized position), so we just pass the normalized sun position.
      skyUniforms.uSunDirWorld.value.copy(sunDirScratch).normalize();
      skyUniforms.uSunColor.value.copy(sunColorScratch);
      skyUniforms.uSunIntensity.value = sun.intensity;

      // ─── Move the shadow camera with the player (v1.0) ───────────────────
      // Sun shadow map is centred on the player's ground projection so the
      // shadow stays high-resolution where it matters. Target follows the
      // camera; the directional light "position" stays in world space.
      if (SHADOWS_ENABLED) {
        sun.target.position.set(camera.position.x, 0, camera.position.z);
        sun.target.updateMatrixWorld();
        // Re-anchor the sun position relative to the moving target so its
        // direction stays correct. (DirectionalLight direction = position -
        // target.) We add the per-region sun anchor on top of the target.
        sun.position.set(
          camera.position.x + sunDirScratch.x,
          sunDirScratch.y,
          camera.position.z + sunDirScratch.z,
        );
      }

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

      // Fog colour tracks the per-region-blended sky horizon (v0.6).
      // `weights[0]` is Ionian weight, so we lerp Phrygian→Ionian as it
      // grows. Match what the sky-shader smoothstep paints at the horizon
      // ring so the fogged grass at far range and the sky agree.
      const ionT = weights[0];
      fogColor.setRGB(
        IONIAN.skyColor.r * ionT + PHRYGIAN.skyColor.r * (1 - ionT),
        IONIAN.skyColor.g * ionT + PHRYGIAN.skyColor.g * (1 - ionT),
        IONIAN.skyColor.b * ionT + PHRYGIAN.skyColor.b * (1 - ionT),
      );
      renderer.setClearColor(fogColor, 1);

      // Notify the React HUD when the dominant mode flips.
      const dom = dominantModeForX(camera.position.x);
      if (dom.name !== lastDominantName) {
        lastDominantName = dom.name;
        onModeChange?.(dom);
      }

      // ─── Drive the god rays pass (v1.0) ──────────────────────────────────
      // We need:
      //   1. Sun position in screen-space NDC → uv (0..1)
      //   2. Whether the sun is in front of the camera (dot(forward, sun-cam) > 0)
      //   3. Phrygian weight as the activation envelope
      if (godRaysPass) {
        const sunPosVec = sunPosScratch.copy(sun.position);
        // Project to NDC.
        sunPosVec.project(camera);
        const inFront = sunPosVec.z > -1 && sunPosVec.z < 1;
        const u = sunPosVec.x * 0.5 + 0.5;
        const v = sunPosVec.y * 0.5 + 0.5;
        godRaysPass.uniforms.uSunScreen.value.set(u, v);
        godRaysPass.uniforms.uSunBehind.value = inFront ? 1.0 : 0.0;
        // Phrygian weight gated by a smooth onset at >0.3. Lerp toward the
        // target each frame so the cross-fade is smooth (~0.5s onset time).
        const targetIntensity = Math.max(0, (phrW - 0.3) / 0.7);
        const curIntensity = godRaysPass.uniforms.uIntensity.value as number;
        godRaysPass.uniforms.uIntensity.value = curIntensity + (targetIntensity - curIntensity) * Math.min(1, dt * 4);
        godRaysPass.uniforms.uSunColor.value.copy(sunColorScratch);
      }

      // Render via composer (post-pipeline) when enabled, otherwise direct.
      if (composer) {
        composer.render();
      } else {
        renderer.render(scene, camera);
      }
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
      // v1.0: composer + bloom pass need their render targets resized too.
      if (composer) composer.setSize(w, h);
      if (bloomPass) bloomPass.setSize(w, h);
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
      // v1.0: tear down post-processing render targets so the GPU doesn't
      // hold their backing memory after the component unmounts.
      if (bloomPass) bloomPass.dispose();
      if (composer) composer.dispose();
      if (ground.customDepthMaterial) ground.customDepthMaterial.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [onModeChange, onLockChange, onUserTakeover]);

  return <Box ref={containerRef} sx={{ width: '100%', height: '100%', overflow: 'hidden' }} />;
};

export default ModalMeadow;
