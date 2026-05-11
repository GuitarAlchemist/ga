/**
 * Fluffy Grass — cinematic re-implementation (2026-05-08).
 *
 * Replaces the original crossed-plane billboard approach with:
 *  - Curved segmented blades (bezier bend, per-instance tilt/twist).
 *  - Real value-noise terrain — visible rolling hills, grass conforms to surface.
 *  - Looping day/night cycle (sky gradient + sun disc + fog/light sweep).
 *  - Visible wind-gust bands sweeping across the field.
 *  - Fireflies that fade in at dusk and trace lazy paths through the meadow.
 *  - Wildflower billboards for color variety.
 *  - OrbitControls so the user can actually look around.
 *
 * Inspired by tympanus.net/codrops/2025/02/04/how-to-make-the-fluffiest-grass-with-three-js
 * but most of the visual character comes from the bend curve + gust shader +
 * sky/sun cycle, none of which were in the original.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { Box } from '@mui/material';

export interface FluffyGrassProps {
  width?: number;
  height?: number;
  /** Grass blades per chunk. Default 800. */
  grassDensity?: number;
  /** Edge length of each chunk in world units. */
  chunkSize?: number;
  /** Number of chunks per side (chunkCount * chunkCount total). */
  chunkCount?: number;
  /** Maximum blade height in world units. */
  grassHeight?: number;
  /** Maximum blade width in world units. */
  grassWidth?: number;
  baseColor?: THREE.Color;
  tipColor?: THREE.Color;
  /** Wind speed multiplier. */
  windSpeed?: number;
  /** Wind bend strength. */
  windStrength?: number;
  /** Day-cycle loop length in seconds (one full sunrise→noon→sunset→night). 0 freezes time. */
  dayLengthSeconds?: number;
  /** Fixed time-of-day in [0,1] when dayLengthSeconds is 0. 0 = sunrise, 0.25 = noon, 0.5 = sunset, 0.75 = midnight. */
  fixedTimeOfDay?: number;
  /** Auto-rotate the camera around the field. */
  autoRotate?: boolean;
  /** Whether to render fireflies at dusk/night. */
  fireflies?: boolean;
  /** Whether to scatter wildflowers across the meadow. */
  flowers?: boolean;
}

// ─── Value noise (CPU side, used for terrain placement) ──────────────────────
// Same signature/output as the GLSL noise in the terrain shader so blade
// placement matches the visible hill surface exactly.
const hash2 = (x: number, y: number): [number, number] => {
  const px = x * 127.1 + y * 311.7;
  const py = x * 269.5 + y * 183.3;
  const sx = Math.sin(px) * 43758.5453123;
  const sy = Math.sin(py) * 43758.5453123;
  return [-1 + 2 * (sx - Math.floor(sx)), -1 + 2 * (sy - Math.floor(sy))];
};

const valueNoise = (x: number, y: number): number => {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  const fx = x - ix;
  const fy = y - iy;
  const ux = fx * fx * (3 - 2 * fx);
  const uy = fy * fy * (3 - 2 * fy);

  const [a0, a1] = hash2(ix, iy);
  const [b0, b1] = hash2(ix + 1, iy);
  const [c0, c1] = hash2(ix, iy + 1);
  const [d0, d1] = hash2(ix + 1, iy + 1);

  const da = a0 * fx + a1 * fy;
  const db = b0 * (fx - 1) + b1 * fy;
  const dc = c0 * fx + c1 * (fy - 1);
  const dd = d0 * (fx - 1) + d1 * (fy - 1);

  return (
    da * (1 - ux) * (1 - uy) +
    db * ux * (1 - uy) +
    dc * (1 - ux) * uy +
    dd * ux * uy
  );
};

// Multi-octave terrain height (raw noise, roughly [-3, 3]).
const baseTerrain = (x: number, z: number): number => {
  let h = 0;
  let amp = 1.6;
  let freq = 0.025;
  for (let i = 0; i < 4; i++) {
    h += valueNoise(x * freq, z * freq) * amp;
    amp *= 0.5;
    freq *= 2;
  }
  return h;
};

// Round-island heightmap: a soft dome rises in the middle, dives under water
// at the rim, with the noise riding on top so the surface still looks
// organic. islandRadius / waterLevel are picked so a square heightmap of
// edge `terrainSize` produces a circular island that fills it comfortably.
const buildTerrainHeight =
  (terrainSize: number) =>
  (x: number, z: number): number => {
    const r = Math.sqrt(x * x + z * z);
    const islandR = terrainSize * 0.42;     // ~33 with size=80
    const beachR  = terrainSize * 0.50;     // ~40 — past this is open water
    // Dome: +rise inside, drops to -waterDepth past beachR.
    const t = THREE.MathUtils.smoothstep(r, islandR * 0.55, beachR);
    const dome = THREE.MathUtils.lerp(3.5, -3.0, t);
    return baseTerrain(x, z) * (1.0 - t * 0.6) + dome;
  };

// GLSL implementations of the same noise + terrain functions, embedded in shaders
// so the GPU sees identical output. Used by both terrain + grass + fireflies.
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
  float terrain(vec2 p) {
    float h = 0.0;
    float amp = 1.6;
    float freq = 0.025;
    for (int i = 0; i < 4; i++) {
      h += vnoise(p * freq) * amp;
      amp *= 0.5;
      freq *= 2.0;
    }
    return h;
  }
`;

export const FluffyGrass: React.FC<FluffyGrassProps> = ({
  width,
  height,
  grassDensity = 800,
  chunkSize = 10,
  chunkCount = 8,
  grassHeight = 1.0,
  grassWidth = 0.1,
  baseColor = new THREE.Color(0x16321f),
  tipColor = new THREE.Color(0x8fc97a),
  windSpeed = 1.0,
  windStrength = 0.5,
  dayLengthSeconds = 90,
  fixedTimeOfDay = 0.18,
  autoRotate = true,
  fireflies = true,
  flowers = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    // Auto-size to container when width/height props aren't given. Lets the
    // demo wrap us in a flex/grid cell that resizes responsively without us
    // re-mounting on every layout tick.
    const W0 = width ?? container.clientWidth ?? 1280;
    const H0 = height ?? container.clientHeight ?? 720;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();

    const camera = new THREE.PerspectiveCamera(55, W0 / H0, 0.1, 1000);
    camera.position.set(34, 18, 34);
    camera.lookAt(0, 1, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W0, H0);
    // Cap pixel ratio to 1.5 — the visual cost of higher DPR (common on
    // phones at 3.0) doesn't pay off in this scene. The bloom + AA already
    // hide aliasing, and 3× DPR triples fragment-shader cost on the GPUs
    // that need help most.
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    // OrbitControls — user can pan/zoom/look; auto-rotate optional.
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.minDistance = 6;
    controls.maxDistance = 120;
    controls.maxPolarAngle = Math.PI * 0.49; // don't dip below ground
    controls.target.set(0, 1, 0);
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 0.35;

    // ─── Sky dome ──────────────────────────────────────────────────────────
    // Inverted-sphere skydome with a procedural gradient + sun disc + soft
    // horizon glow. All of it driven by a single uTime-of-day uniform we
    // also share with grass/fireflies/lights so the world stays coherent.
    const skyUniforms = {
      uSunDir: { value: new THREE.Vector3(0, 1, 0) },
      uTimeOfDay: { value: fixedTimeOfDay }, // [0,1)
    };

    const skyMaterial = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: skyUniforms,
      vertexShader: /* glsl */ `
        varying vec3 vWorldDir;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorldDir = normalize(wp.xyz);
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uSunDir;
        uniform float uTimeOfDay;
        varying vec3 vWorldDir;

        // Mix three sky palettes: night, dawn/dusk, day.
        vec3 skyDay(float h)   { return mix(vec3(0.55,0.78,0.95), vec3(0.16,0.42,0.78), pow(clamp(h,0.0,1.0), 0.7)); }
        vec3 skyDusk(float h)  { return mix(vec3(0.96,0.55,0.30), vec3(0.20,0.10,0.32), pow(clamp(h,0.0,1.0), 0.6)); }
        vec3 skyNight(float h) { return mix(vec3(0.04,0.05,0.13), vec3(0.005,0.01,0.05), pow(clamp(h,0.0,1.0), 0.7)); }

        void main() {
          vec3 dir = normalize(vWorldDir);
          float h = clamp(dir.y, 0.0, 1.0);

          // Two-axis weight: how "day" (sun high) and how "twilight" (sun near horizon).
          float dayW   = smoothstep(-0.05, 0.30, uSunDir.y);
          float duskW  = exp(-pow((uSunDir.y) * 4.0, 2.0));     // peaks at horizon
          float nightW = smoothstep(0.05, -0.10, uSunDir.y);

          vec3 col = skyDay(h)   * dayW
                   + skyDusk(h)  * duskW
                   + skyNight(h) * nightW;

          // Sun disc — soft, with bigger glow at dusk.
          float sunDot = clamp(dot(dir, normalize(uSunDir)), 0.0, 1.0);
          float discC  = smoothstep(0.9985, 0.9999, sunDot);
          float glow   = pow(sunDot, 24.0) * 0.35 + pow(sunDot, 4.0) * 0.10;
          vec3 sunCol  = mix(vec3(1.0, 0.55, 0.25), vec3(1.0, 0.95, 0.85), dayW);
          col += sunCol * discC * 6.0;
          col += sunCol * glow * (0.6 + duskW * 1.6);

          // Stars — only at night, sparse value-noise threshold.
          float starN = fract(sin(dot(dir.xz * 600.0, vec2(12.989, 78.233))) * 43758.55);
          float star = smoothstep(0.9965, 0.9990, starN) * nightW * smoothstep(0.05, 0.4, h);
          col += vec3(star);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const sky = new THREE.Mesh(new THREE.SphereGeometry(400, 32, 16), skyMaterial);
    scene.add(sky);

    scene.fog = new THREE.FogExp2(0x88aacc, 0.012);

    // ─── Lights ────────────────────────────────────────────────────────────
    const ambient = new THREE.HemisphereLight(0xa7c4ff, 0x2a1f10, 0.6);
    scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xfff1d8, 1.4);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.left = -40;
    sun.shadow.camera.right = 40;
    sun.shadow.camera.top = 40;
    sun.shadow.camera.bottom = -40;
    sun.shadow.camera.near = 0.5;
    sun.shadow.camera.far = 180;
    sun.shadow.bias = -0.0006;
    sun.shadow.radius = 3;
    scene.add(sun);
    scene.add(sun.target);

    // ─── Terrain ───────────────────────────────────────────────────────────
    // Round island heightmap: a square plane displaced into a circular dome.
    // The same height function is used for grass/flower/firefly placement so
    // every prop sits exactly on the visible surface.
    const terrainSize = chunkSize * chunkCount;
    const terrainHeight = buildTerrainHeight(terrainSize);
    const islandRadius  = terrainSize * 0.46; // grass placement clamp

    const terrainGeo = new THREE.PlaneGeometry(terrainSize, terrainSize, 192, 192);
    terrainGeo.rotateX(-Math.PI / 2);

    // Pre-compute terrain height per vertex on CPU so we get correct normals.
    {
      const pos = terrainGeo.attributes.position as THREE.BufferAttribute;
      for (let i = 0; i < pos.count; i++) {
        const x = pos.getX(i);
        const z = pos.getZ(i);
        pos.setY(i, terrainHeight(x, z));
      }
      pos.needsUpdate = true;
      terrainGeo.computeVertexNormals();
    }

    const terrainMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uBase: { value: new THREE.Color(0x223a18) },
        uDirt: { value: new THREE.Color(0x4a3a22) },
        uSand: { value: new THREE.Color(0xd9c79a) },
        uUnderwater: { value: new THREE.Color(0x1a2c3a) },
        uSunDir: skyUniforms.uSunDir,
        uSunColor: { value: new THREE.Color(0xfff1d8) },
        uAmbient: { value: new THREE.Color(0x1f2a3a) },
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
        uniform vec3 uBase;
        uniform vec3 uDirt;
        uniform vec3 uSand;
        uniform vec3 uUnderwater;
        uniform vec3 uSunDir;
        uniform vec3 uSunColor;
        uniform vec3 uAmbient;
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}

        void main() {
          // Patchy color: dirt where slope is steep, mossy on flats, plus
          // low-frequency variation so it doesn't look uniform.
          float slope = 1.0 - clamp(vNormal.y, 0.0, 1.0);
          float patchy = vnoise(vWorld.xz * 0.08) * 0.5 + 0.5;
          vec3 grass = mix(uBase, uDirt, smoothstep(0.25, 0.6, slope) + patchy * 0.15);
          grass *= 0.85 + patchy * 0.3;

          // Beach band: vertices near y≈0 fade to sand. Below water = darker.
          float h = vWorld.y;
          float sandW  = smoothstep(1.5, 0.05, h) * smoothstep(-0.6, 0.0, h);
          float underW = smoothstep(0.0, -0.6, h);
          vec3 col = mix(grass, uSand, sandW);
          col = mix(col, uUnderwater, underW);

          // Lambert with sky ambient.
          float d = max(dot(vNormal, normalize(uSunDir)), 0.0);
          col = col * (uAmbient + uSunColor * d);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const terrain = new THREE.Mesh(terrainGeo, terrainMaterial);
    terrain.receiveShadow = true;
    scene.add(terrain);

    // ─── Water ─────────────────────────────────────────────────────────────
    // Tessellated plane (256x256) so the vertex shader can actually displace
    // the surface. Four-component Gerstner sum gives directional swell + chop;
    // analytic normals from the wave gradient drive Schlick Fresnel, sharper
    // sun specular, and a horizon-tinted base color that tracks time-of-day.
    // A foam term lights up where waves crest and along the shoreline.
    const waterGeo = new THREE.PlaneGeometry(800, 800, 256, 256);
    waterGeo.rotateX(-Math.PI / 2);
    const waterMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uTime: { value: 0 },
        uSunDir: skyUniforms.uSunDir,
        uHorizonDay:   { value: new THREE.Color(0.55, 0.78, 0.95) },
        uHorizonDusk:  { value: new THREE.Color(0.96, 0.55, 0.30) },
        uHorizonNight: { value: new THREE.Color(0.04, 0.05, 0.13) },
        uDeepColor:    { value: new THREE.Color(0.02, 0.10, 0.18) },
        uShallowTint:  { value: new THREE.Color(0.20, 0.55, 0.65) },
        uDayW:   { value: 0.0 },
        uDuskW:  { value: 0.0 },
        uNightW: { value: 0.0 },
        uShoreR: { value: terrainSize * 0.50 + 1.0 },
      },
      transparent: true,
      vertexShader: /* glsl */ `
        uniform float uTime;
        varying vec3 vWorld;
        varying vec3 vNormal;
        varying float vCrest;

        // Single Gerstner wave: returns horizontal displacement xz and vertical y.
        // dx/dz, dy contribute analytic tangents we sum into a normal.
        vec3 gerstner(vec2 pos, vec2 dir, float steepness, float wavelength, float speed, float t,
                      inout vec3 tangent, inout vec3 binormal) {
          float k  = 6.28318530718 / wavelength;
          float c  = sqrt(9.81 / k);                  // phase speed (g·k)
          vec2  d  = normalize(dir);
          float f  = k * (dot(d, pos) - c * speed * t);
          float a  = steepness / k;
          // Displacement: x,z are pulled toward crest, y rises in cosine.
          vec3 disp = vec3(
            d.x * (a * cos(f)),
            a * sin(f),
            d.y * (a * cos(f))
          );
          // Accumulate partial derivatives onto running tangent/binormal.
          tangent  += vec3(
            -d.x * d.x * (steepness * sin(f)),
            d.x * (steepness * cos(f)),
            -d.x * d.y * (steepness * sin(f))
          );
          binormal += vec3(
            -d.x * d.y * (steepness * sin(f)),
            d.y * (steepness * cos(f)),
            -d.y * d.y * (steepness * sin(f))
          );
          return disp;
        }

        void main() {
          vec3 p = position;
          vec3 tan = vec3(1.0, 0.0, 0.0);
          vec3 bin = vec3(0.0, 0.0, 1.0);

          // Four overlapping wave trains, mixed scales/directions so the
          // surface has long swell + short chop without obvious tiling.
          vec3 d0 = gerstner(p.xz, vec2( 1.0,  0.6), 0.20, 24.0, 0.8, uTime, tan, bin);
          vec3 d1 = gerstner(p.xz, vec2(-0.7,  1.0), 0.18, 18.0, 0.9, uTime, tan, bin);
          vec3 d2 = gerstner(p.xz, vec2( 0.3, -1.0), 0.14, 11.0, 1.1, uTime, tan, bin);
          vec3 d3 = gerstner(p.xz, vec2(-1.0, -0.3), 0.10,  6.5, 1.4, uTime, tan, bin);
          vec3 disp = d0 + d1 + d2 + d3;

          // Crest term = how steep the wave is here. Used by fragment for foam.
          vCrest = clamp(disp.y * 0.7 + 0.1, 0.0, 1.0);

          vec3 displaced = p + disp;
          vec3 nrm = normalize(cross(bin, tan));
          // Sometimes the cross product flips relative to world-up; reseat it.
          if (nrm.y < 0.0) nrm = -nrm;

          vec4 wp = modelMatrix * vec4(displaced, 1.0);
          vWorld  = wp.xyz;
          vNormal = (modelMatrix * vec4(nrm, 0.0)).xyz;
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform float uTime;
        uniform vec3  uSunDir;
        uniform vec3  uHorizonDay, uHorizonDusk, uHorizonNight;
        uniform vec3  uDeepColor, uShallowTint;
        uniform float uDayW, uDuskW, uNightW;
        uniform float uShoreR;
        varying vec3  vWorld;
        varying vec3  vNormal;
        varying float vCrest;
        ${NOISE_GLSL}

        // Schlick Fresnel for water (n1=1.0, n2=1.33 → F0≈0.02).
        float fresnelSchlick(float cosTheta) {
          float F0 = 0.02;
          return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
        }

        void main() {
          vec3 horiz = uHorizonDay  * uDayW
                     + uHorizonDusk * uDuskW
                     + uHorizonNight* uNightW;

          // Per-pixel detail: ride two value-noise octaves on top of the
          // Gerstner normal so micro-ripples break specular highlights.
          vec2 q = vWorld.xz;
          float n1 = vnoise(q * 0.45 + vec2( uTime * 0.18, uTime * 0.11)) - 0.5;
          float n2 = vnoise(q * 1.40 + vec2(-uTime * 0.22, uTime * 0.16)) - 0.5;
          vec3 micro = vec3(n1 * 0.4, 0.0, n2 * 0.4);
          vec3 nrm = normalize(vNormal + micro);

          vec3 viewV = normalize(cameraPosition - vWorld);
          float NoV = clamp(dot(nrm, viewV), 0.0, 1.0);
          float F = fresnelSchlick(NoV);

          // Base color: deep where Fresnel low (looking down), tinted toward
          // horizon at grazing angles (looking out). Multiply by horizon so
          // night turns it indigo and dusk turns it copper.
          vec3 deep    = mix(uDeepColor, uShallowTint, 0.35) * mix(0.6, 1.2, uDayW + 0.4 * uDuskW);
          vec3 surface = horiz;
          vec3 col = mix(deep, surface, F * 0.95 + 0.05);

          // Primary sun glint: tight specular lobe.
          vec3 sun = normalize(uSunDir);
          vec3 H = normalize(sun + viewV);
          float NoH = max(dot(nrm, H), 0.0);
          float sunUp = smoothstep(-0.05, 0.25, sun.y);
          float spec = pow(NoH, 220.0) * sunUp * 1.6;
          // Secondary wider lobe for "sparkle" off micro-ripples.
          float specBroad = pow(NoH, 32.0) * sunUp * 0.18;
          col += (vec3(1.0, 0.95, 0.85) * spec) + (vec3(1.0, 0.92, 0.75) * specBroad);

          // Foam on wave crests + a thin foam ring along the shoreline.
          float distR  = length(vWorld.xz);
          float shore  = smoothstep(uShoreR + 1.0, uShoreR - 2.5, distR);
          float crest  = smoothstep(0.55, 0.95, vCrest);
          float foam   = clamp(shore * 0.7 + crest * 0.6, 0.0, 1.0);
          // Foam noise so it isn't a uniform band.
          foam *= vnoise(q * 1.1 + vec2(uTime * 0.3, 0.0)) * 0.6 + 0.5;
          col = mix(col, vec3(0.95, 0.97, 1.0), foam * 0.75);

          // Slight darkening with depth (distance from center) for a sense of
          // a real water mass rather than a flat colored plane.
          float depthFade = smoothstep(0.0, 220.0, distR);
          col *= mix(1.0, 0.78, depthFade * 0.55);

          gl_FragColor = vec4(col, 0.95);
        }
      `,
    });
    // Push the water clearly below the shoreline AND enable polygonOffset so
    // any near-coplanar terrain triangles at the beach can't z-fight against
    // the water surface during camera rotation.
    waterMaterial.polygonOffset = true;
    waterMaterial.polygonOffsetFactor = 1;
    waterMaterial.polygonOffsetUnits  = 1;
    const water = new THREE.Mesh(waterGeo, waterMaterial);
    water.position.y = -0.55;
    scene.add(water);

    // ─── Distant mountain silhouette ───────────────────────────────────────
    // A ring of saw-tooth peaks beyond the water — frames the island and
    // gives the horizon depth instead of a flat-disc-with-fog.
    const mountainRingR = 220;
    const mtnSegs = 256;
    const mtnPos: number[] = [];

    const peakHeight = (a: number) => {
      const h =
        9 +
        Math.sin(a * 3.7) * 5 +
        Math.sin(a * 7.3 + 1.1) * 3 +
        Math.sin(a * 13.1 + 2.7) * 1.6 +
        Math.sin(a * 23.0) * 0.8;
      return Math.max(2.5, h);
    };

    for (let i = 0; i < mtnSegs; i++) {
      const a0 = (i / mtnSegs) * Math.PI * 2;
      const a1 = ((i + 1) / mtnSegs) * Math.PI * 2;
      const h0 = peakHeight(a0);
      const h1 = peakHeight(a1);
      const x0 = Math.cos(a0) * mountainRingR;
      const z0 = Math.sin(a0) * mountainRingR;
      const x1 = Math.cos(a1) * mountainRingR;
      const z1 = Math.sin(a1) * mountainRingR;
      const yBase = -3;

      // Two triangles per segment forming a quad with varying top height.
      mtnPos.push(x0, yBase, z0,  x1, yBase, z1,  x1, h1, z1);
      mtnPos.push(x0, yBase, z0,  x1, h1, z1,    x0, h0, z0);
    }

    const mtnGeo = new THREE.BufferGeometry();
    mtnGeo.setAttribute('position', new THREE.Float32BufferAttribute(mtnPos, 3));

    const mtnMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uHorizonDay:   { value: new THREE.Color(0.55, 0.78, 0.95) },
        uHorizonDusk:  { value: new THREE.Color(0.96, 0.55, 0.30) },
        uHorizonNight: { value: new THREE.Color(0.04, 0.05, 0.13) },
        uDayW:   { value: 0.0 },
        uDuskW:  { value: 0.0 },
        uNightW: { value: 0.0 },
      },
      side: THREE.FrontSide,
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
          // Reconstruct the same horizon color the sky uses, then darken
          // toward the base so peaks read as silhouettes against the sky.
          vec3 horiz = uHorizonDay  * uDayW
                     + uHorizonDusk * uDuskW
                     + uHorizonNight* uNightW;
          float alt = clamp(vH / 14.0, 0.0, 1.0);
          // Base of mountains darker, ridges nearly horizon-color.
          vec3 col = mix(horiz * 0.20, horiz * 0.75, alt);
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const mountains = new THREE.Mesh(mtnGeo, mtnMaterial);
    mountains.frustumCulled = false;
    scene.add(mountains);

    // ─── Grass ─────────────────────────────────────────────────────────────
    // Single InstancedMesh per chunk × per crossed-plane angle. The vertex
    // shader curves the blade into a bezier-style arc, applies wind, and
    // generates per-instance variation from the instance ID.
    const grassUniforms = {
      uTime: { value: 0 },
      uBase: { value: baseColor.clone() },
      uTip: { value: tipColor.clone() },
      uTip2: { value: new THREE.Color(0xb8d480) },
      uWindSpeed: { value: windSpeed },
      uWindStrength: { value: windStrength },
      uSunDir: skyUniforms.uSunDir,
      uSunColor: { value: new THREE.Color(0xfff1d8) },
      uAmbient: { value: new THREE.Color(0x4f6a8c) },
      // Golf-ball cursor: uBallPos.y is parked at 1000 when there's no
      // pointer in the canvas, so blades far below stay unaffected.
      uBallPos:    { value: new THREE.Vector3(0, 1000, 0) },
      uBallRadius: { value: 4.0 },
    };

    const grassMaterial = new THREE.ShaderMaterial({
      uniforms: grassUniforms,
      side: THREE.DoubleSide,
      // transparent: true with depthWrite: false caused blade-blade z-sort
      // flicker as the camera rotated — three doesn't depth-sort instances
      // within an InstancedMesh. Since the fragment uses `discard` for the
      // silhouette and outputs alpha=1, alpha-test rendering is the right
      // mode here.
      transparent: false,
      depthWrite: true,
      vertexShader: /* glsl */ `
        uniform float uTime;
        uniform float uWindSpeed;
        uniform float uWindStrength;
        uniform vec3  uBallPos;
        uniform float uBallRadius;

        attribute float aRandom;   // per-instance [0,1)
        attribute float aBend;     // per-instance bend amount
        attribute float aTwist;    // per-instance Y-rotation offset

        varying vec2 vUv;
        varying float vBend;
        varying float vGust;
        varying vec3 vNormalW;
        varying float vRandom;

        ${NOISE_GLSL}

        void main() {
          vUv = uv;
          vRandom = aRandom;

          // World-space anchor (after instanceMatrix translation).
          vec4 ip = instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0);
          vec3 anchor = (modelMatrix * ip).xyz;

          // Large-scale gust band: a slow noise field sweeping diagonally.
          // Same field is used to brighten the fragment — visible gust waves.
          // pow(.., 1.6) sharpens the band edges so gusts read as moving waves
          // rather than a continuous breeze.
          vec2 gustUV = anchor.xz * 0.045 + vec2(uTime * 0.18, uTime * 0.10);
          float gustRaw = vnoise(gustUV) * 0.5 + 0.5;
          float gust = pow(gustRaw, 1.6);
          vGust = gust;

          // Per-blade flutter: a fast small-amplitude noise so blades don't
          // bend in lockstep.
          float flutter = vnoise(anchor.xz * 0.6 + vec2(uTime * uWindSpeed, aRandom * 13.0));

          // Bezier-style bend along the blade's local Y axis.
          // Local position has y in [0, bladeHeight]. We bend forward in +x by
          // a quadratic curve, then rotate by aTwist around the world Y so
          // each blade faces a slightly different direction.
          vec3 p = position;
          float t = clamp(uv.y, 0.0, 1.0);
          float curve = t * t;          // bezier-ish (0 at base, 1 at tip)
          // Wider amplitude swing so the gust bands are visibly stronger than
          // the resting wind state — gust=0 ≈ 0.2·strength, gust=1 ≈ 1.2·strength.
          float windAmp = (gust * 1.0 + 0.2) * uWindStrength + flutter * 0.08;
          float bendAmt = aBend + windAmp;

          p.x += curve * bendAmt;
          p.y -= curve * bendAmt * 0.25;   // tip drops slightly when bent

          // Per-blade Y rotation (twist).
          float c = cos(aTwist);
          float s = sin(aTwist);
          vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);

          // Apply instance transform (translation + scale).
          vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);

          // Golf-ball deflection: push the blade radially away from the ball
          // and crush its tip downward, scaled by the blade-along factor t
          // (base barely moves, tip moves a lot). Anchor is the blade's base
          // in world space — distance is computed in xz so the ball doesn't
          // need to be perfectly grounded.
          vec3 anchorWorld = (modelMatrix * instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
          vec2 toBall = anchorWorld.xz - uBallPos.xz;
          float distXZ = length(toBall);
          if (distXZ < uBallRadius) {
            float push = 1.0 - distXZ / uBallRadius;
            push = pow(push, 1.5);
            vec2 awayDir = toBall / max(distXZ, 0.0001);
            float bladeT = clamp(t, 0.0, 1.0);
            // Radial outward push, strongest at tip.
            worldPos.xz += awayDir * push * 1.6 * bladeT;
            // Tip drops as the ball squashes the blade.
            worldPos.y -= push * 0.7 * bladeT;
          }

          // Normal — quick approximation of the bent blade's facing direction.
          vec3 nLocal = normalize(vec3(-bendAmt * (1.0 - t) * 2.0, 0.5, 1.0));
          vec3 nRot   = vec3(c * nLocal.x - s * nLocal.z, nLocal.y, s * nLocal.x + c * nLocal.z);
          vNormalW    = normalize((modelMatrix * vec4(nRot, 0.0)).xyz);

          vBend = bendAmt;
          gl_Position = projectionMatrix * viewMatrix * worldPos;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uBase;
        uniform vec3 uTip;
        uniform vec3 uTip2;
        uniform vec3 uSunDir;
        uniform vec3 uSunColor;
        uniform vec3 uAmbient;

        varying vec2 vUv;
        varying float vBend;
        varying float vGust;
        varying vec3 vNormalW;
        varying float vRandom;

        ${NOISE_GLSL}

        void main() {
          // Blade shape: taper width sharply near tip, soft fade at the very edge.
          float halfW = abs(vUv.x - 0.5);
          float taper = 1.0 - vUv.y * 0.85;
          if (halfW > taper * 0.5) discard;

          // Vertical AO — base darker than tip.
          float ao = pow(vUv.y, 1.4);

          // Two-tone tip with per-blade variation.
          vec3 tipMix = mix(uTip, uTip2, vRandom);
          vec3 col    = mix(uBase, tipMix, ao);

          // Wind-gust sheen: blades inside a bright gust band brighten enough
          // to read as a moving wave of color across the meadow.
          col += (vGust - 0.45) * 0.32 * vec3(0.9, 1.05, 0.7);

          // Fake SSS — sun back-lighting at low angles brightens the tip.
          float sunDot = max(dot(vNormalW, normalize(uSunDir)), 0.0);
          float backLit = pow(1.0 - sunDot, 2.0) * smoothstep(0.0, 0.3, uSunDir.y);
          col += backLit * vec3(0.35, 0.45, 0.20) * ao;

          // Lighting (wrap diffuse + ambient).
          float wrap = sunDot * 0.5 + 0.5;
          col = col * (uAmbient * 0.4 + uSunColor * wrap);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const bladeGeometry = new THREE.PlaneGeometry(grassWidth, grassHeight, 1, 6);
    // Origin at base of blade so bend curves naturally.
    bladeGeometry.translate(0, grassHeight / 2, 0);

    const grassMeshes: THREE.InstancedMesh[] = [];
    const halfCount = chunkCount / 2;
    const planeAngles = [0, Math.PI / 3, (2 * Math.PI) / 3]; // 3 cross-planes — 60° each, costs 1/2 of original 6
    const dummy = new THREE.Object3D();

    for (let cx = 0; cx < chunkCount; cx++) {
      for (let cz = 0; cz < chunkCount; cz++) {
        const chunkX = (cx - halfCount) * chunkSize;
        const chunkZ = (cz - halfCount) * chunkSize;

        for (const baseAngle of planeAngles) {
          const geo = bladeGeometry.clone();
          // Per-instance attributes.
          const aRandom = new Float32Array(grassDensity);
          const aBend = new Float32Array(grassDensity);
          const aTwist = new Float32Array(grassDensity);

          const inst = new THREE.InstancedMesh(geo, grassMaterial, grassDensity);
          inst.castShadow = true;
          inst.receiveShadow = false;
          inst.frustumCulled = false; // chunk-level culling deferred — they're cheap

          // Rejection-sample to the island circle so grass never floats above
          // water or pokes out of the underwater shelf. Hide rejected blades
          // by zeroing their scale rather than skipping the slot — keeps the
          // instance count uniform.
          for (let i = 0; i < grassDensity; i++) {
            let x = 0, z = 0, y = 0, accepted = false;
            for (let tries = 0; tries < 6; tries++) {
              const tx = chunkX + Math.random() * chunkSize;
              const tz = chunkZ + Math.random() * chunkSize;
              const ty = terrainHeight(tx, tz);
              const r  = Math.sqrt(tx * tx + tz * tz);
              if (ty > 0.35 && r < islandRadius) {
                x = tx; z = tz; y = ty;
                accepted = true;
                break;
              }
            }

            const scaleH = accepted ? 0.7 + Math.random() * 0.7 : 0;
            const scaleW = accepted ? 0.8 + Math.random() * 0.5 : 0;
            dummy.position.set(x, y, z);
            dummy.rotation.set(0, 0, 0);
            dummy.scale.set(scaleW, scaleH, 1);
            dummy.updateMatrix();
            inst.setMatrixAt(i, dummy.matrix);

            aRandom[i] = Math.random();
            aBend[i] = (Math.random() - 0.5) * 0.15;
            aTwist[i] = baseAngle + (Math.random() - 0.5) * 0.6;
          }

          inst.instanceMatrix.needsUpdate = true;
          geo.setAttribute('aRandom', new THREE.InstancedBufferAttribute(aRandom, 1));
          geo.setAttribute('aBend', new THREE.InstancedBufferAttribute(aBend, 1));
          geo.setAttribute('aTwist', new THREE.InstancedBufferAttribute(aTwist, 1));

          scene.add(inst);
          grassMeshes.push(inst);
        }
      }
    }

    // ─── Wildflowers ───────────────────────────────────────────────────────
    let flowersMesh: THREE.InstancedMesh | null = null;
    if (flowers) {
      const flowerColors = [
        new THREE.Color(0xffe066), // yellow
        new THREE.Color(0xff6b6b), // red
        new THREE.Color(0xffffff), // white daisy
        new THREE.Color(0xc77dff), // violet
        new THREE.Color(0xffa94d), // orange
      ];

      const flowerCount = Math.min(800, Math.floor(chunkCount * chunkCount * 12));
      const flowerGeo = new THREE.PlaneGeometry(0.35, 0.35);
      flowerGeo.translate(0, 0.18, 0);

      const flowerMat = new THREE.ShaderMaterial({
        uniforms: {
          uTime: grassUniforms.uTime,
          uSunColor: grassUniforms.uSunColor,
          uAmbient: grassUniforms.uAmbient,
        },
        side: THREE.DoubleSide,
        transparent: true,
        depthWrite: false,
        vertexShader: /* glsl */ `
          uniform float uTime;
          attribute vec3 aColor;
          attribute float aSeed;
          varying vec3 vColor;
          varying vec2 vUv;
          void main() {
            vUv = uv;
            vColor = aColor;
            vec3 p = position;
            float sway = sin(uTime * 1.4 + aSeed * 6.28) * 0.05;
            p.x += sway * uv.y;
            // Always face camera-ish: rotate around Y by view direction
            vec4 wp = modelMatrix * instanceMatrix * vec4(p, 1.0);
            vec3 toCam = normalize(cameraPosition - wp.xyz);
            float yaw = atan(toCam.x, toCam.z);
            float c = cos(yaw); float s = sin(yaw);
            vec3 r = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
            wp = modelMatrix * instanceMatrix * vec4(r, 1.0);
            gl_Position = projectionMatrix * viewMatrix * wp;
          }
        `,
        fragmentShader: /* glsl */ `
          uniform vec3 uSunColor;
          uniform vec3 uAmbient;
          varying vec3 vColor;
          varying vec2 vUv;
          void main() {
            // Petal disc with center.
            vec2 c = vUv - 0.5;
            float r = length(c);
            float petals = 0.5 + 0.5 * cos(atan(c.y, c.x) * 5.0);
            float disc = smoothstep(0.5, 0.3, r) * (0.5 + petals * 0.5);
            float center = smoothstep(0.12, 0.04, r);
            vec3 col = mix(vColor, vec3(1.0, 0.85, 0.2), center);
            col *= (uAmbient * 0.5 + uSunColor * 0.7);
            float a = max(disc, center * 1.0);
            if (a < 0.05) discard;
            gl_FragColor = vec4(col, a);
          }
        `,
      });

      flowersMesh = new THREE.InstancedMesh(flowerGeo, flowerMat, flowerCount);
      flowersMesh.frustumCulled = false;
      const fColors = new Float32Array(flowerCount * 3);
      const fSeeds = new Float32Array(flowerCount);
      for (let i = 0; i < flowerCount; i++) {
        let x = 0, z = 0, y = 0, accepted = false;
        for (let tries = 0; tries < 8; tries++) {
          const tx = (Math.random() - 0.5) * terrainSize;
          const tz = (Math.random() - 0.5) * terrainSize;
          const ty = terrainHeight(tx, tz);
          const r  = Math.sqrt(tx * tx + tz * tz);
          if (ty > 0.5 && r < islandRadius * 0.95) {
            x = tx; z = tz; y = ty;
            accepted = true;
            break;
          }
        }
        const s = accepted ? 0.7 + Math.random() * 0.6 : 0;
        dummy.position.set(x, y, z);
        dummy.rotation.set(0, Math.random() * Math.PI * 2, 0);
        dummy.scale.set(s, s, s);
        dummy.updateMatrix();
        flowersMesh.setMatrixAt(i, dummy.matrix);
        const col = flowerColors[Math.floor(Math.random() * flowerColors.length)];
        fColors[i * 3 + 0] = col.r;
        fColors[i * 3 + 1] = col.g;
        fColors[i * 3 + 2] = col.b;
        fSeeds[i] = Math.random();
      }
      flowersMesh.instanceMatrix.needsUpdate = true;
      flowerGeo.setAttribute('aColor', new THREE.InstancedBufferAttribute(fColors, 3));
      flowerGeo.setAttribute('aSeed', new THREE.InstancedBufferAttribute(fSeeds, 1));
      scene.add(flowersMesh);
    }

    // ─── Fireflies ─────────────────────────────────────────────────────────
    let fireflyPoints: THREE.Points | null = null;
    if (fireflies) {
      const fireflyCount = 220;
      const positions = new Float32Array(fireflyCount * 3);
      const seeds = new Float32Array(fireflyCount);
      for (let i = 0; i < fireflyCount; i++) {
        let x = 0, z = 0, y = 0;
        // Sample a polar coordinate inside the island radius so fireflies
        // hover above the meadow, not over the water.
        for (let tries = 0; tries < 8; tries++) {
          const r = Math.sqrt(Math.random()) * islandRadius * 0.92;
          const a = Math.random() * Math.PI * 2;
          const tx = Math.cos(a) * r;
          const tz = Math.sin(a) * r;
          const ty = terrainHeight(tx, tz);
          if (ty > 0.3) {
            x = tx; z = tz;
            y = ty + 0.4 + Math.random() * 1.8;
            break;
          }
        }
        positions[i * 3] = x;
        positions[i * 3 + 1] = y;
        positions[i * 3 + 2] = z;
        seeds[i] = Math.random();
      }
      const fGeo = new THREE.BufferGeometry();
      fGeo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      fGeo.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 1));

      const fMat = new THREE.ShaderMaterial({
        uniforms: {
          uTime: grassUniforms.uTime,
          uPixel: { value: renderer.getPixelRatio() },
          uIntensity: { value: 0.0 }, // updated per-frame from sun height
        },
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        vertexShader: /* glsl */ `
          uniform float uTime;
          uniform float uPixel;
          attribute float aSeed;
          varying float vBlink;
          void main() {
            vec3 p = position;
            // Wandering motion — small Lissajous around home position.
            float t = uTime + aSeed * 100.0;
            p.x += sin(t * 0.6 + aSeed * 6.28) * 0.7;
            p.y += sin(t * 0.4 + aSeed * 12.0) * 0.4;
            p.z += cos(t * 0.5 + aSeed * 7.7)  * 0.7;
            vec4 mvp = modelViewMatrix * vec4(p, 1.0);
            gl_Position = projectionMatrix * mvp;
            // Bigger sprites — they read as glowing insects, not stars.
            gl_PointSize = (14.0 + sin(t * 3.0) * 6.0) * uPixel * (28.0 / -mvp.z);
            vBlink = 0.55 + 0.45 * sin(t * 2.0 + aSeed * 6.28);
          }
        `,
        fragmentShader: /* glsl */ `
          uniform float uIntensity;
          varying float vBlink;
          void main() {
            vec2 c = gl_PointCoord - 0.5;
            float r = length(c);
            // Sharper core + wider warm halo so each firefly glows distinctly
            // even before bloom kicks in. Bloom pass amplifies the core.
            float core = smoothstep(0.30, 0.0, r);
            float halo = smoothstep(0.5, 0.05, r) * 0.55;
            float a = (core * 1.6 + halo) * vBlink * uIntensity;
            if (a < 0.01) discard;
            vec3 col = vec3(1.0, 0.92, 0.55);
            gl_FragColor = vec4(col * 1.4, a);
          }
        `,
      });

      fireflyPoints = new THREE.Points(fGeo, fMat);
      scene.add(fireflyPoints);
    }

    // ─── Golf-ball cursor ──────────────────────────────────────────────────
    // The OS cursor is hidden over the canvas; a 3D golf ball follows the
    // pointer along the terrain surface and pushes grass aside via the
    // grass shader's uBallPos uniform. Re-engaging the OS cursor when the
    // pointer leaves the canvas avoids a "lost cursor" UX trap.
    const ballRadius = 1.25;
    const ballGeo = new THREE.SphereGeometry(ballRadius, 48, 32);
    const ballMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uSunDir:   skyUniforms.uSunDir,
        uSunColor: grassUniforms.uSunColor,
        uAmbient:  grassUniforms.uAmbient,
      },
      vertexShader: /* glsl */ `
        varying vec3 vNormal;
        varying vec3 vWorld;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorld = wp.xyz;
          vNormal = normalize(normalMatrix * normal);
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uSunDir;
        uniform vec3 uSunColor;
        uniform vec3 uAmbient;
        varying vec3 vNormal;
        varying vec3 vWorld;
        void main() {
          // Procedural dimples — repeating circular wells in lat/lon space.
          // Looks plausibly golf-ball-like without a texture.
          vec3 n = normalize(vNormal);
          float lat = asin(clamp(n.y, -1.0, 1.0));
          float lon = atan(n.z, n.x);
          vec2 grid = vec2(lon * 8.0, lat * 12.0);
          vec2 cell = fract(grid) - 0.5;
          float dimpleR = length(cell);
          float dimple = smoothstep(0.34, 0.18, dimpleR);
          // Bend the normal slightly into the dimples so light pools in them.
          vec3 nBent = normalize(n - 0.35 * dimple * n);

          float lit = max(dot(nBent, normalize(uSunDir)), 0.0);
          vec3 base = mix(vec3(0.97, 0.97, 0.96), vec3(0.78, 0.80, 0.86), dimple * 0.6);
          vec3 col = base * (uAmbient * 0.5 + uSunColor * (lit * 0.8 + 0.25));

          // Tight specular highlight.
          vec3 viewV = normalize(cameraPosition - vWorld);
          vec3 reflV = reflect(-normalize(uSunDir), nBent);
          float spec = pow(max(dot(reflV, viewV), 0.0), 64.0);
          col += vec3(1.0, 0.97, 0.92) * spec * 0.5;

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const ball = new THREE.Mesh(ballGeo, ballMaterial);
    ball.castShadow = true;
    ball.position.set(0, 1000, 0); // parked off-screen until first pointer
    scene.add(ball);

    // Soft contact shadow — a faint dark disk pinned to the ball's xz under
    // it. Helps anchor the ball visually even where the directional sun's
    // shadow falls oddly relative to the camera angle.
    const ballShadowMat = new THREE.MeshBasicMaterial({
      color: 0x000000,
      transparent: true,
      opacity: 0.35,
      depthWrite: false,
    });
    const ballShadow = new THREE.Mesh(
      new THREE.CircleGeometry(ballRadius * 1.6, 32),
      ballShadowMat,
    );
    ballShadow.rotation.x = -Math.PI / 2;
    ballShadow.position.set(0, 1000, 0);
    ballShadow.renderOrder = 1;
    scene.add(ballShadow);

    // Pointer → world-space hit on the terrain.
    const mouseNDC   = new THREE.Vector2();
    const raycaster  = new THREE.Raycaster();
    const ballTarget = new THREE.Vector3(0, 1000, 0);
    let pointerInside = false;

    const onPointerMove = (e: PointerEvent) => {
      const rect = renderer.domElement.getBoundingClientRect();
      mouseNDC.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      mouseNDC.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      raycaster.setFromCamera(mouseNDC, camera);
      const hits = raycaster.intersectObject(terrain, false);
      if (hits.length > 0) {
        ballTarget.copy(hits[0].point);
        pointerInside = true;
      }
    };
    const onPointerLeave = () => {
      pointerInside = false;
      ballTarget.set(0, 1000, 0); // park ball off-screen
    };
    renderer.domElement.style.cursor = 'none';
    renderer.domElement.addEventListener('pointermove', onPointerMove);
    renderer.domElement.addEventListener('pointerleave', onPointerLeave);

    // ─── Post-processing (bloom) ───────────────────────────────────────────
    // Bloom amplifies the sun disc and fireflies into glowing highlights.
    // High threshold (~0.92) so only genuinely bright pixels bloom — keeps
    // the meadow itself crisp.
    const composer = new EffectComposer(renderer);
    composer.setSize(W0, H0);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    composer.addPass(new RenderPass(scene, camera));
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(W0, H0),
      /* strength */ 0.55,
      /* radius   */ 0.45,
      /* threshold*/ 0.92,
    );
    composer.addPass(bloomPass);
    composer.addPass(new OutputPass());

    // ─── Animation loop ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    let bobApplied = 0;

    // Horizon palettes — must match the sky shader's three palettes at h=0
    // so fog and mountain silhouette dissolve cleanly into the sky.
    const horizonDay   = new THREE.Color(0.55, 0.78, 0.95);
    const horizonDusk  = new THREE.Color(0.96, 0.55, 0.30);
    const horizonNight = new THREE.Color(0.04, 0.05, 0.13);

    const updateTimeOfDay = (tod: number) => {
      // Sun travels in a great circle: t=0 sunrise (east horizon), 0.25 noon,
      // 0.5 sunset (west horizon), 0.75 midnight. Slight z tilt so noon sun
      // isn't dead-overhead.
      const angle = tod * Math.PI * 2;
      const dir = new THREE.Vector3(Math.cos(angle), Math.sin(angle), 0.25)
        .normalize();
      skyUniforms.uSunDir.value.copy(dir);
      sun.position.copy(dir).multiplyScalar(80);
      sun.target.position.set(0, 0, 0);

      // Sun color + intensity sweep.
      const dayW = THREE.MathUtils.smoothstep(dir.y, -0.05, 0.30);
      const nightW = THREE.MathUtils.smoothstep(-dir.y, -0.05, 0.10);
      const duskW = Math.exp(-Math.pow(dir.y * 4.0, 2.0));
      const sunWarm = new THREE.Color(0xff8a3c);
      const sunCool = new THREE.Color(0xfff1d8);
      sun.color.copy(sunWarm).lerp(sunCool, dayW);
      sun.intensity = 0.05 + 1.55 * dayW;

      ambient.intensity = 0.18 + 0.55 * dayW + 0.05 * nightW;
      ambient.color.setHSL(0.6, 0.5, 0.4 + 0.25 * dayW);
      ambient.groundColor.setHSL(0.08, 0.4, 0.05 + 0.10 * dayW);

      // Fog tint = sky horizon color (same three-palette blend the shader uses).
      // This guarantees the field's haze fades into the sky seamlessly.
      // (THREE.Color has no addScaledVector — we mix component-wise.)
      const fogCol = new THREE.Color(
        horizonDay.r * dayW + horizonDusk.r * duskW + horizonNight.r * nightW,
        horizonDay.g * dayW + horizonDusk.g * duskW + horizonNight.g * nightW,
        horizonDay.b * dayW + horizonDusk.b * duskW + horizonNight.b * nightW,
      );
      (scene.fog as THREE.FogExp2).color.copy(fogCol);
      (scene.fog as THREE.FogExp2).density = 0.010 + 0.006 * (1 - dayW);
      renderer.setClearColor(fogCol, 1);

      // Mountains share the same horizon weights so their silhouettes match
      // exactly the sky color directly behind them.
      mtnMaterial.uniforms.uDayW.value   = dayW;
      mtnMaterial.uniforms.uDuskW.value  = duskW;
      mtnMaterial.uniforms.uNightW.value = nightW;

      // Water tracks the same horizon palette so it looks like sky reflection.
      waterMaterial.uniforms.uDayW.value   = dayW;
      waterMaterial.uniforms.uDuskW.value  = duskW;
      waterMaterial.uniforms.uNightW.value = nightW;

      // Grass uniforms follow.
      grassUniforms.uSunColor.value.copy(sun.color).multiplyScalar(0.7 + dayW * 0.6);
      grassUniforms.uAmbient.value.set(0.15 + 0.10 * dayW, 0.20 + 0.18 * dayW, 0.30 + 0.18 * dayW);

      skyUniforms.uTimeOfDay.value = tod;

      if (fireflyPoints) {
        const fmat = fireflyPoints.material as THREE.ShaderMaterial;
        // Fade in below sun-y = 0.15, fully bright at -0.05.
        const f = THREE.MathUtils.smoothstep(0.15 - dir.y, 0.0, 0.20);
        fmat.uniforms.uIntensity.value = f;
      }
    };

    const animate = () => {
      const elapsed = clock.getElapsedTime();
      grassUniforms.uTime.value = elapsed;
      waterMaterial.uniforms.uTime.value = elapsed;

      // Smooth ball follow — exponential approach so quick mouse moves don't
      // teleport the ball, but it never lags more than a frame or two behind.
      // Lift slightly above the surface so the ball sits on the grass tips
      // rather than half-buried.
      const lift = ballRadius * 0.85;
      const tx = pointerInside ? ballTarget.x : 0;
      const ty = pointerInside ? ballTarget.y + lift : 1000;
      const tz = pointerInside ? ballTarget.z : 0;
      ball.position.x += (tx - ball.position.x) * 0.30;
      ball.position.y += (ty - ball.position.y) * 0.30;
      ball.position.z += (tz - ball.position.z) * 0.30;
      // Roll the ball as it moves (visual flourish — looks alive).
      const dx = tx - ball.position.x;
      const dz = tz - ball.position.z;
      ball.rotation.x += dz * 0.05;
      ball.rotation.z -= dx * 0.05;

      grassUniforms.uBallPos.value.copy(ball.position);
      ballShadow.position.set(
        ball.position.x,
        // pin to surface — slight offset above terrain at ball xz.
        Math.max(ball.position.y - lift, 0.05),
        ball.position.z,
      );
      // Shadow fades as ball lifts away from ground (dragging mouse fast).
      ballShadow.visible = pointerInside && ball.position.y < 30;

      const tod = dayLengthSeconds > 0
        ? (elapsed / dayLengthSeconds) % 1
        : fixedTimeOfDay;
      updateTimeOfDay(tod);

      // Camera bob — applied AFTER controls.update so OrbitControls' spherical
      // state stays clean (we undo last frame's bob first). Reads as a slow
      // drone-style float when auto-rotating; off when the user is driving.
      camera.position.y -= bobApplied;
      controls.update();
      bobApplied = autoRotate
        ? Math.sin(elapsed * 0.55) * 0.30 + Math.sin(elapsed * 0.27 + 1.0) * 0.18
        : 0;
      camera.position.y += bobApplied;

      composer.render();
      raf = requestAnimationFrame(animate);
    };

    updateTimeOfDay(fixedTimeOfDay);
    animate();

    // ─── Resize observer ───────────────────────────────────────────────────
    // Keeps the renderer + camera + composer + bloom-pass in sync when the
    // container changes size (drawer open/close on mobile, window resize,
    // orientation rotation, devtools panel toggle). Throttled by RAF
    // implicitly: setSize is a one-line GPU op and runs only on observed
    // changes, not every frame.
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

    // ─── Cleanup ───────────────────────────────────────────────────────────
    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      controls.dispose();
      grassMeshes.forEach((m) => {
        m.geometry.dispose();
      });
      grassMaterial.dispose();
      bladeGeometry.dispose();
      terrainGeo.dispose();
      terrainMaterial.dispose();
      skyMaterial.dispose();
      sky.geometry.dispose();
      mtnGeo.dispose();
      mtnMaterial.dispose();
      waterGeo.dispose();
      waterMaterial.dispose();
      ballGeo.dispose();
      ballMaterial.dispose();
      ballShadow.geometry.dispose();
      ballShadowMat.dispose();
      renderer.domElement.removeEventListener('pointermove', onPointerMove);
      renderer.domElement.removeEventListener('pointerleave', onPointerLeave);
      composer.dispose();
      if (flowersMesh) {
        flowersMesh.geometry.dispose();
        (flowersMesh.material as THREE.Material).dispose();
      }
      if (fireflyPoints) {
        fireflyPoints.geometry.dispose();
        (fireflyPoints.material as THREE.Material).dispose();
      }
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [
    width,
    height,
    grassDensity,
    chunkSize,
    chunkCount,
    grassHeight,
    grassWidth,
    baseColor,
    tipColor,
    windSpeed,
    windStrength,
    dayLengthSeconds,
    fixedTimeOfDay,
    autoRotate,
    fireflies,
    flowers,
  ]);

  // When width/height props are omitted we fill the parent — gives the
  // demo a clean way to wrap us in a flex/grid cell that resizes naturally.
  const sx = (width !== undefined && height !== undefined)
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };
  return <Box ref={containerRef} sx={sx} />;
};

export default FluffyGrass;
