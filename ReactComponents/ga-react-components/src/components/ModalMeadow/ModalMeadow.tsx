/**
 * Modal Meadow — interactive mode-region 3D demo (v0.8).
 *
 * v0.8 ships:
 *   1. All SEVEN diatonic modes (Lydian, Ionian, Mixolydian, Dorian, Aeolian,
 *      Phrygian, Locrian) laid out left-to-right in modal-brightness order.
 *      x = -210, -140, -70, 0, +70, +140, +210. Field half-width ±245m.
 *   2. v0.6 hills (8m amplitude) and fog inherited.
 *   3. v0.7 ponds (one per region, tinted to local mode).
 *   4. EYE_HEIGHT 3.5 (v0.7) so the player sees the rolling terrain clearly.
 *   5. Auto-walk that traverses the full Lydian→Locrian sweep at 2.5 m/s
 *      (~3.5 minute sweep), reversing at the ends. Z-axis gently oscillates.
 *   6. Descent effect: slope-magnitude → pitch glide on the active mode's
 *      drone (max ~50 cents), mote speed/FOV bumps. See `descentEffect` block.
 *   7. Locrian erratic wind: direction reverses every 4 seconds + a per-blade
 *      shimmer (brief lightness flash) so the tritone-root reads as
 *      physically unstable.
 *
 * Shader strategy
 * ──────────────
 * Per-fragment / per-blade we compute a 7-element weight vector from world-x
 * (Gaussian RBF around each region centre, renormalised — same math as JS
 * `modeWeightsForX`). All per-mode params (colours, wind, droop, pond tint)
 * are passed as length-7 arrays of uniforms; the shader does a weighted sum.
 * This keeps the shader code single-pass, no per-region branches.
 *
 * Performance budget
 * ─────────────────
 * v0.8 keeps the same blade-count budget as v0.5 (~240k instances) because
 * the per-blade work is dominated by noise sampling, not the 7-way weight
 * computation (cheap exp + normalisation). On integrated GPU the test page
 * holds 30+ FPS at 1280×720. If we need to scale down, the cheap knob is
 * BLADES_PER_CHUNK or LOD radius — both already linear scalers.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { Box } from '@mui/material';

import {
  LYDIAN,
  MODES,
  REGION_HALF_WIDTH,
  FIELD_HALF_X,
  modeWeightsForX,
  dominantModeForX,
  type ModeConfig,
} from './modes';
import { ModalMeadowAudio, type ChordPulseEvent } from './audio';
import { sampleTerrainY, HEIGHT_GLSL } from './terrain';

// ─── Tunables ────────────────────────────────────────────────────────────────
const FIELD_SIZE_X = FIELD_HALF_X * 2 + 30;  // 520m — covers all 7 regions + margin
const FIELD_SIZE_Z = 220;                    // metres along z
const CHUNK_SIZE = 11;                       // grass chunk edge length
const CHUNK_COUNT_X = Math.ceil(FIELD_SIZE_X / CHUNK_SIZE);
const CHUNK_COUNT_Z = Math.ceil(FIELD_SIZE_Z / CHUNK_SIZE);
const BLADES_PER_CHUNK = 110;                // pulled down from 200 so 7 regions
                                              // stay inside v0.5 performance budget
const EYE_HEIGHT = 3.5;                      // v0.7 — sit higher to see hills
const WALK_SPEED = 5.0;                      // metres per second
const AUTO_WALK_SPEED = 2.5;                 // metres per second along X (brief)
const MOUSE_SENSITIVITY = 0.0022;            // radians per pixel
const BASE_FOV_DEG = 70;                     // resting field of view

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

// Build the per-mode "region weight" GLSL snippet. We pre-bake the 7 region
// centres + the inverse-2σ² constant so the shader gets a tight loop and the
// compiler can unroll it.
const REGION_WEIGHTS_GLSL = /* glsl */ `
  // Returns 7 weights summing to 1, one per region, given a world-x value.
  // Mirrors JS modeWeightsForX in modes.ts.
  void regionWeights(float worldX, out float w0, out float w1, out float w2,
                     out float w3, out float w4, out float w5, out float w6) {
    float centers[7];
    centers[0] = ${MODES[0].regionCenterX.toFixed(2)};
    centers[1] = ${MODES[1].regionCenterX.toFixed(2)};
    centers[2] = ${MODES[2].regionCenterX.toFixed(2)};
    centers[3] = ${MODES[3].regionCenterX.toFixed(2)};
    centers[4] = ${MODES[4].regionCenterX.toFixed(2)};
    centers[5] = ${MODES[5].regionCenterX.toFixed(2)};
    centers[6] = ${MODES[6].regionCenterX.toFixed(2)};
    float invTwoSigmaSq = ${(1 / (2 * REGION_HALF_WIDTH * REGION_HALF_WIDTH)).toFixed(8)};
    float ws[7];
    float total = 0.0;
    for (int i = 0; i < 7; i++) {
      float d = worldX - centers[i];
      ws[i] = exp(-d * d * invTwoSigmaSq);
      total += ws[i];
    }
    float inv = (total > 0.0) ? (1.0 / total) : 0.0;
    w0 = ws[0] * inv;
    w1 = ws[1] * inv;
    w2 = ws[2] * inv;
    w3 = ws[3] * inv;
    w4 = ws[4] * inv;
    w5 = ws[5] * inv;
    w6 = ws[6] * inv;
  }
`;

interface ModalMeadowProps {
  /** Callback fired when the dominant mode under the player's feet changes. */
  onModeChange?: (mode: ModeConfig) => void;
  /** Callback fired when pointer-lock state changes (true = locked). */
  onLockChange?: (locked: boolean) => void;
  /**
   * Callback fired when the user takes control (pointer-lock click or a
   * movement key press) — auto-walk releases at this point and never
   * resumes. v0.6 API surface, preserved verbatim in v0.8 so the test
   * page's HUD takeover-swap behaviour keeps working.
   */
  onUserTakeover?: () => void;
  /** If true (default), camera auto-walks across all 7 regions until the
   *  player takes pointer-lock. Set false to disable auto-walk entirely. */
  autoWalk?: boolean;
}

export const ModalMeadow: React.FC<ModalMeadowProps> = ({
  onModeChange,
  onLockChange,
  onUserTakeover,
  autoWalk = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = container.clientWidth || 1280;
    const H0 = container.clientHeight || 720;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();

    const camera = new THREE.PerspectiveCamera(BASE_FOV_DEG, W0 / H0, 0.1, 800);
    // Spawn at Lydian centre, looking east toward the brightness curve.
    camera.position.set(LYDIAN.regionCenterX, EYE_HEIGHT, 0);
    camera.rotation.order = 'YXZ'; // yaw then pitch — avoids roll-from-pitch

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W0, H0);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    container.appendChild(renderer.domElement);

    // Pre-build colour arrays for shader uniforms. Vec3 per mode in linear-RGB.
    const baseColors = MODES.map((m) => m.baseColor.clone());
    const tipColors = MODES.map((m) => m.tipColor.clone());
    const skyColors = MODES.map((m) => m.skyColor.clone());
    const pondColors = MODES.map((m) => m.pondColor.clone());
    const windSpeeds = MODES.map((m) => m.windSpeed);
    const windStrengths = MODES.map((m) => m.windStrength);
    const droops = MODES.map((m) => m.droop);

    // ─── Sky — 7-way colour blend, mixes all region sky colours by camera-x ─
    const skyUniforms = {
      uSkyColors: { value: skyColors },
      uCameraX: { value: camera.position.x },
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
        uniform vec3 uSkyColors[7];
        uniform float uCameraX;
        varying vec3 vDir;
        ${REGION_WEIGHTS_GLSL}
        void main() {
          float w0, w1, w2, w3, w4, w5, w6;
          regionWeights(uCameraX, w0, w1, w2, w3, w4, w5, w6);
          vec3 horizon =
            uSkyColors[0] * w0 + uSkyColors[1] * w1 + uSkyColors[2] * w2 +
            uSkyColors[3] * w3 + uSkyColors[4] * w4 + uSkyColors[5] * w5 +
            uSkyColors[6] * w6;
          // Gentle vertical gradient: brighter near horizon, slightly cooler up high.
          float h = clamp(vDir.y, 0.0, 1.0);
          vec3 zenith = horizon * 0.55 + vec3(0.05, 0.08, 0.15);
          vec3 col = mix(horizon, zenith, smoothstep(0.0, 0.7, h));
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(500, 24, 16), skyMaterial);
    scene.add(sky);

    // Fog colour tracks the active sky horizon — keeps far blades from
    // popping at the field edge. Recomputed each frame from camera-x weights.
    const fog = new THREE.FogExp2(0xb6c8b0, 0.0055);
    scene.fog = fog;

    // ─── Lights ────────────────────────────────────────────────────────────
    // Dynamic sun whose position + colour are interpolated across all 7
    // regions. Bright regions (Lydian/Ionian) get a high-noon yellow-white
    // sun; dark regions (Phrygian/Locrian) drop toward a low dusky one.
    const ambient = new THREE.HemisphereLight(0xb6d2e8, 0x2a3a1a, 0.65);
    scene.add(ambient);

    // Per-region sun positions + colours, in modes-order (Lydian→Locrian).
    // Lydian = high gold-white; Ionian high warm yellow; Mixo lower amber;
    // Dorian noon silver; Aeolian afternoon steel; Phrygian sunset orange;
    // Locrian deep dusk red. Heights step down from 90 → 12m.
    const SUN_POSITIONS = [
      new THREE.Vector3(40, 95, 30),       // Lydian — high noon, slight north
      new THREE.Vector3(40, 90, 30),       // Ionian
      new THREE.Vector3(60, 70, 20),       // Mixolydian
      new THREE.Vector3(80, 55, 10),       // Dorian
      new THREE.Vector3(95, 38, -10),      // Aeolian
      new THREE.Vector3(115, 22, -30),     // Phrygian — low sunset
      new THREE.Vector3(120, 12, -40),     // Locrian — deepest dusk
    ];
    const SUN_COLORS = [
      new THREE.Color(0xfffce0),           // Lydian — gold-white
      new THREE.Color(0xfff4d0),           // Ionian — warm yellow
      new THREE.Color(0xffd9a0),           // Mixolydian — amber
      new THREE.Color(0xe0d0c0),           // Dorian — silver
      new THREE.Color(0xc0b8b0),           // Aeolian — steel
      new THREE.Color(0xd96a4a),           // Phrygian — sunset orange-red
      new THREE.Color(0x884060),           // Locrian — dusky red-violet
    ];
    const SUN_INTENSITIES = [1.15, 1.10, 0.95, 0.80, 0.70, 0.55, 0.45];

    const sun = new THREE.DirectionalLight(SUN_COLORS[1].getHex(), SUN_INTENSITIES[1]);
    sun.position.copy(SUN_POSITIONS[1]);
    scene.add(sun);

    // Scratch objects we reuse each frame to avoid GC pressure.
    const sunDirScratch = new THREE.Vector3();
    const sunColorScratch = new THREE.Color();
    // Scratch for the ground-clamp sample-ahead vector (see tick()).
    const groundClampDirScratch = new THREE.Vector3();

    // ─── Ground plane — heightmap-displaced rolling hills ──────────────────
    // 7-region tinted ground. Lambert from the interpolated sun direction.
    const groundUniforms = {
      uBaseColors: { value: baseColors.map((c) => c.clone().multiplyScalar(0.55)) },
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
          vec3 p = position;
          float h = terrainY(position.xy);
          p.z = h;
          // Finite-difference normal so the shading reads the hills.
          float eps = 1.5;
          float hx = terrainY(position.xy + vec2(eps, 0.0));
          float hz = terrainY(position.xy + vec2(0.0, eps));
          vec3 nLocal = normalize(vec3(-(hx - h) / eps, -(hz - h) / eps, 1.0));
          vec3 nWorld = vec3(nLocal.x, nLocal.z, -nLocal.y);
          vNormal = nWorld;
          vec4 wp = modelMatrix * vec4(p, 1.0);
          vWorld = wp.xyz;
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uBaseColors[7];
        uniform vec3 uSunDir;
        uniform vec3 uSunColor;
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}
        ${REGION_WEIGHTS_GLSL}
        void main() {
          float w0, w1, w2, w3, w4, w5, w6;
          regionWeights(vWorld.x, w0, w1, w2, w3, w4, w5, w6);
          vec3 base =
            uBaseColors[0] * w0 + uBaseColors[1] * w1 + uBaseColors[2] * w2 +
            uBaseColors[3] * w3 + uBaseColors[4] * w4 + uBaseColors[5] * w5 +
            uBaseColors[6] * w6;
          // Patchy variation so the ground doesn't look uniform between blades.
          float patchy = vnoise(vWorld.xz * 0.08) * 0.5 + 0.5;
          // Lambert from the per-region sun.
          float ndotl = max(0.15, dot(normalize(vNormal), normalize(uSunDir)));
          vec3 col = base * (0.6 + patchy * 0.35) * (0.55 + ndotl * uSunColor);
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    // High-res segmented plane — covers the full 7-region field.
    const ground = new THREE.Mesh(
      new THREE.PlaneGeometry(FIELD_SIZE_X, FIELD_SIZE_Z * 1.4, 360, 160),
      groundMaterial,
    );
    ground.rotation.x = -Math.PI / 2;
    scene.add(ground);

    // ─── Ponds (v0.7) ──────────────────────────────────────────────────────
    // One pond per region, sat in a "low" spot near the region centre.
    // Each pond is a flat disk slightly above the local ground at that
    // point, tinted to the region's pondColor. Ripples animate via uTime.
    const POND_RADIUS = 10.0;
    const pondUniforms = {
      uTime: { value: 0 },
      uPondColors: { value: pondColors },
      uPondCenters: { value: MODES.map((m) => new THREE.Vector2(m.regionCenterX, 0)) },
      uSunDir: { value: groundUniforms.uSunDir.value },
    };
    // Build a single combined pond mesh: 7 disks placed at the region centres,
    // each at the terrain Y at that spot minus 0.5m so it reads as a recessed
    // pool. Shader picks the active pond by nearest centre.
    const pondGeometry = new THREE.PlaneGeometry(POND_RADIUS * 2, POND_RADIUS * 2, 32, 32);
    const pondMaterial = new THREE.ShaderMaterial({
      uniforms: pondUniforms,
      transparent: true,
      depthWrite: false,
      side: THREE.DoubleSide,
      vertexShader: /* glsl */ `
        varying vec2 vLocal;
        varying vec3 vWorld;
        void main() {
          vLocal = position.xy;
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorld = wp.xyz;
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform float uTime;
        uniform vec3 uPondColors[7];
        uniform vec2 uPondCenters[7];
        uniform vec3 uSunDir;
        varying vec2 vLocal;
        varying vec3 vWorld;
        ${NOISE_GLSL}
        void main() {
          // Distance-to-centre disk mask with slight feathering.
          float r = length(vLocal);
          float diskR = ${POND_RADIUS.toFixed(2)};
          if (r > diskR) discard;
          float feather = smoothstep(diskR, diskR - 1.5, r);
          // Pick the nearest pond centre to colour this fragment.
          int best = 0;
          float bestD = 1.0e9;
          for (int i = 0; i < 7; i++) {
            float d = abs(vWorld.x - uPondCenters[i].x);
            if (d < bestD) { bestD = d; best = i; }
          }
          vec3 col = uPondColors[0];
          if (best == 1) col = uPondColors[1];
          else if (best == 2) col = uPondColors[2];
          else if (best == 3) col = uPondColors[3];
          else if (best == 4) col = uPondColors[4];
          else if (best == 5) col = uPondColors[5];
          else if (best == 6) col = uPondColors[6];
          // Concentric ripples + small-scale shimmer = water surface.
          float ripple =
            sin(r * 1.7 - uTime * 2.0) * 0.5 +
            sin(r * 0.7 + uTime * 0.6) * 0.5;
          float micro = vnoise(vLocal * 0.7 + vec2(uTime * 0.15, uTime * 0.10));
          float shine = pow(max(0.0, ripple * 0.5 + 0.5), 4.0) * 0.45 + micro * 0.10;
          // Highlight tinted by sun colour for visual unity.
          col = mix(col, col + vec3(shine), 0.6);
          gl_FragColor = vec4(col, feather * 0.85);
        }
      `,
    });
    const pondGroup = new THREE.Group();
    for (let i = 0; i < MODES.length; i++) {
      const px = MODES[i].regionCenterX;
      const pz = 24;                              // place pond slightly north of axis
      const py = sampleTerrainY(px, pz) - 0.4;    // sat 0.4m below local ground (pool)
      const m = new THREE.Mesh(pondGeometry, pondMaterial);
      m.rotation.x = -Math.PI / 2;
      m.position.set(px, py, pz);
      pondGroup.add(m);
    }
    scene.add(pondGroup);

    // ─── Grass ─────────────────────────────────────────────────────────────
    // 7-region grass shader. Wind speed/strength/droop and colours are all
    // weighted sums across the 7 modes. The Locrian region also gets a
    // direction-reversing wind and a per-blade shimmer flash.
    const grassUniforms = {
      uTime: { value: 0 },
      uBaseColors: { value: baseColors },
      uTipColors: { value: tipColors },
      uWindSpeeds: { value: new Float32Array(windSpeeds) },
      uWindStrengths: { value: new Float32Array(windStrengths) },
      uDroops: { value: new Float32Array(droops) },
      // Locrian wind reversal: ±1 multiplier flipping every 4 seconds. The
      // shader only applies it to the Locrian-weight component.
      uLocrianWindDir: { value: 1.0 },
      // Audio-reactive sway — per-mode pulse intensity, X-bias direction,
      // origin XZ for tonic ring expansion, and age (seconds since pulse).
      // Length 7 each. Direction +1 = subdominant (boost +X), -1 = dominant
      // (boost -X), 0 = tonic (ring from origin).
      uChordPulse: { value: new Float32Array(MODES.length) },
      uChordPulseDir: { value: new Float32Array(MODES.length) },
      // Origins flattened: [x0,z0, x1,z1, ...]. Length 14.
      uChordPulseOrigin: { value: new Float32Array(MODES.length * 2) },
      uChordPulseAge: { value: new Float32Array(MODES.length).fill(99) },
    };

    const grassMaterial = new THREE.ShaderMaterial({
      uniforms: grassUniforms,
      side: THREE.DoubleSide,
      transparent: false,
      depthWrite: true,
      vertexShader: /* glsl */ `
        uniform float uTime;
        uniform float uWindSpeeds[7];
        uniform float uWindStrengths[7];
        uniform float uDroops[7];
        uniform float uLocrianWindDir;
        uniform float uChordPulse[7];
        uniform float uChordPulseDir[7];
        uniform float uChordPulseOrigin[14];
        uniform float uChordPulseAge[7];

        attribute float aRandom;
        attribute float aTwist;

        varying vec2 vUv;
        varying float vRandom;
        varying float vLocrianW;   // for shimmer in fragment
        varying float vWorldX;     // for fragment to look up region colours
        varying float vBladeBend;

        ${NOISE_GLSL}
        ${HEIGHT_GLSL}
        ${REGION_WEIGHTS_GLSL}

        void main() {
          vUv = uv;
          vRandom = aRandom;

          vec3 anchor = (modelMatrix * instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
          float groundY = terrainY(anchor.xz);
          vWorldX = anchor.x;

          float w0, w1, w2, w3, w4, w5, w6;
          regionWeights(anchor.x, w0, w1, w2, w3, w4, w5, w6);
          vLocrianW = w6;

          float windSpeed =
            uWindSpeeds[0]*w0 + uWindSpeeds[1]*w1 + uWindSpeeds[2]*w2 +
            uWindSpeeds[3]*w3 + uWindSpeeds[4]*w4 + uWindSpeeds[5]*w5 +
            uWindSpeeds[6]*w6;
          float windStrength =
            uWindStrengths[0]*w0 + uWindStrengths[1]*w1 + uWindStrengths[2]*w2 +
            uWindStrengths[3]*w3 + uWindStrengths[4]*w4 + uWindStrengths[5]*w5 +
            uWindStrengths[6]*w6;
          float droop =
            uDroops[0]*w0 + uDroops[1]*w1 + uDroops[2]*w2 +
            uDroops[3]*w3 + uDroops[4]*w4 + uDroops[5]*w5 +
            uDroops[6]*w6;

          // Wind direction: ordinarily +X. Locrian region flips its share
          // every 4 seconds via uLocrianWindDir. We mix by Locrian weight
          // so the wind reversal is local to that region, not global.
          float windDir = mix(1.0, uLocrianWindDir, w6);

          // Gust band — slow noise field. Drift direction depends on windDir
          // so reversal under Locrian visibly flips which way the blades lean.
          vec2 gustUV = anchor.xz * 0.045 + vec2(uTime * 0.18 * windDir, uTime * 0.10);
          float gust = pow(vnoise(gustUV) * 0.5 + 0.5, 1.6);
          float flutter = vnoise(anchor.xz * 0.6 + vec2(uTime * windSpeed, aRandom * 13.0));

          // ─── Audio-reactive chord pulse, 7 modes ─────────────────────────
          // For each mode i: amplitude bump weighted by region weight. The
          // tonic ring expands from the captured origin; subdominant/dominant
          // pulls a side. Linear X-bias normalised by REGION_HALF_WIDTH so it
          // reads inside the local 70m-wide region.
          float pulseSum = 0.0;
          float regionHalf = ${REGION_HALF_WIDTH.toFixed(2)};

          // Compile-time loop — 7 iterations is fine.
          // (GLSL ES 1.00 wants a constant trip-count.)
          for (int i = 0; i < 7; i++) {
            float wReg = i==0?w0 : i==1?w1 : i==2?w2 : i==3?w3 : i==4?w4 : i==5?w5 : w6;
            float pulse = uChordPulse[i];
            float dir = uChordPulseDir[i];
            float age = uChordPulseAge[i];
            float ox = uChordPulseOrigin[i*2 + 0];
            float oz = uChordPulseOrigin[i*2 + 1];
            float distRing = length(anchor.xz - vec2(ox, oz));
            float ringPhase = distRing - age * 18.0;
            float ring = exp(-ringPhase * ringPhase * 0.02);
            float biasX = clamp((anchor.x - ${MODES[0].regionCenterX.toFixed(2)}
                                 - float(i) * 70.0) / regionHalf, -1.0, 1.0);
            // Note: above is approximate — what matters is "left half vs right
            // half" of the region; we use anchor.x minus the local centre.
            float bias = mix(1.0, 0.5 + 0.5 * biasX * dir, abs(dir));
            float tonic = (dir == 0.0) ? ring : 0.0;
            pulseSum += pulse * wReg * (bias + tonic * 1.5);
          }
          float pulseBoost = 1.0 + 0.18 * pulseSum;

          float bendAmt = ((gust * 1.0 + 0.2) * windStrength + flutter * 0.08) * pulseBoost + droop;
          bendAmt *= windDir;   // visible direction flip during Locrian reversal
          vBladeBend = abs(bendAmt);

          // Quadratic-bezier-style bend along blade's local Y.
          vec3 p = position;
          float t = clamp(uv.y, 0.0, 1.0);
          float curve = t * t;
          p.x += curve * bendAmt;
          p.y -= curve * abs(bendAmt) * 0.25;

          // Per-blade Y rotation (twist).
          float c = cos(aTwist);
          float s = sin(aTwist);
          vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
          rotated.y += groundY;

          vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);
          gl_Position = projectionMatrix * viewMatrix * worldPos;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform float uTime;
        uniform vec3 uBaseColors[7];
        uniform vec3 uTipColors[7];

        varying vec2 vUv;
        varying float vRandom;
        varying float vLocrianW;
        varying float vWorldX;
        varying float vBladeBend;
        ${REGION_WEIGHTS_GLSL}

        void main() {
          // Sharp taper toward the tip so blades read as blades, not rectangles.
          float halfW = abs(vUv.x - 0.5);
          float taper = 1.0 - vUv.y * 0.85;
          if (halfW > taper * 0.5) discard;

          float ao = pow(vUv.y, 1.4);

          float w0, w1, w2, w3, w4, w5, w6;
          regionWeights(vWorldX, w0, w1, w2, w3, w4, w5, w6);
          vec3 baseCol =
            uBaseColors[0]*w0 + uBaseColors[1]*w1 + uBaseColors[2]*w2 +
            uBaseColors[3]*w3 + uBaseColors[4]*w4 + uBaseColors[5]*w5 +
            uBaseColors[6]*w6;
          vec3 tipCol =
            uTipColors[0]*w0 + uTipColors[1]*w1 + uTipColors[2]*w2 +
            uTipColors[3]*w3 + uTipColors[4]*w4 + uTipColors[5]*w5 +
            uTipColors[6]*w6;

          vec3 col = mix(baseCol, tipCol, ao);
          col *= (1.0 - vBladeBend * 0.10);
          col *= (0.85 + vRandom * 0.30);

          // Locrian shimmer: ~5% of blades per frame flash brief desat pulse.
          // We bias by Locrian weight so shimmer only happens inside Locrian.
          // Hash the random + time (1Hz bucket) so a given blade flashes for
          // ~0.1s at a time, then settles.
          float shimmerBucket = floor(uTime * 10.0) + floor(vRandom * 1000.0);
          float shimmerHash = fract(sin(shimmerBucket * 12.9898) * 43758.5453);
          if (vLocrianW > 0.4 && shimmerHash > 0.95) {
            // Desaturate flash toward grey, biased violet — reads as electric.
            float l = dot(col, vec3(0.299, 0.587, 0.114));
            col = mix(col, vec3(l, l, l * 1.15), 0.5);
          }

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const bladeHeight = 1.0;
    const bladeWidth = 0.1;
    const bladeGeometry = new THREE.PlaneGeometry(bladeWidth, bladeHeight, 1, 6);
    bladeGeometry.translate(0, bladeHeight / 2, 0);

    const halfCountX = CHUNK_COUNT_X / 2;
    const halfCountZ = CHUNK_COUNT_Z / 2;
    const planeAngles = [0, Math.PI / 3, (2 * Math.PI) / 3];
    const dummy = new THREE.Object3D();
    const grassMeshes: THREE.InstancedMesh[] = [];

    for (let cx = 0; cx < CHUNK_COUNT_X; cx++) {
      for (let cz = 0; cz < CHUNK_COUNT_Z; cz++) {
        const chunkX = (cx - halfCountX) * CHUNK_SIZE;
        const chunkZ = (cz - halfCountZ) * CHUNK_SIZE;

        // Cheap LOD: blades thin out away from the auto-walk corridor (Z≈0).
        // Far chunks along Z get fewer blades; X is uniform so all 7 regions
        // are equally inhabited.
        const zDist = Math.abs(chunkZ);
        const lodFactor = zDist > 60 ? 0.4 : zDist > 30 ? 0.7 : 1.0;
        const bladeCount = Math.max(15, Math.floor(BLADES_PER_CHUNK * lodFactor));

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
    // ~350 small additive-blended points drift across the whole 7-region
    // field. Colour is tinted by the local region. The descent effect can
    // speed them up and tilt them in the direction of motion.
    const MOTE_COUNT = 350;
    const MOTE_MAX_HEIGHT = 5.0;
    const motePositions = new Float32Array(MOTE_COUNT * 3);
    const moteSeeds = new Float32Array(MOTE_COUNT);
    for (let i = 0; i < MOTE_COUNT; i++) {
      const x = (Math.random() - 0.5) * FIELD_SIZE_X;
      const z = (Math.random() - 0.5) * FIELD_SIZE_Z * 0.9;
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
      uMoteColors: { value: MODES.map((m) => m.tipColor.clone().lerp(new THREE.Color(0xffffff), 0.3)) },
      uPixelRatio: { value: Math.min(window.devicePixelRatio, 1.5) },
      // Descent-effect dynamic uniforms. uMoteSpeedMult ∈ [0.8, 2.0] depending
      // on slope; uMoteTilt is the (dx, dz) direction the player is moving so
      // motes appear to streak past as scenery.
      uMoteSpeedMult: { value: 1.0 },
      uMoteTilt: { value: new THREE.Vector2(0, 0) },
    };
    const moteMaterial = new THREE.ShaderMaterial({
      uniforms: moteUniforms,
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      vertexShader: /* glsl */ `
        uniform float uTime;
        uniform float uPixelRatio;
        uniform float uMoteSpeedMult;
        uniform vec2 uMoteTilt;
        attribute float aSeed;
        varying float vLife;
        varying float vWorldX;
        ${NOISE_GLSL}
        ${HEIGHT_GLSL}
        void main() {
          // Climb + drift; descent effect inflates time-scale via uMoteSpeedMult,
          // and the tilt vector adds a horizontal push opposite to motion so
          // the motes appear to "stream past" the camera.
          float climbDur = 9.0 + aSeed * 6.0;
          float climbPhase = mod(uTime * uMoteSpeedMult + aSeed * climbDur, climbDur) / climbDur;
          float h = climbPhase * ${MOTE_MAX_HEIGHT.toFixed(2)};
          vec2 driftUV = position.xz * 0.02 + vec2(uTime * 0.04, uTime * 0.03);
          float dx = vnoise(driftUV) * 1.5;
          float dz = vnoise(driftUV + vec2(31.7, 17.3)) * 1.5;
          // Tilt pushes against direction of motion — gives a "scenery passing"
          // feel. Scaled by climb-phase so the streak builds with the mote's age.
          vec2 tiltOffset = -uMoteTilt * (4.0 + 3.0 * (uMoteSpeedMult - 1.0)) * climbPhase;
          vec3 wp = vec3(position.x + dx + tiltOffset.x, 0.0, position.z + dz + tiltOffset.y);
          wp.y = terrainY(wp.xz) + 1.0 + h;
          vLife = 1.0 - climbPhase;
          vWorldX = wp.x;

          vec4 vp = viewMatrix * vec4(wp, 1.0);
          gl_Position = projectionMatrix * vp;
          gl_PointSize = uPixelRatio * (50.0 / max(1.0, -vp.z)) * (0.6 + vLife * 0.7);
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uMoteColors[7];
        varying float vLife;
        varying float vWorldX;
        ${REGION_WEIGHTS_GLSL}
        void main() {
          vec2 d = gl_PointCoord - 0.5;
          float r2 = dot(d, d);
          if (r2 > 0.25) discard;
          float alpha = pow(1.0 - r2 * 4.0, 1.8) * (0.35 + vLife * 0.6);
          float w0, w1, w2, w3, w4, w5, w6;
          regionWeights(vWorldX, w0, w1, w2, w3, w4, w5, w6);
          vec3 col =
            uMoteColors[0]*w0 + uMoteColors[1]*w1 + uMoteColors[2]*w2 +
            uMoteColors[3]*w3 + uMoteColors[4]*w4 + uMoteColors[5]*w5 +
            uMoteColors[6]*w6;
          gl_FragColor = vec4(col * (0.7 + vLife * 0.6), alpha);
        }
      `,
    });
    const motes = new THREE.Points(moteGeometry, moteMaterial);
    motes.frustumCulled = false;
    scene.add(motes);

    // ─── FPS controls ──────────────────────────────────────────────────────
    let yaw = -Math.PI / 2;  // facing +x (east, toward Locrian)
    let pitch = -0.05;
    const keys = new Set<string>();
    let pointerLocked = false;
    // Auto-walk state: toggles off when user takes pointer-lock. Direction
    // along X flips at the field ends. Z gently oscillates for variety.
    let autoWalkActive = autoWalk;
    let autoWalkDirX = +1;
    const autoWalkStartTime = performance.now() / 1000;
    // v0.6 takeover-callback latch — fired once when the user takes control
    // (pointer-lock click OR any movement key while auto-walking).
    let takeoverFired = false;
    const fireTakeover = () => {
      if (takeoverFired) return;
      takeoverFired = true;
      autoWalkActive = false;
      try {
        onUserTakeover?.();
      } catch (err) {
        console.warn('[ModalMeadow] onUserTakeover threw', err);
      }
    };

    const MOVE_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD',
                               'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);
    const onKeyDown = (e: KeyboardEvent) => {
      keys.add(e.code);
      // Even before pointer-lock, a movement key counts as takeover (v0.6).
      if (MOVE_KEYS.has(e.code)) fireTakeover();
    };
    const onKeyUp = (e: KeyboardEvent) => keys.delete(e.code);
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);

    const onMouseMove = (e: MouseEvent) => {
      if (!pointerLocked) return;
      yaw -= e.movementX * MOUSE_SENSITIVITY;
      pitch -= e.movementY * MOUSE_SENSITIVITY;
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
      if (pointerLocked) {
        // User has taken manual control — fire the v0.6 takeover-latch and
        // clear auto-walk so WASD wins from here on.
        fireTakeover();
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

    // ─── Chord-pulse state (per mode) ──────────────────────────────────────
    const pulseIntensityArr = grassUniforms.uChordPulse.value as Float32Array;
    const pulseDirArr = grassUniforms.uChordPulseDir.value as Float32Array;
    const pulseOriginArr = grassUniforms.uChordPulseOrigin.value as Float32Array;
    const pulseAgeArr = grassUniforms.uChordPulseAge.value as Float32Array;

    const handleChordPulse = (evt: ChordPulseEvent) => {
      const i = evt.modeIndex;
      if (i < 0 || i >= MODES.length) return;
      pulseIntensityArr[i] = 1.0;
      pulseAgeArr[i] = 0;
      // Map roman degree → direction: I/i → 0, IV/bII → +1, V/bvii → -1.
      if (evt.romanRoot === 4) {
        pulseDirArr[i] = 1.0;
      } else if (evt.romanRoot === 5 || evt.romanRoot === 7) {
        pulseDirArr[i] = -1.0;
      } else {
        pulseDirArr[i] = 0.0;
      }
      pulseOriginArr[i * 2] = camera.position.x;
      pulseOriginArr[i * 2 + 1] = camera.position.z;
    };
    const unsubscribePulse = audio.onChordPulse(handleChordPulse);

    let stopStubPulses: (() => void) | null = audio.startStubPulses(MODES);

    // ─── Debug hook (dev-only screenshots) ─────────────────────────────────
    // Exposes `window.__modalMeadowTeleport(x)` so dev/CI can grab a
    // screenshot of any region without driving pointer-lock + WASD.
    // Also `__modalMeadowSetAutoWalk(false)` to pause the auto-walk for
    // stable screenshots.
    interface DebugWindow extends Window {
      __modalMeadowTeleport?: (x: number, z?: number) => void;
      __modalMeadowSetAutoWalk?: (on: boolean) => void;
      __modalMeadowGetState?: () => unknown;
    }
    (window as DebugWindow).__modalMeadowTeleport = (x: number, z?: number) => {
      camera.position.x = x;
      if (typeof z === 'number') camera.position.z = z;
      camera.position.y = sampleTerrainY(camera.position.x, camera.position.z) + EYE_HEIGHT;
    };
    (window as DebugWindow).__modalMeadowSetAutoWalk = (on: boolean) => {
      autoWalkActive = on;
    };
    (window as DebugWindow).__modalMeadowGetState = () => ({
      x: camera.position.x,
      y: camera.position.y,
      z: camera.position.z,
      mode: dominantModeForX(camera.position.x).name,
      autoWalkActive,
    });

    // ─── Animation loop ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    let lastDominantName: string | null = null;
    let prevCamY = camera.position.y;
    // Descent-effect smoothed signed slope (negative = descending).
    // Updated each frame, then mapped to FOV / pitch / mote bumps.
    let smoothedDY = 0;
    // Locrian wind-reversal timer.
    let locrianFlipAccumulator = 0;
    let locrianFlipState = 1;

    const tick = () => {
      const dt = Math.min(clock.getDelta(), 0.1);
      const elapsed = clock.elapsedTime;
      grassUniforms.uTime.value = elapsed;
      moteUniforms.uTime.value = elapsed;
      pondUniforms.uTime.value = elapsed;

      // ─── Locrian wind reversal ────────────────────────────────────────────
      locrianFlipAccumulator += dt;
      if (locrianFlipAccumulator >= 4.0) {
        locrianFlipAccumulator -= 4.0;
        locrianFlipState = -locrianFlipState;
      }
      grassUniforms.uLocrianWindDir.value = locrianFlipState;

      // Camera rotation from yaw/pitch (YXZ order set on camera).
      camera.rotation.y = yaw;
      camera.rotation.x = pitch;

      // Movement: WASD when pointer-locked, otherwise auto-walk traverses
      // the full Lydian↔Locrian sweep.
      if (pointerLocked) {
        let mx = 0;
        let mz = 0;
        if (keys.has('KeyW')) mz -= 1;
        if (keys.has('KeyS')) mz += 1;
        if (keys.has('KeyA')) mx -= 1;
        if (keys.has('KeyD')) mx += 1;
        if (mx !== 0 || mz !== 0) {
          const len = Math.sqrt(mx * mx + mz * mz);
          mx /= len; mz /= len;
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
        }
      } else if (autoWalkActive) {
        // Travel along +/-X at AUTO_WALK_SPEED, reversing at field ends.
        const xEdge = FIELD_HALF_X - 5;
        camera.position.x += autoWalkDirX * AUTO_WALK_SPEED * dt;
        if (camera.position.x > xEdge) {
          camera.position.x = xEdge;
          autoWalkDirX = -1;
        } else if (camera.position.x < -xEdge) {
          camera.position.x = -xEdge;
          autoWalkDirX = +1;
        }
        // Gentle Z oscillation — keeps the camera from feeling on rails.
        const tWalk = (performance.now() / 1000 - autoWalkStartTime);
        camera.position.z = Math.sin(tWalk * 0.18) * 18.0;
        // Yaw slowly tracks direction of travel so the camera looks ahead.
        const targetYaw = autoWalkDirX > 0 ? -Math.PI / 2 : Math.PI / 2;
        yaw += (targetYaw - yaw) * Math.min(1, dt * 0.5);
      }
      // Soft bound — keep player inside the field.
      const halfX = FIELD_HALF_X;
      const halfZ = FIELD_SIZE_Z / 2 - 5;
      camera.position.x = Math.max(-halfX, Math.min(halfX, camera.position.x));
      camera.position.z = Math.max(-halfZ, Math.min(halfZ, camera.position.z));

      // Camera Y follows the terrain at eye height — every frame so the
      // descent effect always reads a real slope, even when looking around.
      //
      // Ground clamp (v1.2): sample terrain at camera XZ AND at three points
      // ahead along the camera's facing direction, and ride the MAX. With
      // 8m amplitude hills, sampling only at the camera's centre lets the
      // near-frustum clip through approaching hillsides — the geometric
      // centre is above its local terrain Y, but the upcoming hill face is
      // higher. Riding the max of (here, +1m, +2.5m, +4m ahead) lifts the
      // camera onto the approaching slope before its near plane reaches it.
      // Facing direction is a reasonable proxy for movement direction:
      // auto-walk eases yaw toward velocity each frame, and pure-rotation
      // browsing still wants "ahead = where you're looking" so the camera
      // never tilts to reveal sub-terrain.
      camera.getWorldDirection(groundClampDirScratch);
      groundClampDirScratch.y = 0;
      const horizLen2 =
        groundClampDirScratch.x * groundClampDirScratch.x +
        groundClampDirScratch.z * groundClampDirScratch.z;
      let groundY = sampleTerrainY(camera.position.x, camera.position.z);
      if (horizLen2 > 1e-6) {
        const invLen = 1 / Math.sqrt(horizLen2);
        const dirX = groundClampDirScratch.x * invLen;
        const dirZ = groundClampDirScratch.z * invLen;
        // 1.0 / 2.5 / 4.0 m — covers the near-plane (0.1m) plus reasonable
        // step-ahead at WALK_SPEED 5 m/s. Tight enough to avoid the
        // "invisible wall" feel of clamping to a distant peak.
        const camX = camera.position.x;
        const camZ = camera.position.z;
        const a1 = sampleTerrainY(camX + dirX * 1.0, camZ + dirZ * 1.0);
        if (a1 > groundY) groundY = a1;
        const a2 = sampleTerrainY(camX + dirX * 2.5, camZ + dirZ * 2.5);
        if (a2 > groundY) groundY = a2;
        const a3 = sampleTerrainY(camX + dirX * 4.0, camZ + dirZ * 4.0);
        if (a3 > groundY) groundY = a3;
      }
      const targetY = groundY + EYE_HEIGHT;
      camera.position.y = targetY;

      // ─── Descent effect ───────────────────────────────────────────────────
      // Track signed dY per frame (smoothed). Map to:
      //   pitch glide  — bus detune cents, negative for descent (≤ -50 cents)
      //   mote speed   — descend ⇒ accel 2× when slope steep; ascend ⇒ slow
      //   FOV bump     — +5° on steep descent, -2° on ascent
      // Slope magnitude gating: |dy| / dt must exceed 0.4 m/s before we apply
      // anything (flat walking gets baseline).
      const dYThisFrame = camera.position.y - prevCamY;
      prevCamY = camera.position.y;
      const dySmoothing = Math.exp(-dt / 0.20); // ~0.2s time constant
      smoothedDY = smoothedDY * dySmoothing + dYThisFrame * (1 - dySmoothing);
      const slopeMag = Math.abs(smoothedDY) / Math.max(dt, 1e-6);  // m/s vertical
      const slopeSign = smoothedDY < 0 ? -1 : (smoothedDY > 0 ? 1 : 0);
      const slopeT = Math.max(0, Math.min(1, (slopeMag - 0.4) / 1.4));
      // Pitch glide: descent = -50 cents max, ascent = +20 cents (asymmetric
      // because UP is supposed to be subtler per brief). Apply to all buses.
      const centsTarget = slopeSign < 0
        ? -50 * slopeT
        : +20 * slopeT * 0.6;
      // Build the per-bus cents array; same value across all buses keeps the
      // global "scenery slope" feel without sounding microtonal.
      const centsArr: number[] = new Array(MODES.length);
      for (let i = 0; i < MODES.length; i++) centsArr[i] = centsTarget;
      audio.setDetune(centsArr);
      // Mote speed multiplier: descent 1→2x, ascent 1→0.5x.
      const moteMult = slopeSign < 0
        ? 1.0 + 1.0 * slopeT
        : 1.0 - 0.5 * slopeT;
      moteUniforms.uMoteSpeedMult.value = moteMult;
      // Mote tilt = horizontal direction of motion (used by mote shader to
      // push particles opposite the motion so it reads as scenery passing).
      let motionX = 0;
      let motionZ = 0;
      if (autoWalkActive && !pointerLocked) {
        motionX = autoWalkDirX;
      } else if (pointerLocked) {
        // Approx direction = forward projected by recent WASD
        const cosY = Math.cos(yaw);
        const sinY = Math.sin(yaw);
        motionX = -sinY;
        motionZ = -cosY;
      }
      moteUniforms.uMoteTilt.value.set(motionX * slopeT, motionZ * slopeT);
      // FOV bump: +5° descent, -2° ascent. Lerp toward target over ~0.4s.
      const fovTarget = BASE_FOV_DEG + (slopeSign < 0 ? 5 * slopeT : -2 * slopeT);
      camera.fov += (fovTarget - camera.fov) * Math.min(1, dt / 0.4);
      camera.updateProjectionMatrix();

      // ─── Per-region weights drive audio + sky + sun + fog ────────────────
      const weights = modeWeightsForX(camera.position.x);
      audio.setWeights(weights);
      skyUniforms.uCameraX.value = camera.position.x;

      // Sun interpolation across 7 modes — weighted average of per-region
      // position + colour + intensity.
      sunDirScratch.set(0, 0, 0);
      sunColorScratch.setRGB(0, 0, 0);
      let intensityAcc = 0;
      for (let i = 0; i < MODES.length; i++) {
        const w = weights[i];
        sunDirScratch.x += SUN_POSITIONS[i].x * w;
        sunDirScratch.y += SUN_POSITIONS[i].y * w;
        sunDirScratch.z += SUN_POSITIONS[i].z * w;
        sunColorScratch.r += SUN_COLORS[i].r * w;
        sunColorScratch.g += SUN_COLORS[i].g * w;
        sunColorScratch.b += SUN_COLORS[i].b * w;
        intensityAcc += SUN_INTENSITIES[i] * w;
      }
      sun.position.copy(sunDirScratch);
      sun.color.copy(sunColorScratch);
      sun.intensity = intensityAcc;
      groundUniforms.uSunDir.value.copy(sunDirScratch).normalize();
      groundUniforms.uSunColor.value.copy(sunColorScratch);

      // ─── Chord pulse decay ───────────────────────────────────────────────
      const PULSE_DECAY = Math.exp(-dt / 0.18);
      for (let i = 0; i < MODES.length; i++) {
        pulseIntensityArr[i] *= PULSE_DECAY;
        pulseAgeArr[i] += dt;
      }

      // Fog colour tracks the local sky (weighted average across regions).
      let fr = 0, fg = 0, fb = 0;
      for (let i = 0; i < MODES.length; i++) {
        fr += MODES[i].skyColor.r * weights[i];
        fg += MODES[i].skyColor.g * weights[i];
        fb += MODES[i].skyColor.b * weights[i];
      }
      fog.color.setRGB(fr, fg, fb);
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
      unsubscribePulse();
      if (stopStubPulses) stopStubPulses();
      audio.dispose();
      delete (window as DebugWindow).__modalMeadowTeleport;
      delete (window as DebugWindow).__modalMeadowSetAutoWalk;
      delete (window as DebugWindow).__modalMeadowGetState;
      grassMeshes.forEach((m) => m.geometry.dispose());
      grassMaterial.dispose();
      bladeGeometry.dispose();
      ground.geometry.dispose();
      groundMaterial.dispose();
      pondGeometry.dispose();
      pondMaterial.dispose();
      sky.geometry.dispose();
      skyMaterial.dispose();
      moteGeometry.dispose();
      moteMaterial.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
    // Intentionally exhaustive — autoWalk is captured at mount time; toggling
    // it later would require an explicit refresh.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [onModeChange, onLockChange, onUserTakeover]);

  return <Box ref={containerRef} sx={{ width: '100%', height: '100%', overflow: 'hidden' }} />;
};

export default ModalMeadow;
