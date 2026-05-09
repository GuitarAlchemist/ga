/**
 * Stonehenge — restored, original, not-ruined.
 *
 * Reconstructs the monument as it would have stood ~2500 BCE:
 *
 *  - Outer Sarsen Circle: 30 vertical sarsen uprights (~4.1m × 2.1m × 1.1m)
 *    capped by a continuous ring of curved lintels. Mortise/tenon joints
 *    are implied visually — lintels rest squarely on the uprights with no
 *    gaps. Diameter ~33m.
 *  - Sarsen Trilithon Horseshoe: 5 trilithons (two uprights + one lintel
 *    each), opening toward the NE summer-solstice sunrise. The central
 *    Great Trilithon is the tallest at ~7.3m above ground.
 *  - Bluestone Circle: ~30 smaller bluestones between the outer sarsen
 *    ring and the trilithon horseshoe. Bluestones are tinted slightly
 *    cooler than sarsens — not actually blue, but enough to read as a
 *    different rock type at a glance.
 *  - Heel Stone: a single tilted sarsen marker outside the entrance,
 *    placed on the solstice sunrise alignment.
 *  - Salisbury Plain ground, distant rolling hills, day/night cycle, and
 *    a flock of ravens that wheels overhead at dusk.
 *
 * Stone surface variation comes from per-fragment noise — each stone uses
 * the same shader, but world-space noise + per-instance random offsets
 * produce visually distinct weathering patterns without needing per-stone
 * geometry.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { Box } from '@mui/material';

export interface StonehengeProps {
  width?: number;
  height?: number;
  /** Loop length in seconds for the day/night cycle. 0 freezes time. */
  dayLengthSeconds?: number;
  /** When dayLengthSeconds=0, fixed time-of-day in [0,1). 0=sunrise, 0.25=noon, 0.5=sunset, 0.75=midnight. */
  fixedTimeOfDay?: number;
  /** Auto-orbit the camera. */
  autoRotate?: boolean;
  /** Whether to render the raven flock at dusk. */
  ravens?: boolean;
}

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
  float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
      v += a * vnoise(p);
      p *= 2.0;
      a *= 0.5;
    }
    return v;
  }
`;

// ─── Stone shader ────────────────────────────────────────────────────────
// Used by every stone (sarsen + bluestone + heel). The bluestone variant
// just passes a different uColorBase / uColorBright pair. Per-instance
// random offsets (uOffset uniform, set per-mesh) shift the noise so the
// same shader produces visually distinct stones.

const STONE_VERTEX = /* glsl */ `
  ${NOISE_GLSL}

  uniform float uRoughness;   // vertex displacement amplitude
  uniform vec3  uOffset;      // per-stone noise offset

  varying vec3 vWorld;
  varying vec3 vLocal;
  varying vec3 vNormal;

  void main() {
    vec3 p = position;
    // Subtle vertex-noise displacement so stone faces aren't perfectly
    // flat. Effect is largest on big faces (long axes), smallest near
    // edges. Computed in local space so weathering doesn't swim with the
    // camera.
    float n = fbm(position.xy * 0.6 + uOffset.xy)
            + fbm(position.yz * 0.7 + uOffset.yz);
    p += normal * (n * uRoughness * 0.10);

    vec4 wp = modelMatrix * vec4(p, 1.0);
    vWorld = wp.xyz;
    vLocal = position;
    vNormal = normalize(normalMatrix * normal);
    gl_Position = projectionMatrix * viewMatrix * wp;
  }
`;

const STONE_FRAGMENT = /* glsl */ `
  ${NOISE_GLSL}

  uniform vec3  uColorBase;    // dark stone
  uniform vec3  uColorBright;  // weathered highlight
  uniform vec3  uLichen;       // lichen tint (greenish on sarsens, rusty on bluestones)
  uniform vec3  uOffset;
  uniform vec3  uSunDir;
  uniform vec3  uSunColor;
  uniform vec3  uAmbient;

  varying vec3 vWorld;
  varying vec3 vLocal;
  varying vec3 vNormal;

  void main() {
    // Base stone color: two-octave noise mixing dark↔bright, modulated
    // by a slow "weathering" field so the same shader gives every stone
    // its own gross color trend.
    vec2 uv = vLocal.xy * 1.4 + uOffset.xy;
    float nA = fbm(uv);
    float nB = fbm(vLocal.yz * 1.8 + uOffset.yz);
    float wear = 0.5 * (nA + nB);

    vec3 col = mix(uColorBase, uColorBright, wear * 0.85 + 0.15);

    // Sedimentary bands, hairline cracks, and granular flecks. These stay
    // procedural so the scene has material detail without external textures.
    float strata = smoothstep(0.82, 0.97, abs(sin(vLocal.y * 6.5 + fbm(vWorld.xz * 0.42 + uOffset.xy) * 2.8)));
    float crackField = fbm(vWorld.xy * 1.55 + uOffset.yx);
    float cracks = smoothstep(0.60, 0.82, crackField) * smoothstep(0.18, 1.0, abs(vNormal.y - 0.25));
    float fleck = smoothstep(0.52, 0.74, fbm(vWorld.xy * 4.5 + uOffset.xy * 2.0));
    col *= 1.0 - strata * 0.08 - cracks * 0.18;
    col = mix(col, uColorBright * 1.08, fleck * 0.10);

    // Lichen patches — clustered on north-ish faces (vNormal.z signed)
    // and concentrated near the bottom (where moisture pools). Lichens
    // get a tighter noise so the patches read as discrete blooms, not a
    // gradient.
    float lichenN = fbm(vWorld.xz * 0.6 + uOffset.xz);
    float lichenMask = smoothstep(0.55, 0.78, lichenN);
    float wetnessMask = smoothstep(2.5, 0.0, vWorld.y);          // bottom-heavy
    float facingMask  = smoothstep(0.2, 0.8, max(-vNormal.x, max(vNormal.z, 0.0)));
    float lichen = lichenMask * (0.4 + wetnessMask * 0.4 + facingMask * 0.4);
    col = mix(col, uLichen, clamp(lichen * 0.65, 0.0, 0.55));

    // Edge AO — ambient occlusion approximation: faces that point steeply
    // sideways get darkened slightly so corners look dirtier than flats.
    float ao = 0.85 + 0.15 * clamp(vNormal.y, 0.0, 1.0);
    ao *= 0.92 + 0.08 * smoothstep(-0.2, 0.7, vNormal.y);
    col *= ao;

    // Lighting (Lambert + ambient with wrap).
    vec3 N = normalize(vNormal);
    vec3 L = normalize(uSunDir);
    float lit = max(dot(N, L), 0.0);
    float wrap = lit * 0.5 + 0.5;
    col = col * (uAmbient * 0.45 + uSunColor * wrap);

    gl_FragColor = vec4(col, 1.0);
  }
`;

// Build a stone mesh with a roughened box geometry. Every stone shares
// the same single base geometry; per-stone variation is provided via
// uOffset uniform. To keep things efficient we use a ShaderMaterial
// pre-compiled once and clone uniforms per stone.

interface StoneOptions {
  w: number; h: number; d: number;
  segments?: number;
  roughness?: number;
  type: 'sarsen' | 'bluestone' | 'heel';
  receiveShadow?: boolean;
}

const Stonehenge: React.FC<StonehengeProps> = ({
  width,
  height,
  dayLengthSeconds = 120,
  fixedTimeOfDay = 0.18,
  autoRotate = true,
  ravens = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = width ?? container.clientWidth ?? 1280;
    const H0 = height ?? container.clientHeight ?? 720;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0xb8c8e0, 0.0042);

    const camera = new THREE.PerspectiveCamera(47, W0 / H0, 0.1, 4000);
    camera.position.set(34, 13, 35);
    camera.lookAt(0, 5, 0);

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
    controls.minDistance = 10;
    controls.maxDistance = 150;
    controls.maxPolarAngle = Math.PI * 0.485;
    controls.target.set(0, 3.5, 0);
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 0.13;

    // ─── Sky dome ──────────────────────────────────────────────────────────
    const skyUniforms = {
      uSunDir: { value: new THREE.Vector3(0, 1, 0) },
      uTimeOfDay: { value: fixedTimeOfDay },
    };

    const skyMaterial = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: skyUniforms,
      vertexShader: /* glsl */ `
        varying vec3 vDir;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vDir = normalize(wp.xyz);
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uSunDir;
        varying vec3 vDir;
        // English-summer palette — paler day, lavender dusk, deep blue night.
        vec3 skyDay(float h)   { return mix(vec3(0.78, 0.85, 0.95), vec3(0.30, 0.50, 0.84), pow(clamp(h,0.0,1.0), 0.6)); }
        vec3 skyDusk(float h)  { return mix(vec3(1.00, 0.55, 0.32), vec3(0.30, 0.18, 0.40), pow(clamp(h,0.0,1.0), 0.55)); }
        vec3 skyNight(float h) { return mix(vec3(0.05, 0.06, 0.13), vec3(0.005, 0.01, 0.04), pow(clamp(h,0.0,1.0), 0.7)); }
        void main() {
          vec3 d = normalize(vDir);
          float h = clamp(d.y, 0.0, 1.0);
          float dayW   = smoothstep(-0.05, 0.30, uSunDir.y);
          float duskW  = exp(-pow(uSunDir.y * 4.0, 2.0));
          float nightW = smoothstep(0.05, -0.10, uSunDir.y);
          vec3 col = skyDay(h)*dayW + skyDusk(h)*duskW + skyNight(h)*nightW;

          float sunDot = clamp(dot(d, normalize(uSunDir)), 0.0, 1.0);
          float disc = smoothstep(0.9985, 0.9999, sunDot);
          float glow = pow(sunDot, 20.0) * 0.40 + pow(sunDot, 4.0) * 0.10;
          vec3 sunCol = mix(vec3(1.0, 0.55, 0.25), vec3(1.0, 0.96, 0.86), dayW);
          col += sunCol * disc * 6.0 + sunCol * glow * (0.7 + duskW * 1.5);

          float starN = fract(sin(dot(d.xz * 600.0, vec2(12.989, 78.233))) * 43758.55);
          col += vec3(smoothstep(0.9965, 0.9990, starN) * nightW * smoothstep(0.05, 0.4, h));

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(1500, 32, 16), skyMaterial);
    scene.add(sky);

    // ─── Lights ────────────────────────────────────────────────────────────
    const ambient = new THREE.HemisphereLight(0xc0d4ff, 0x4a3920, 0.6);
    scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xfff1d8, 1.5);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.left = -50;
    sun.shadow.camera.right = 50;
    sun.shadow.camera.top = 50;
    sun.shadow.camera.bottom = -50;
    sun.shadow.camera.near = 1;
    sun.shadow.camera.far = 300;
    sun.shadow.bias = -0.0006;
    sun.shadow.radius = 4;
    scene.add(sun);
    scene.add(sun.target);

    // ─── Salisbury Plain ground ────────────────────────────────────────────
    const groundSize = 1200;
    const groundGeo = new THREE.PlaneGeometry(groundSize, groundSize, 160, 160);
    groundGeo.rotateX(-Math.PI / 2);
    const terrainHeight = (x: number, z: number) => {
      const r = Math.sqrt(x * x + z * z);
      const flat = THREE.MathUtils.smoothstep(r, 30, 80);
      return (
        Math.sin(x * 0.012) +
        Math.cos(z * 0.009) * 0.7 +
        Math.sin(x * 0.04 + z * 0.03) * 0.4
      ) * 1.6 * flat;
    };
    {
      // Gentle rolling rise/fall — Salisbury Plain isn't dead-flat.
      const pos = groundGeo.attributes.position as THREE.BufferAttribute;
      for (let i = 0; i < pos.count; i++) {
        const x = pos.getX(i);
        const z = pos.getZ(i);
        pos.setY(i, terrainHeight(x, z));
      }
      pos.needsUpdate = true;
      groundGeo.computeVertexNormals();
    }

    const groundMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uGrassA: { value: new THREE.Color(0x6f9140) },
        uGrassB: { value: new THREE.Color(0x314f24) },
        uGrassC: { value: new THREE.Color(0xa1b75e) },
        uChalk:  { value: new THREE.Color(0xd8cfad) }, // chalky pale soil patches
        uSoil:   { value: new THREE.Color(0x594833) },
        uSunDir: skyUniforms.uSunDir,
        uSunColor: { value: new THREE.Color(0xfff1d8) },
        uAmbient:  { value: new THREE.Color(0x506890) },
      },
      vertexShader: /* glsl */ `
        varying vec3 vWorld;
        varying vec3 vNormal;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorld = wp.xyz;
          vNormal = normalize(normalMatrix * normal);
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uGrassA, uGrassB, uGrassC, uChalk, uSoil;
        uniform vec3 uSunDir, uSunColor, uAmbient;
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}
        void main() {
          vec2 xz = vWorld.xz;
          float r = length(xz);
          float patchy = fbm(xz * 0.055);
          float broad = fbm(xz * 0.012 + vec2(7.0, 3.0));
          vec3 col = mix(uGrassA, uGrassB, patchy * 0.82 + 0.08);
          col = mix(col, uGrassC, smoothstep(0.18, 0.72, broad) * 0.28);

          // Fine anisotropic grass streaks so the field reads as turf instead
          // of a smooth green plane.
          float bladeA = sin(xz.x * 8.0 + fbm(xz * 0.18) * 5.0);
          float bladeB = sin((xz.x * 0.45 + xz.y) * 11.0 + broad * 4.0);
          float blade = smoothstep(0.45, 1.0, bladeA * 0.5 + bladeB * 0.5);
          col = mix(col, col * vec3(0.78, 0.90, 0.68), blade * 0.20);

          // Trampled chalk and soil around the restored monument, plus the
          // solstice avenue toward the Heel Stone. These marks give the scale
          // and layout something visible to sit in.
          vec2 sunrise = normalize(vec2(1.0, 1.0));
          float along = dot(xz, sunrise);
          float side = abs(xz.x * sunrise.y - xz.y * sunrise.x);
          float avenue = smoothstep(3.3, 0.6, side) * smoothstep(5.0, 15.0, along) * smoothstep(42.0, 31.0, along);
          float outerRing = 1.0 - smoothstep(0.35, 2.4, abs(r - 18.6));
          float innerRing = 1.0 - smoothstep(0.25, 1.6, abs(r - 11.6));
          float centerWear = smoothstep(15.5, 4.0, r);
          float chalkN = fbm(xz * 0.16 + vec2(2.0, 9.0));
          float chalk = max(max(outerRing * 0.55, innerRing * 0.36), avenue * 0.62);
          chalk += smoothstep(0.62, 0.80, chalkN) * 0.18;
          col = mix(col, uSoil, centerWear * 0.22);
          col = mix(col, uChalk, clamp(chalk, 0.0, 0.72));

          // Subtle contact darkening under the main stones.
          float contact = max(outerRing * 0.18, innerRing * 0.14) + centerWear * 0.08;
          col *= 1.0 - contact;

          float lit = max(dot(vNormal, normalize(uSunDir)), 0.0);
          float hemi = 0.52 + 0.48 * clamp(vNormal.y, 0.0, 1.0);
          col = col * (uAmbient * 0.50 + uSunColor * (lit * 0.72 + 0.34) * hemi);
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const ground = new THREE.Mesh(groundGeo, groundMaterial);
    ground.receiveShadow = true;
    scene.add(ground);

    // Foreground grass is built AFTER the stones — see "Foreground grass —
    // FluffyGrass-style bezier blades" further down. Ground material runs
    // first because grass uniform colors don't depend on it.

    // ─── Distant rolling hills ─────────────────────────────────────────────
    const hillSegs = 256;
    const hillR = 700;
    const hillPos: number[] = [];
    const hillPeak = (a: number) => {
      const h = 18 + Math.sin(a * 5.1) * 9 + Math.sin(a * 11.3 + 1.2) * 5 + Math.sin(a * 23.1 + 2.4) * 2.5;
      return Math.max(2, h);
    };
    for (let i = 0; i < hillSegs; i++) {
      const a0 = (i / hillSegs) * Math.PI * 2;
      const a1 = ((i + 1) / hillSegs) * Math.PI * 2;
      const h0 = hillPeak(a0);
      const h1 = hillPeak(a1);
      const x0 = Math.cos(a0) * hillR; const z0 = Math.sin(a0) * hillR;
      const x1 = Math.cos(a1) * hillR; const z1 = Math.sin(a1) * hillR;
      const yBase = -2;
      hillPos.push(x0, yBase, z0,  x1, yBase, z1,  x1, h1, z1);
      hillPos.push(x0, yBase, z0,  x1, h1, z1,    x0, h0, z0);
    }
    const hillGeo = new THREE.BufferGeometry();
    hillGeo.setAttribute('position', new THREE.Float32BufferAttribute(hillPos, 3));
    const hillMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uHorizonDay:   { value: new THREE.Color(0.78, 0.85, 0.95) },
        uHorizonDusk:  { value: new THREE.Color(1.00, 0.55, 0.32) },
        uHorizonNight: { value: new THREE.Color(0.05, 0.06, 0.13) },
        uDayW: { value: 0 }, uDuskW: { value: 0 }, uNightW: { value: 0 },
      },
      vertexShader: /* glsl */ `
        varying float vH;
        void main() {
          vH = position.y;
          gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uHorizonDay, uHorizonDusk, uHorizonNight;
        uniform float uDayW, uDuskW, uNightW;
        varying float vH;
        void main() {
          vec3 h = uHorizonDay*uDayW + uHorizonDusk*uDuskW + uHorizonNight*uNightW;
          float alt = clamp(vH / 28.0, 0.0, 1.0);
          vec3 col = mix(h * 0.30, h * 0.78, alt);
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const hills = new THREE.Mesh(hillGeo, hillMaterial);
    hills.frustumCulled = false;
    scene.add(hills);

    // ─── Stone factory ─────────────────────────────────────────────────────
    // Single shared geometry per stone-shape — we just clone the material
    // (so per-stone uniforms can differ) and slot each instance into a
    // group so it can be transformed independently. With ~100 stones total
    // (30 outer + 30 lintels + 10 trilithon parts + 30 bluestones + 1 heel)
    // this is fine for desktop and mobile alike.

    const buildStone = (opt: StoneOptions): THREE.Mesh => {
      const seg = opt.segments ?? 6;
      const geo = new THREE.BoxGeometry(opt.w, opt.h, opt.d, seg, seg, seg);
      // Pre-displace verts a little so each stone has unique outline.
      const pos = geo.attributes.position as THREE.BufferAttribute;
      const off = new THREE.Vector3(Math.random() * 17, Math.random() * 13, Math.random() * 11);
      for (let i = 0; i < pos.count; i++) {
        const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i);
        const r = (Math.sin(x * 0.7 + off.x) + Math.cos(y * 0.5 + off.y) + Math.sin(z * 0.6 + off.z));
        const amp = 0.05 * Math.min(opt.w, opt.h, opt.d);
        // Don't displace the very top or bottom — we want flat-ish caps so
        // lintels actually rest on a face.
        const yt = Math.abs(y) / (opt.h / 2);
        const fade = 1 - Math.pow(yt, 6);
        pos.setX(i, x + r * amp * 0.35 * fade);
        pos.setY(i, y + r * amp * 0.15 * fade);
        pos.setZ(i, z + r * amp * 0.35 * fade);
      }
      pos.needsUpdate = true;
      geo.computeVertexNormals();

      const isBluestone = opt.type === 'bluestone';
      const material = new THREE.ShaderMaterial({
        uniforms: {
          uColorBase:   { value: isBluestone ? new THREE.Color(0x4a5566) : new THREE.Color(0x575048) },
          uColorBright: { value: isBluestone ? new THREE.Color(0x7d8294) : new THREE.Color(0x8a8175) },
          uLichen:      { value: isBluestone ? new THREE.Color(0x6a7860) : new THREE.Color(0x6e8047) },
          uOffset:      { value: new THREE.Vector3(off.x, off.y, off.z) },
          uRoughness:   { value: opt.roughness ?? 1.0 },
          uSunDir:      skyUniforms.uSunDir,
          uSunColor:    { value: new THREE.Color(0xfff1d8) },
          uAmbient:     { value: new THREE.Color(0x506890) },
        },
        vertexShader: STONE_VERTEX,
        fragmentShader: STONE_FRAGMENT,
      });

      const mesh = new THREE.Mesh(geo, material);
      mesh.castShadow = true;
      mesh.receiveShadow = opt.receiveShadow ?? true;
      return mesh;
    };

    // ─── Outer Sarsen Circle (30 uprights + 30 lintels) ────────────────────
    const SARSEN_RING_R = 16.5;
    const SARSEN_COUNT = 30;
    const SARSEN_W = 2.1;
    const SARSEN_H = 4.1;
    const SARSEN_D = 1.1;
    const LINTEL_H = 0.8;
    const LINTEL_D = 1.0;

    const stoneMats: THREE.ShaderMaterial[] = [];

    const sarsenGroup = new THREE.Group();
    scene.add(sarsenGroup);

    // Place uprights tangent to the circle (long face faces outward).
    const sarsenPositions: { x: number; z: number; theta: number }[] = [];
    for (let i = 0; i < SARSEN_COUNT; i++) {
      const theta = (i / SARSEN_COUNT) * Math.PI * 2;
      const x = Math.cos(theta) * SARSEN_RING_R;
      const z = Math.sin(theta) * SARSEN_RING_R;
      sarsenPositions.push({ x, z, theta });

      const upright = buildStone({
        w: SARSEN_W, h: SARSEN_H, d: SARSEN_D,
        type: 'sarsen', roughness: 0.7,
      });
      upright.position.set(x, SARSEN_H / 2, z);
      upright.rotation.y = -theta + Math.PI / 2; // long face faces tangent (outward)
      sarsenGroup.add(upright);
      stoneMats.push(upright.material as THREE.ShaderMaterial);
    }

    // Lintels — each spans the chord between two adjacent uprights.
    for (let i = 0; i < SARSEN_COUNT; i++) {
      const a = sarsenPositions[i];
      const b = sarsenPositions[(i + 1) % SARSEN_COUNT];
      const cx = (a.x + b.x) / 2;
      const cz = (a.z + b.z) / 2;
      const dx = b.x - a.x;
      const dz = b.z - a.z;
      const chordLen = Math.sqrt(dx * dx + dz * dz);
      const dirAngle = Math.atan2(dz, dx);

      const lintel = buildStone({
        w: chordLen + SARSEN_W * 0.2,        // overlap onto each upright
        h: LINTEL_H,
        d: LINTEL_D,
        type: 'sarsen', roughness: 0.55,
      });
      lintel.position.set(cx, SARSEN_H + LINTEL_H / 2, cz);
      lintel.rotation.y = -dirAngle;
      sarsenGroup.add(lintel);
      stoneMats.push(lintel.material as THREE.ShaderMaterial);
    }

    // ─── Trilithon Horseshoe (5 trilithons) ────────────────────────────────
    // Heights asymmetric — central trilithon (Great Trilithon) is tallest
    // at ~7.3m. Horseshoe opens to the NE (sunrise on summer solstice).
    const TRILITHON_DATA: Array<{
      angle: number;       // angle of horseshoe opening (radians, NE = π/4)
      height: number;
      width: number;
      depth: number;
      gap: number;         // gap between paired uprights
      radius: number;      // distance from center to trilithon midpoint
    }> = [
      // Outer trilithons (smaller), then inner (taller). Order around the horseshoe:
      { angle: -Math.PI * 0.78, height: 6.7, width: 2.3, depth: 1.2, gap: 1.4, radius: 8.0 },
      { angle: -Math.PI * 0.55, height: 7.0, width: 2.4, depth: 1.3, gap: 1.4, radius: 7.0 },
      { angle:  Math.PI * 1.0,  height: 7.3, width: 2.5, depth: 1.4, gap: 1.5, radius: 6.5 },  // Great Trilithon
      { angle:  Math.PI * 0.55, height: 7.0, width: 2.4, depth: 1.3, gap: 1.4, radius: 7.0 },
      { angle:  Math.PI * 0.78, height: 6.7, width: 2.3, depth: 1.2, gap: 1.4, radius: 8.0 },
    ];

    const trilithonGroup = new THREE.Group();
    scene.add(trilithonGroup);

    for (const t of TRILITHON_DATA) {
      const cx = Math.cos(t.angle) * t.radius;
      const cz = Math.sin(t.angle) * t.radius;
      // Pair faces inward (toward center). Tangent direction = perpendicular
      // to the radial. Each upright sits offset along the tangent by
      // ±(width + gap)/2.
      const tangentX = -Math.sin(t.angle);
      const tangentZ =  Math.cos(t.angle);
      const halfSep = (t.width + t.gap) / 2;

      for (const sign of [-1, 1] as const) {
        const ux = cx + tangentX * halfSep * sign;
        const uz = cz + tangentZ * halfSep * sign;
        const upright = buildStone({
          w: t.width, h: t.height, d: t.depth,
          type: 'sarsen', roughness: 0.55,
        });
        upright.position.set(ux, t.height / 2, uz);
        upright.rotation.y = -t.angle - Math.PI / 2;
        trilithonGroup.add(upright);
        stoneMats.push(upright.material as THREE.ShaderMaterial);
      }

      // Lintel atop the pair.
      const lintel = buildStone({
        w: t.width * 2 + t.gap + 0.4,
        h: 0.85,
        d: t.depth + 0.1,
        type: 'sarsen', roughness: 0.5,
      });
      lintel.position.set(cx, t.height + 0.42, cz);
      lintel.rotation.y = -t.angle - Math.PI / 2;
      trilithonGroup.add(lintel);
      stoneMats.push(lintel.material as THREE.ShaderMaterial);
    }

    // ─── Bluestone Circle + Horseshoe ──────────────────────────────────────
    const BLUESTONE_RING_R = 11.5;
    const BLUESTONE_COUNT = 28;
    for (let i = 0; i < BLUESTONE_COUNT; i++) {
      const theta = (i / BLUESTONE_COUNT) * Math.PI * 2;
      const x = Math.cos(theta) * BLUESTONE_RING_R;
      const z = Math.sin(theta) * BLUESTONE_RING_R;
      const h = 1.6 + Math.random() * 0.7;
      const w = 0.6 + Math.random() * 0.4;
      const d = 0.5 + Math.random() * 0.3;
      const stone = buildStone({
        w, h, d,
        type: 'bluestone', roughness: 0.9,
      });
      // Slight outward lean for variety.
      stone.position.set(x, h / 2, z);
      stone.rotation.y = -theta + Math.PI / 2 + (Math.random() - 0.5) * 0.2;
      stone.rotation.z = (Math.random() - 0.5) * 0.05;
      scene.add(stone);
      stoneMats.push(stone.material as THREE.ShaderMaterial);
    }

    // Inner bluestone horseshoe — smaller, follows trilithon radii.
    const INNER_BLUE_COUNT = 19;
    for (let i = 0; i < INNER_BLUE_COUNT; i++) {
      // Angle spans roughly the same horseshoe opening as the trilithons.
      const t = i / (INNER_BLUE_COUNT - 1);
      const angle = -Math.PI * 0.78 + t * (Math.PI * 1.56);
      const r = 4.5 + Math.sin(t * Math.PI) * 0.6;
      const x = Math.cos(angle) * r;
      const z = Math.sin(angle) * r;
      const h = 1.4 + Math.random() * 0.5;
      const stone = buildStone({
        w: 0.5 + Math.random() * 0.3,
        h, d: 0.45,
        type: 'bluestone', roughness: 0.95,
      });
      stone.position.set(x, h / 2, z);
      stone.rotation.y = -angle - Math.PI / 2 + (Math.random() - 0.5) * 0.2;
      scene.add(stone);
      stoneMats.push(stone.material as THREE.ShaderMaterial);
    }

    // ─── Heel Stone ────────────────────────────────────────────────────────
    // Sits NE of the circle, leaning slightly. Marks the summer solstice
    // sunrise alignment with the centre of the monument.
    {
      const heel = buildStone({
        w: 2.4, h: 4.9, d: 2.1,
        type: 'heel', roughness: 1.4,
      });
      const hAngle = Math.PI * 0.25; // NE
      const hR = 30;
      heel.position.set(Math.cos(hAngle) * hR, 4.9 / 2, Math.sin(hAngle) * hR);
      heel.rotation.y = -hAngle;
      heel.rotation.z = -0.10; // slight lean
      scene.add(heel);
      stoneMats.push(heel.material as THREE.ShaderMaterial);
    }

    // ─── Foreground grass — FluffyGrass-style bezier blades ────────────────
    // Real instanced bezier blades with bend / wind / per-instance variation,
    // placed densely around the monument and rejection-sampled against
    // stone footprints so blades don't grow up through sarsens.
    //
    // Stone exclusions are computed analytically from the same parametric
    // formulas that placed the stones above. Each entry is (x, z, radius).
    const stoneExclusions: Array<{ x: number; z: number; r: number }> = [];

    for (let i = 0; i < SARSEN_COUNT; i++) {
      const theta = (i / SARSEN_COUNT) * Math.PI * 2;
      stoneExclusions.push({
        x: Math.cos(theta) * SARSEN_RING_R,
        z: Math.sin(theta) * SARSEN_RING_R,
        r: 1.6,
      });
    }
    for (const t of TRILITHON_DATA) {
      const cx = Math.cos(t.angle) * t.radius;
      const cz = Math.sin(t.angle) * t.radius;
      const tx = -Math.sin(t.angle);
      const tz =  Math.cos(t.angle);
      const halfSep = (t.width + t.gap) / 2;
      for (const sign of [-1, 1]) {
        stoneExclusions.push({
          x: cx + tx * halfSep * sign,
          z: cz + tz * halfSep * sign,
          r: 1.8,
        });
      }
    }
    for (let i = 0; i < BLUESTONE_COUNT; i++) {
      const theta = (i / BLUESTONE_COUNT) * Math.PI * 2;
      stoneExclusions.push({
        x: Math.cos(theta) * BLUESTONE_RING_R,
        z: Math.sin(theta) * BLUESTONE_RING_R,
        r: 0.7,
      });
    }
    for (let i = 0; i < INNER_BLUE_COUNT; i++) {
      const tt = i / (INNER_BLUE_COUNT - 1);
      const angle = -Math.PI * 0.78 + tt * (Math.PI * 1.56);
      const r = 4.5 + Math.sin(tt * Math.PI) * 0.6;
      stoneExclusions.push({ x: Math.cos(angle) * r, z: Math.sin(angle) * r, r: 0.7 });
    }
    stoneExclusions.push({
      x: Math.cos(Math.PI * 0.25) * 30,
      z: Math.sin(Math.PI * 0.25) * 30,
      r: 1.8,
    });

    const insideStone = (x: number, z: number): boolean => {
      for (const ex of stoneExclusions) {
        const dx = x - ex.x;
        const dz = z - ex.z;
        if (dx * dx + dz * dz < ex.r * ex.r) return true;
      }
      return false;
    };

    const grassUniforms = {
      uTime:         { value: 0 },
      uSunDir:       skyUniforms.uSunDir,
      uSunColor:     { value: new THREE.Color(0xfff1d8) },
      uAmbient:      { value: new THREE.Color(0x506890) },
      uBaseColor:    { value: new THREE.Color(0x223818) },
      uTipColor:     { value: new THREE.Color(0x9bb95a) },
      uTipColor2:    { value: new THREE.Color(0xc2cf7a) },
      uWindStrength: { value: 0.30 },
    };

    const grassMaterial = new THREE.ShaderMaterial({
      uniforms: grassUniforms,
      side: THREE.DoubleSide,
      vertexShader: /* glsl */ `
        ${NOISE_GLSL}

        uniform float uTime;
        uniform float uWindStrength;

        attribute float aRandom;
        attribute float aBend;
        attribute float aTwist;

        varying vec2 vUv;
        varying float vGust;
        varying vec3 vNormalW;
        varying float vRandom;

        void main() {
          vUv = uv;
          vRandom = aRandom;

          vec4 ip = instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0);
          vec3 anchor = (modelMatrix * ip).xyz;

          // Large-scale gust band sweeping diagonally across the field.
          vec2 gustUV = anchor.xz * 0.05 + vec2(uTime * 0.18, uTime * 0.10);
          float gustRaw = vnoise(gustUV) * 0.5 + 0.5;
          float gust = pow(gustRaw, 1.6);
          vGust = gust;

          float flutter = vnoise(anchor.xz * 0.6 + vec2(uTime * 1.0, aRandom * 13.0));

          vec3 p = position;
          float t = clamp(uv.y, 0.0, 1.0);
          float curve = t * t;
          float windAmp = (gust * 1.0 + 0.2) * uWindStrength + flutter * 0.07;
          float bendAmt = aBend + windAmp;

          p.x += curve * bendAmt;
          p.y -= curve * bendAmt * 0.25;

          float c = cos(aTwist);
          float s = sin(aTwist);
          vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);

          vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);

          vec3 nLocal = normalize(vec3(-bendAmt * (1.0 - t) * 2.0, 0.5, 1.0));
          vec3 nRot   = vec3(c * nLocal.x - s * nLocal.z, nLocal.y, s * nLocal.x + c * nLocal.z);
          vNormalW    = normalize((modelMatrix * vec4(nRot, 0.0)).xyz);

          gl_Position = projectionMatrix * viewMatrix * worldPos;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uBaseColor;
        uniform vec3 uTipColor;
        uniform vec3 uTipColor2;
        uniform vec3 uSunDir;
        uniform vec3 uSunColor;
        uniform vec3 uAmbient;

        varying vec2 vUv;
        varying float vGust;
        varying vec3 vNormalW;
        varying float vRandom;

        void main() {
          float halfW = abs(vUv.x - 0.5);
          float taper = 1.0 - vUv.y * 0.85;
          if (halfW > taper * 0.5) discard;

          float ao = pow(vUv.y, 1.4);
          vec3 tipMix = mix(uTipColor, uTipColor2, vRandom);
          vec3 col = mix(uBaseColor, tipMix, ao);

          col += (vGust - 0.45) * 0.28 * vec3(0.9, 1.05, 0.7);

          float sunDot = max(dot(vNormalW, normalize(uSunDir)), 0.0);
          float backLit = pow(1.0 - sunDot, 2.0) * smoothstep(0.0, 0.3, uSunDir.y);
          col += backLit * vec3(0.30, 0.40, 0.20) * ao;

          float wrap = sunDot * 0.5 + 0.5;
          col = col * (uAmbient * 0.4 + uSunColor * wrap);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const bladeBase = new THREE.PlaneGeometry(0.07, 0.55, 1, 6);
    bladeBase.translate(0, 0.275, 0);

    const phoneGrass = window.matchMedia('(max-width: 900px), (pointer: coarse)').matches;
    const grassChunkSize  = 8;
    const grassChunkCount = phoneGrass ? 8 : 14;
    const grassDensity    = phoneGrass ? 110 : 240;
    const planeAngles     = [0, Math.PI / 3, (2 * Math.PI) / 3];
    const grassHalfCount  = grassChunkCount / 2;
    const grassMeshes: THREE.InstancedMesh[] = [];

    const dummyG = new THREE.Object3D();
    for (let cx = 0; cx < grassChunkCount; cx++) {
      for (let cz = 0; cz < grassChunkCount; cz++) {
        const baseX = (cx - grassHalfCount) * grassChunkSize;
        const baseZ = (cz - grassHalfCount) * grassChunkSize;

        for (const baseAngle of planeAngles) {
          const geo = bladeBase.clone();
          const aRandomArr = new Float32Array(grassDensity);
          const aBendArr   = new Float32Array(grassDensity);
          const aTwistArr  = new Float32Array(grassDensity);
          const inst = new THREE.InstancedMesh(geo, grassMaterial, grassDensity);
          inst.castShadow = false;
          inst.receiveShadow = false;
          inst.frustumCulled = false;

          for (let i = 0; i < grassDensity; i++) {
            let x = 0, z = 0, ok = false;
            for (let tries = 0; tries < 6; tries++) {
              const tx = baseX + Math.random() * grassChunkSize;
              const tz = baseZ + Math.random() * grassChunkSize;
              if (!insideStone(tx, tz)) { x = tx; z = tz; ok = true; break; }
            }
            const sH = ok ? 0.55 + Math.random() * 0.85 : 0;
            const sW = ok ? 0.85 + Math.random() * 0.5  : 0;
            const y = ok ? terrainHeight(x, z) : 0;
            dummyG.position.set(x, y, z);
            dummyG.rotation.set(0, 0, 0);
            dummyG.scale.set(sW, sH, 1);
            dummyG.updateMatrix();
            inst.setMatrixAt(i, dummyG.matrix);
            aRandomArr[i] = Math.random();
            aBendArr[i]   = (Math.random() - 0.5) * 0.10;
            aTwistArr[i]  = baseAngle + (Math.random() - 0.5) * 0.5;
          }
          inst.instanceMatrix.needsUpdate = true;
          geo.setAttribute('aRandom', new THREE.InstancedBufferAttribute(aRandomArr, 1));
          geo.setAttribute('aBend',   new THREE.InstancedBufferAttribute(aBendArr, 1));
          geo.setAttribute('aTwist',  new THREE.InstancedBufferAttribute(aTwistArr, 1));
          scene.add(inst);
          grassMeshes.push(inst);
        }
      }
    }

    // ─── Ravens (tiny additive points wheeling at altitude) ────────────────
    let ravenPoints: THREE.Points | null = null;
    if (ravens) {
      const count = 14;
      const positions = new Float32Array(count * 3);
      const seeds = new Float32Array(count);
      for (let i = 0; i < count; i++) {
        positions[i * 3] = 0;
        positions[i * 3 + 1] = 30 + Math.random() * 8;
        positions[i * 3 + 2] = 0;
        seeds[i] = Math.random();
      }
      const rGeo = new THREE.BufferGeometry();
      rGeo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      rGeo.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 1));
      const rMat = new THREE.ShaderMaterial({
        uniforms: {
          uTime: { value: 0 },
          uPixel: { value: renderer.getPixelRatio() },
          uIntensity: { value: 0.0 },
        },
        transparent: true,
        depthWrite: false,
        vertexShader: /* glsl */ `
          uniform float uTime;
          uniform float uPixel;
          attribute float aSeed;
          void main() {
            // Each raven traces an off-center ellipse around the monument
            // at a different radius and phase.
            float t = uTime * (0.18 + aSeed * 0.12) + aSeed * 6.28;
            float radius = 18.0 + aSeed * 18.0;
            vec3 p;
            p.x = cos(t) * radius;
            p.z = sin(t) * (radius * 0.85);
            p.y = position.y + sin(t * 1.4) * 1.2;
            vec4 mvp = modelViewMatrix * vec4(p, 1.0);
            gl_Position = projectionMatrix * mvp;
            gl_PointSize = 4.0 * uPixel * (60.0 / -mvp.z);
          }
        `,
        fragmentShader: /* glsl */ `
          uniform float uIntensity;
          void main() {
            // Tiny wing-shaped sprite via two offset gauss falloffs — looks
            // like a distant bird silhouette without needing geometry.
            vec2 c = gl_PointCoord - 0.5;
            float v = smoothstep(0.5, 0.1, length(c)) * uIntensity;
            if (v < 0.05) discard;
            gl_FragColor = vec4(vec3(0.05), v);
          }
        `,
      });
      ravenPoints = new THREE.Points(rGeo, rMat);
      ravenPoints.frustumCulled = false;
      scene.add(ravenPoints);
    }

    // ─── Bloom ─────────────────────────────────────────────────────────────
    const composer = new EffectComposer(renderer);
    composer.setSize(W0, H0);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    composer.addPass(new RenderPass(scene, camera));
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(W0, H0),
      /* strength */ 0.50,
      /* radius   */ 0.50,
      /* threshold*/ 0.93,
    );
    composer.addPass(bloomPass);
    composer.addPass(new OutputPass());

    // ─── Time-of-day driver ────────────────────────────────────────────────
    const horizonDay   = new THREE.Color(0.78, 0.85, 0.95);
    const horizonDusk  = new THREE.Color(1.00, 0.55, 0.32);
    const horizonNight = new THREE.Color(0.05, 0.06, 0.13);

    const updateTimeOfDay = (tod: number) => {
      // Sun rises in the NE so it aligns with the heel stone at sunrise —
      // matches the actual solstice geometry. Z component biases the sun's
      // azimuth toward the horseshoe opening.
      const angle = tod * Math.PI * 2;
      const dir = new THREE.Vector3(Math.cos(angle), Math.sin(angle), 0.6).normalize();
      skyUniforms.uSunDir.value.copy(dir);
      sun.position.copy(dir).multiplyScalar(120);
      sun.target.position.set(0, 0, 0);

      const dayW   = THREE.MathUtils.smoothstep(dir.y, -0.05, 0.30);
      const duskW  = Math.exp(-Math.pow(dir.y * 4.0, 2.0));
      const nightW = THREE.MathUtils.smoothstep(-dir.y, -0.05, 0.10);

      const sunWarm = new THREE.Color(0xff8a3c);
      const sunCool = new THREE.Color(0xfff1d8);
      sun.color.copy(sunWarm).lerp(sunCool, dayW);
      sun.intensity = 0.05 + 1.65 * dayW;

      ambient.intensity = 0.20 + 0.55 * dayW + 0.05 * nightW;
      ambient.color.setHSL(0.6, 0.5, 0.4 + 0.22 * dayW);
      ambient.groundColor.setHSL(0.10, 0.4, 0.07 + 0.10 * dayW);

      const fogCol = new THREE.Color(
        horizonDay.r * dayW + horizonDusk.r * duskW + horizonNight.r * nightW,
        horizonDay.g * dayW + horizonDusk.g * duskW + horizonNight.g * nightW,
        horizonDay.b * dayW + horizonDusk.b * duskW + horizonNight.b * nightW,
      );
      (scene.fog as THREE.FogExp2).color.copy(fogCol);
      (scene.fog as THREE.FogExp2).density = 0.0035 + 0.0025 * (1 - dayW);
      renderer.setClearColor(fogCol, 1);

      hillMaterial.uniforms.uDayW.value   = dayW;
      hillMaterial.uniforms.uDuskW.value  = duskW;
      hillMaterial.uniforms.uNightW.value = nightW;

      groundMaterial.uniforms.uSunColor.value.copy(sun.color).multiplyScalar(0.7 + dayW * 0.6);
      groundMaterial.uniforms.uAmbient.value.set(
        0.15 + 0.10 * dayW,
        0.20 + 0.18 * dayW,
        0.30 + 0.18 * dayW,
      );

      // Grass blades follow the same lighting as the ground.
      grassUniforms.uSunColor.value.copy(sun.color).multiplyScalar(0.7 + dayW * 0.6);
      grassUniforms.uAmbient.value.set(
        0.15 + 0.10 * dayW,
        0.20 + 0.18 * dayW,
        0.30 + 0.18 * dayW,
      );

      // Stones share lighting with the ground.
      for (const m of stoneMats) {
        m.uniforms.uSunColor.value.copy(sun.color).multiplyScalar(0.7 + dayW * 0.6);
        m.uniforms.uAmbient.value.set(
          0.15 + 0.12 * dayW,
          0.20 + 0.16 * dayW,
          0.30 + 0.14 * dayW,
        );
      }

      skyUniforms.uTimeOfDay.value = tod;

      if (ravenPoints) {
        const rmat = ravenPoints.material as THREE.ShaderMaterial;
        // Ravens visible from late afternoon to dusk (folkloric evening corvid feel).
        const f = THREE.MathUtils.smoothstep(0.5 - dir.y, 0.0, 0.5);
        rmat.uniforms.uIntensity.value = f * 0.85 + 0.15;
      }
    };

    // ─── Animation loop ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    let bobApplied = 0;

    const animate = () => {
      const elapsed = clock.getElapsedTime();
      const tod = dayLengthSeconds > 0
        ? (elapsed / dayLengthSeconds) % 1
        : fixedTimeOfDay;
      updateTimeOfDay(tod);

      grassUniforms.uTime.value = elapsed;

      if (ravenPoints) {
        const rmat = ravenPoints.material as THREE.ShaderMaterial;
        rmat.uniforms.uTime.value = elapsed;
      }

      camera.position.y -= bobApplied;
      controls.update();
      bobApplied = autoRotate
        ? Math.sin(elapsed * 0.42) * 0.50 + Math.sin(elapsed * 0.21 + 1.0) * 0.30
        : 0;
      camera.position.y += bobApplied;

      composer.render();
      raf = requestAnimationFrame(animate);
    };

    updateTimeOfDay(fixedTimeOfDay);
    animate();

    // ─── Resize observer ───────────────────────────────────────────────────
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
      groundGeo.dispose();
      groundMaterial.dispose();
      hillGeo.dispose();
      hillMaterial.dispose();
      sky.geometry.dispose();
      skyMaterial.dispose();
      bladeBase.dispose();
      grassMaterial.dispose();
      for (const m of grassMeshes) m.geometry.dispose();
      scene.traverse((obj) => {
        if (obj instanceof THREE.Mesh) {
          obj.geometry.dispose();
          if (obj.material instanceof THREE.Material) obj.material.dispose();
        }
      });
      if (ravenPoints) {
        ravenPoints.geometry.dispose();
        (ravenPoints.material as THREE.Material).dispose();
      }
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [width, height, dayLengthSeconds, fixedTimeOfDay, autoRotate, ravens]);

  const sx = (width !== undefined && height !== undefined)
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };
  return <Box ref={containerRef} sx={sx} />;
};

export default Stonehenge;
