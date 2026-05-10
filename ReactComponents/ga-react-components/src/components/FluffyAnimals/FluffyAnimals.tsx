/**
 * FluffyAnimals — bezier-blade fur on animal-shaped ellipsoid clusters.
 *
 * Reuses the fluffy-grass blade idiom (PlaneGeometry segmented into 6 strips,
 * vertex-shader bend curve, alpha-test discard for the silhouette) and
 * applies it to fur instead of grass. Each animal is built from a small
 * collection of ellipsoid "parts" (body, head, ears, tail). Fur instances
 * are sampled on the ellipsoid surfaces and oriented so each blade's local
 * +Y matches the surface normal — so the fur sticks out radially.
 *
 * Animals: Bear, Sheep, Fox, Bunny, Cat. Differ in body shape, fur length,
 * fur color, and which parts get fur (a fox's nose stays smooth, a sheep's
 * head stays smooth, etc.).
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { Box } from '@mui/material';

export interface FluffyAnimalsProps {
  width?: number;
  height?: number;
  /** Multiplier applied to every animal's fur density (0.3..2). */
  furDensity?: number;
  /** Multiplier on fur blade length. */
  furLength?: number;
  /** Wind sway strength on fur tips. */
  windStrength?: number;
  autoRotate?: boolean;
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
`;

// ─── Fur shader ─────────────────────────────────────────────────────────
// One material shared by every fur InstancedMesh; per-animal color tone
// flows through aBaseColor / aTipColor instance attributes.

const FUR_VERTEX = /* glsl */ `
  ${NOISE_GLSL}

  uniform float uTime;
  uniform float uWindStrength;

  attribute float aRandom;
  attribute float aTwist;
  attribute float aLength;     // 0..1 multiplier on blade length
  attribute vec3  aBaseColor;
  attribute vec3  aTipColor;

  varying vec2 vUv;
  varying vec3 vBaseColor;
  varying vec3 vTipColor;
  varying vec3 vNormalW;
  varying float vRandom;

  void main() {
    vUv = uv;
    vRandom = aRandom;
    vBaseColor = aBaseColor;
    vTipColor = aTipColor;

    // Base anchor in world space (instanceMatrix already places + orients
    // the blade so local +Y matches the surface normal).
    vec4 anchor4 = modelMatrix * instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0);
    vec3 anchor = anchor4.xyz;

    // Stretch the blade by aLength (per-instance variation).
    vec3 p = position;
    p.y *= aLength;

    // Per-blade twist around local Y (the surface normal axis).
    float c = cos(aTwist);
    float s = sin(aTwist);
    vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);

    // Local-frame curl: the blade arcs forward in local +X for natural
    // "lay" — like fur that's been lightly groomed.
    float t = clamp(uv.y, 0.0, 1.0);
    float curve = t * t;
    rotated.x += curve * 0.18 * (0.5 + aRandom * 0.5);
    rotated.y -= curve * 0.05;

    vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);

    // World-space wind sway on the tip — small breeze that varies
    // spatially via vnoise so neighbouring blades sway coherently.
    float gust = vnoise(anchor.xz * 0.6 + vec2(uTime * 0.4, uTime * 0.25));
    worldPos.x += (gust * 0.6 + 0.4) * uWindStrength * curve * 0.16;
    worldPos.z += sin(uTime * 0.7 + anchor.y * 1.4 + aRandom * 6.28) * uWindStrength * curve * 0.10;

    // Normal: blade plane normal rotated by twist + transformed to world.
    vec3 nLocal = normalize(vec3(s, 0.4, c));
    vNormalW = normalize((modelMatrix * instanceMatrix * vec4(nLocal, 0.0)).xyz);

    gl_Position = projectionMatrix * viewMatrix * worldPos;
  }
`;

const FUR_FRAGMENT = /* glsl */ `
  uniform vec3 uSunDir;
  uniform vec3 uSunColor;
  uniform vec3 uAmbient;

  varying vec2 vUv;
  varying vec3 vBaseColor;
  varying vec3 vTipColor;
  varying vec3 vNormalW;
  varying float vRandom;

  void main() {
    // Blade taper — silhouette via discard so depth-write stays correct.
    float halfW = abs(vUv.x - 0.5);
    float taper = 1.0 - vUv.y * 0.85;
    if (halfW > taper * 0.5) discard;

    // Color from base to tip with slight per-blade variation.
    float ao = pow(vUv.y, 1.3);
    vec3 col = mix(vBaseColor, vTipColor, ao + (vRandom - 0.5) * 0.10);

    // Lambert shading using a wrap term so backsides aren't black.
    vec3 N = normalize(vNormalW);
    vec3 L = normalize(uSunDir);
    float lit = max(dot(N, L), 0.0);
    float wrap = lit * 0.55 + 0.45;

    // Backlit translucency at low sun angles for that fluffy halo.
    float backLit = pow(1.0 - lit, 2.0) * smoothstep(0.0, 0.4, uSunDir.y);
    col += backLit * vTipColor * 0.30 * ao;

    col = col * (uAmbient * 0.55 + uSunColor * wrap);
    gl_FragColor = vec4(col, 1.0);
  }
`;

// ─── Helpers ────────────────────────────────────────────────────────────

const yAxis = new THREE.Vector3(0, 1, 0);

interface Part {
  /** Center in animal-local space. */
  c: [number, number, number];
  /** Radii along x/y/z. */
  r: [number, number, number];
  /** Visible body color. */
  body: number;
  /** Fur tip color. */
  tip?: number;
  /** Whether to grow fur on this part. */
  fur?: boolean;
  /** Fur length multiplier on this part. */
  furLen?: number;
  /** Fur density multiplier on this part. */
  furDen?: number;
}

interface AnimalDef {
  name: string;
  parts: Part[];
  /** Where this animal sits relative to the scene root. */
  pos: [number, number, number];
  /** Y rotation so the animal faces the camera. */
  yaw?: number;
}

const ANIMALS: AnimalDef[] = [
  // ─── Bear — big, brown, shaggy. ─────────────────────────────────────
  {
    name: 'bear',
    pos: [-9, 0, 0],
    yaw: 0.2,
    parts: [
      { c: [0, 1.05, 0], r: [1.05, 0.95, 1.40], body: 0x6b4423, tip: 0xa67849, fur: true, furLen: 1.1, furDen: 1.0 },
      { c: [0, 1.65, 1.10], r: [0.65, 0.65, 0.70], body: 0x6b4423, tip: 0xa67849, fur: true, furLen: 0.8 },
      { c: [-0.32, 2.20, 1.05], r: [0.18, 0.18, 0.18], body: 0x4a2f17, tip: 0x6b4423, fur: true, furLen: 0.4 },
      { c: [0.32, 2.20, 1.05], r: [0.18, 0.18, 0.18], body: 0x4a2f17, tip: 0x6b4423, fur: true, furLen: 0.4 },
      { c: [0, 1.50, 1.65], r: [0.28, 0.20, 0.20], body: 0x2a1810 }, // snout, no fur
      { c: [0, 1.55, 1.85], r: [0.08, 0.06, 0.06], body: 0x111111 }, // nose
      { c: [-0.20, 1.78, 1.55], r: [0.07, 0.07, 0.07], body: 0x111111 }, // eye L
      { c: [ 0.20, 1.78, 1.55], r: [0.07, 0.07, 0.07], body: 0x111111 }, // eye R
    ],
  },
  // ─── Sheep — round, woolly, dark face. ──────────────────────────────
  {
    name: 'sheep',
    pos: [-4, 0, 0],
    yaw: 0.0,
    parts: [
      { c: [0, 1.0, 0], r: [0.95, 0.95, 1.20], body: 0xf4ecdc, tip: 0xffffff, fur: true, furLen: 1.4, furDen: 1.5 },
      { c: [0, 1.30, 1.00], r: [0.40, 0.45, 0.50], body: 0x2a2018 }, // dark head
      { c: [-0.30, 1.65, 0.85], r: [0.15, 0.18, 0.10], body: 0x2a2018, tip: 0xf4ecdc, fur: true, furLen: 0.35 }, // ear L
      { c: [ 0.30, 1.65, 0.85], r: [0.15, 0.18, 0.10], body: 0x2a2018, tip: 0xf4ecdc, fur: true, furLen: 0.35 }, // ear R
      { c: [-0.16, 1.40, 1.45], r: [0.05, 0.05, 0.05], body: 0xffffff }, // eye
      { c: [ 0.16, 1.40, 1.45], r: [0.05, 0.05, 0.05], body: 0xffffff },
      { c: [ 0.0,  1.20, 1.50], r: [0.10, 0.08, 0.08], body: 0x111111 }, // nose
    ],
  },
  // ─── Fox — sleek body, long tail, pointed ears. ─────────────────────
  {
    name: 'fox',
    pos: [1, 0, 0],
    yaw: -0.1,
    parts: [
      { c: [0, 0.85, 0], r: [0.55, 0.55, 1.10], body: 0xc25c2c, tip: 0xea8054, fur: true, furLen: 0.55, furDen: 0.9 },
      { c: [0, 1.10, 0.95], r: [0.45, 0.40, 0.55], body: 0xc25c2c, tip: 0xf2a36a, fur: true, furLen: 0.40 },
      { c: [0, 1.05, 1.30], r: [0.18, 0.16, 0.20], body: 0xffffff, tip: 0xffffff, fur: true, furLen: 0.18 }, // white muzzle
      { c: [-0.27, 1.55, 0.90], r: [0.10, 0.30, 0.10], body: 0xc25c2c, tip: 0x222222, fur: true, furLen: 0.20 }, // ear L
      { c: [ 0.27, 1.55, 0.90], r: [0.10, 0.30, 0.10], body: 0xc25c2c, tip: 0x222222, fur: true, furLen: 0.20 }, // ear R
      { c: [0, 0.95, -1.20], r: [0.30, 0.30, 0.85], body: 0xc25c2c, tip: 0xffffff, fur: true, furLen: 0.95, furDen: 1.4 }, // tail
      { c: [-0.18, 1.20, 1.30], r: [0.05, 0.05, 0.05], body: 0x111111 },
      { c: [ 0.18, 1.20, 1.30], r: [0.05, 0.05, 0.05], body: 0x111111 },
      { c: [0, 1.05, 1.50], r: [0.05, 0.04, 0.05], body: 0x111111 }, // nose
    ],
  },
  // ─── Bunny — round body, long upright ears. ─────────────────────────
  {
    name: 'bunny',
    pos: [6, 0, 0],
    yaw: 0.1,
    parts: [
      { c: [0, 0.70, 0], r: [0.55, 0.60, 0.80], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.65, furDen: 1.2 },
      { c: [0, 1.10, 0.55], r: [0.40, 0.40, 0.45], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.50 },
      { c: [-0.20, 1.95, 0.40], r: [0.10, 0.55, 0.10], body: 0xf2efe8, tip: 0xfdd6cf, fur: true, furLen: 0.20 }, // ear L
      { c: [ 0.20, 1.95, 0.40], r: [0.10, 0.55, 0.10], body: 0xf2efe8, tip: 0xfdd6cf, fur: true, furLen: 0.20 }, // ear R
      { c: [0, 0.65, -0.85], r: [0.22, 0.22, 0.22], body: 0xffffff, tip: 0xffffff, fur: true, furLen: 0.70, furDen: 2.0 }, // pom tail
      { c: [-0.14, 1.10, 0.95], r: [0.05, 0.05, 0.05], body: 0x111111 },
      { c: [ 0.14, 1.10, 0.95], r: [0.05, 0.05, 0.05], body: 0x111111 },
      { c: [0, 0.95, 1.00], r: [0.05, 0.04, 0.05], body: 0xff8a8a }, // pink nose
    ],
  },
  // ─── Cat — sleek body, triangle ears, long tail. ────────────────────
  {
    name: 'cat',
    pos: [11, 0, 0],
    yaw: -0.2,
    parts: [
      { c: [0, 0.80, 0], r: [0.45, 0.45, 0.95], body: 0x4a4036, tip: 0x77685a, fur: true, furLen: 0.40, furDen: 1.0 },
      { c: [0, 1.10, 0.85], r: [0.40, 0.38, 0.45], body: 0x4a4036, tip: 0x77685a, fur: true, furLen: 0.30 },
      { c: [-0.22, 1.55, 0.75], r: [0.15, 0.20, 0.05], body: 0x4a4036, tip: 0xfdd6cf, fur: true, furLen: 0.15 }, // ear L
      { c: [ 0.22, 1.55, 0.75], r: [0.15, 0.20, 0.05], body: 0x4a4036, tip: 0xfdd6cf, fur: true, furLen: 0.15 }, // ear R
      { c: [0.7, 0.85, -1.20], r: [0.13, 0.13, 0.95], body: 0x4a4036, tip: 0x77685a, fur: true, furLen: 0.30, furDen: 0.8 }, // tail
      { c: [-0.16, 1.20, 1.15], r: [0.06, 0.06, 0.06], body: 0xb6db5e }, // green eye L
      { c: [ 0.16, 1.20, 1.15], r: [0.06, 0.06, 0.06], body: 0xb6db5e }, // green eye R
      { c: [0, 1.05, 1.25], r: [0.05, 0.04, 0.05], body: 0xff7a7a }, // pink nose
    ],
  },
];

const FluffyAnimals: React.FC<FluffyAnimalsProps> = ({
  width,
  height,
  furDensity = 1,
  furLength = 1,
  windStrength = 0.5,
  autoRotate = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = width ?? container.clientWidth ?? 1280;
    const H0 = height ?? container.clientHeight ?? 720;

    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0xc8d8e8, 0.008);

    const camera = new THREE.PerspectiveCamera(45, W0 / H0, 0.1, 200);
    camera.position.set(0, 4, 14);
    camera.lookAt(0, 1.2, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W0, H0);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.minDistance = 5;
    controls.maxDistance = 40;
    controls.maxPolarAngle = Math.PI * 0.49;
    controls.target.set(0, 1.2, 0);
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 0.4;

    // ─── Sky + lighting ──────────────────────────────────────────────────
    const skyMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: { uSunDir: { value: new THREE.Vector3(0.4, 0.7, 0.6).normalize() } },
      vertexShader: `varying vec3 vDir; void main() { vec4 wp = modelMatrix*vec4(position,1); vDir = normalize(wp.xyz); gl_Position = projectionMatrix*viewMatrix*wp; }`,
      fragmentShader: `uniform vec3 uSunDir; varying vec3 vDir;
        void main() {
          vec3 d = normalize(vDir);
          float h = clamp(d.y*0.5+0.5, 0.0, 1.0);
          vec3 col = mix(vec3(0.95, 0.85, 0.74), vec3(0.45, 0.66, 0.92), pow(h, 0.7));
          float sd = max(dot(d, normalize(uSunDir)), 0.0);
          col += vec3(1.0, 0.95, 0.78) * (pow(sd, 32.0) * 1.2 + pow(sd, 4.0) * 0.10);
          gl_FragColor = vec4(col, 1.0);
        }`,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(120, 32, 16), skyMat);
    scene.add(sky);

    const ambient = new THREE.HemisphereLight(0xc6dbff, 0x4a3920, 0.7);
    scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xfff1d8, 1.6);
    sun.position.set(8, 14, 10);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.left = -16;
    sun.shadow.camera.right = 16;
    sun.shadow.camera.top = 16;
    sun.shadow.camera.bottom = -16;
    sun.shadow.camera.near = 1;
    sun.shadow.camera.far = 50;
    sun.shadow.bias = -0.0006;
    scene.add(sun);

    const sunDir = new THREE.Vector3().subVectors(sun.position, sun.target.position).normalize();

    // ─── Ground (simple painterly turf) ──────────────────────────────────
    const groundGeo = new THREE.PlaneGeometry(80, 80, 64, 64);
    groundGeo.rotateX(-Math.PI / 2);
    const groundMat = new THREE.ShaderMaterial({
      uniforms: {
        uSunDir: { value: sunDir.clone() },
        uSunColor: { value: new THREE.Color(0xfff1d8) },
        uAmbient: { value: new THREE.Color(0x506890) },
      },
      vertexShader: `
        varying vec3 vWorld;
        varying vec3 vNormal;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorld = wp.xyz;
          vNormal = normalize(normalMatrix * normal);
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: `
        uniform vec3 uSunDir, uSunColor, uAmbient;
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}
        void main() {
          float n = vnoise(vWorld.xz * 0.5);
          vec3 grass = mix(vec3(0.36, 0.50, 0.24), vec3(0.20, 0.34, 0.16), n * 0.5 + 0.5);
          // Soft circular vignette so the row of animals reads as the
          // subject and the periphery falls into haze.
          float r = length(vWorld.xz);
          grass = mix(grass, vec3(0.36, 0.45, 0.34), smoothstep(15.0, 35.0, r));
          float lit = max(dot(vNormal, normalize(uSunDir)), 0.0);
          grass = grass * (uAmbient * 0.4 + uSunColor * (lit * 0.7 + 0.4));
          gl_FragColor = vec4(grass, 1.0);
        }
      `,
    });
    const ground = new THREE.Mesh(groundGeo, groundMat);
    ground.receiveShadow = true;
    scene.add(ground);

    // ─── Build animals ───────────────────────────────────────────────────
    const phoneish = window.matchMedia('(max-width: 900px), (pointer: coarse)').matches;
    const densityScale = (phoneish ? 0.45 : 1.0) * furDensity;
    const lengthScale  = furLength;

    const furUniforms = {
      uTime: { value: 0 },
      uWindStrength: { value: windStrength },
      uSunDir: { value: sunDir.clone() },
      uSunColor: { value: new THREE.Color(0xfff1d8) },
      uAmbient: { value: new THREE.Color(0x4f6a8c) },
    };

    const furMaterial = new THREE.ShaderMaterial({
      uniforms: furUniforms,
      vertexShader: FUR_VERTEX,
      fragmentShader: FUR_FRAGMENT,
      side: THREE.DoubleSide,
      transparent: false,
      depthWrite: true,
    });

    const furMeshes: THREE.InstancedMesh[] = [];
    const furGeoms: THREE.BufferGeometry[] = [];
    const bodyMeshes: THREE.Mesh[] = [];
    const bodyMats: THREE.Material[] = [];

    const dummy = new THREE.Object3D();

    for (const animal of ANIMALS) {
      const group = new THREE.Group();
      group.position.set(...animal.pos);
      if (animal.yaw) group.rotation.y = animal.yaw;
      scene.add(group);

      // Total fur instance count for this animal — sum across furred parts.
      const furParts = animal.parts.filter((p) => p.fur);
      const partInstanceCounts: number[] = furParts.map((p) => {
        // Surface area ≈ 4π * mean(r1*r2). Density scaled by part's density.
        const [rx, ry, rz] = p.r;
        const area = 4 * Math.PI * Math.max(rx * ry + ry * rz + rx * rz, 0.05) / 3;
        const baseN = Math.round(area * 2200 * (p.furDen ?? 1.0) * densityScale);
        return Math.max(20, baseN);
      });
      const totalCount = partInstanceCounts.reduce((s, n) => s + n, 0);

      // One InstancedMesh per animal (cheaper than per-part). A short
      // base blade geometry — 0.35m at full length — is the canvas;
      // per-instance aLength scales it.
      const blade = new THREE.PlaneGeometry(0.05, 0.35, 1, 6);
      blade.translate(0, 0.175, 0);

      const aRandomArr = new Float32Array(totalCount);
      const aTwistArr  = new Float32Array(totalCount);
      const aLengthArr = new Float32Array(totalCount);
      const aBaseArr   = new Float32Array(totalCount * 3);
      const aTipArr    = new Float32Array(totalCount * 3);

      const inst = new THREE.InstancedMesh(blade, furMaterial, totalCount);
      inst.castShadow = false;
      inst.receiveShadow = false;
      inst.frustumCulled = false;

      const baseColor = new THREE.Color();
      const tipColor  = new THREE.Color();

      let idx = 0;
      for (let pi = 0; pi < furParts.length; pi++) {
        const part = furParts[pi];
        const n = partInstanceCounts[pi];
        baseColor.set(part.body);
        tipColor.set(part.tip ?? part.body);
        const partLen = (part.furLen ?? 1.0) * lengthScale;
        const [cx, cy, cz] = part.c;
        const [rx, ry, rz] = part.r;

        for (let i = 0; i < n; i++) {
          // Uniform random direction on unit sphere → ellipsoid surface.
          // Bias the distribution slightly toward upper-hemisphere normals
          // because that's what the camera sees most.
          const u = Math.random();
          const v = Math.random();
          const phi   = Math.acos(2 * u - 1);
          const theta = 2 * Math.PI * v;
          const sinPhi = Math.sin(phi);
          const dx = sinPhi * Math.cos(theta);
          const dy = Math.cos(phi);
          const dz = sinPhi * Math.sin(theta);

          // Position on ellipsoid surface.
          const px = cx + dx * rx;
          const py = cy + dy * ry;
          const pz = cz + dz * rz;

          // Normal at that point: gradient of ellipsoid equation =
          // (x/rx², y/ry², z/rz²), normalized.
          const nxRaw = dx / rx;
          const nyRaw = dy / ry;
          const nzRaw = dz / rz;
          const nLen = Math.sqrt(nxRaw * nxRaw + nyRaw * nyRaw + nzRaw * nzRaw);
          const nx = nxRaw / nLen;
          const ny = nyRaw / nLen;
          const nz = nzRaw / nLen;

          dummy.position.set(px, py, pz);
          // Rotate blade so its local +Y aligns with the surface normal.
          dummy.quaternion.setFromUnitVectors(yAxis, new THREE.Vector3(nx, ny, nz));
          // Random scale variation on width, length per-instance via aLength.
          dummy.scale.set(0.85 + Math.random() * 0.4, 1, 1);
          dummy.updateMatrix();
          inst.setMatrixAt(idx, dummy.matrix);

          aRandomArr[idx] = Math.random();
          aTwistArr[idx]  = Math.random() * Math.PI * 2;
          aLengthArr[idx] = (0.7 + Math.random() * 0.6) * partLen;
          aBaseArr[idx * 3 + 0] = baseColor.r;
          aBaseArr[idx * 3 + 1] = baseColor.g;
          aBaseArr[idx * 3 + 2] = baseColor.b;
          aTipArr[idx * 3 + 0] = tipColor.r;
          aTipArr[idx * 3 + 1] = tipColor.g;
          aTipArr[idx * 3 + 2] = tipColor.b;
          idx++;
        }
      }
      inst.instanceMatrix.needsUpdate = true;
      blade.setAttribute('aRandom', new THREE.InstancedBufferAttribute(aRandomArr, 1));
      blade.setAttribute('aTwist',  new THREE.InstancedBufferAttribute(aTwistArr, 1));
      blade.setAttribute('aLength', new THREE.InstancedBufferAttribute(aLengthArr, 1));
      blade.setAttribute('aBaseColor', new THREE.InstancedBufferAttribute(aBaseArr, 3));
      blade.setAttribute('aTipColor',  new THREE.InstancedBufferAttribute(aTipArr, 3));

      group.add(inst);
      furMeshes.push(inst);
      furGeoms.push(blade);

      // Body parts (visible underneath the fur — gives the animal weight).
      for (const part of animal.parts) {
        const geom = new THREE.SphereGeometry(1, 24, 16);
        const mat = new THREE.MeshStandardMaterial({
          color: part.body,
          roughness: 0.9,
          metalness: 0.0,
        });
        const m = new THREE.Mesh(geom, mat);
        m.position.set(...part.c);
        m.scale.set(...part.r);
        m.castShadow = true;
        m.receiveShadow = true;
        group.add(m);
        bodyMeshes.push(m);
        bodyMats.push(mat);
      }
    }

    // ─── Bloom post-pass ─────────────────────────────────────────────────
    const composer = new EffectComposer(renderer);
    composer.setSize(W0, H0);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    composer.addPass(new RenderPass(scene, camera));
    const bloomPass = new UnrealBloomPass(new THREE.Vector2(W0, H0), 0.30, 0.45, 0.92);
    composer.addPass(bloomPass);
    composer.addPass(new OutputPass());

    // ─── Animate ─────────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    const animate = () => {
      const elapsed = clock.getElapsedTime();
      furUniforms.uTime.value = elapsed;
      furUniforms.uWindStrength.value = windStrength;
      controls.autoRotate = autoRotate;
      controls.update();
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
      furMaterial.dispose();
      furGeoms.forEach((g) => g.dispose());
      furMeshes.forEach((m) => { /* geom already in furGeoms */ void m; });
      bodyMeshes.forEach((m) => m.geometry.dispose());
      bodyMats.forEach((m) => m.dispose());
      groundGeo.dispose();
      groundMat.dispose();
      sky.geometry.dispose();
      skyMat.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [width, height, furDensity, furLength, windStrength, autoRotate]);

  const sx = (width !== undefined && height !== undefined)
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };
  return <Box ref={containerRef} sx={sx} />;
};

export default FluffyAnimals;
