/**
 * Sand Dunes — cinematic re-implementation (2026-05-08).
 *
 * Built in the same idiom as FluffyGrass:
 *  - Procedural sand surface from ridged multifractal noise (sharp crests,
 *    asymmetric slip faces facing the wind).
 *  - Day/night cycle drives sun direction, sky palette, fog tint, mountain
 *    silhouette, sand color, and sparkle behaviour from one uTimeOfDay.
 *  - Sky shader: dawn/day/dusk/night palette mix + sun disc + horizon glow
 *    + sparse stars at night — same shape as the grass version, retuned
 *    for desert (warmer, hazier).
 *  - Distant rocky-mountain ring tied to the same horizon palette so it
 *    sits inside the haze cleanly.
 *  - Wind-driven sand particles drifting across the field, shear-amplified
 *    near crests; mirage heat-shimmer near the horizon at noon.
 *  - Bloom amplifies the sun, mountain rim-light and sand glints; camera
 *    bob when auto-rotating reads as a slow drone shot.
 *
 * No physical sky model dependency — everything is one shader so the
 * whole frame can be retuned by the time-of-day slider.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { Box } from '@mui/material';

export interface SandDunesProps {
  width?: number;
  height?: number;
  /** Edge length of the dune field in world units. */
  fieldSize?: number;
  /** Number of segments per side of the heightmap mesh. Higher = crisper crests, more verts. */
  fieldSegments?: number;
  /** Wind direction in radians (0 = +x, π/2 = +z). Crests run perpendicular to this. */
  windDirRad?: number;
  /** 0 freezes the cycle at fixedTimeOfDay, otherwise full sweep length. */
  dayLengthSeconds?: number;
  /** [0,1) — 0 sunrise, 0.25 noon, 0.5 sunset, 0.75 midnight. */
  fixedTimeOfDay?: number;
  /** Auto-rotate the camera (with subtle drone bob) around the dune field. */
  autoRotate?: boolean;
  /** Whether to render airborne sand grains. */
  sandParticles?: boolean;
  /** Whether to render the heat-shimmer pass at noon. */
  mirage?: boolean;
}

// Shared GLSL helpers — same noise primitives used by FluffyGrass so the
// two scenes look like they live in the same world. Adds ridged() on top
// of value noise: r = 2|0.5 - n| stretches the field into sharp ridges.
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
  // Ridged multifractal — produces sharp ridges instead of smooth blobs.
  float ridged(vec2 p) {
    return 1.0 - abs(vnoise(p));
  }
  // Five-octave ridged stack with wind-aligned dominant direction.
  // dominantDir parameter unused inside the shader — caller pre-rotates p
  // before calling so dunes' long axis runs perpendicular to the wind.
  float duneHeight(vec2 p) {
    float h = 0.0;
    float amp = 1.0;
    float freq = 0.012;
    for (int i = 0; i < 5; i++) {
      // Ridged^2 sharpens the crests further; squared in [0,1] keeps the
      // cumulative scale bounded.
      float r = ridged(p * freq);
      h += r * r * amp;
      amp *= 0.5;
      freq *= 2.0;
    }
    // Bias up so the field sits above y=0; scale to a useful dune height.
    return h * 22.0;
  }
`;

// CPU mirror of the GLSL ridged-multifractal so we can place mountain
// silhouettes / sample heights from JavaScript.
const cpuHash2 = (x: number, y: number): [number, number] => {
  const px = x * 127.1 + y * 311.7;
  const py = x * 269.5 + y * 183.3;
  const sx = Math.sin(px) * 43758.5453123;
  const sy = Math.sin(py) * 43758.5453123;
  return [-1 + 2 * (sx - Math.floor(sx)), -1 + 2 * (sy - Math.floor(sy))];
};
const cpuVnoise = (x: number, y: number): number => {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  const fx = x - ix;
  const fy = y - iy;
  const ux = fx * fx * (3 - 2 * fx);
  const uy = fy * fy * (3 - 2 * fy);
  const [a0, a1] = cpuHash2(ix, iy);
  const [b0, b1] = cpuHash2(ix + 1, iy);
  const [c0, c1] = cpuHash2(ix, iy + 1);
  const [d0, d1] = cpuHash2(ix + 1, iy + 1);
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

const SandDunes: React.FC<SandDunesProps> = ({
  width,
  height,
  fieldSize = 600,
  fieldSegments = 320,
  windDirRad = 0.4,
  dayLengthSeconds = 90,
  fixedTimeOfDay = 0.20,
  autoRotate = true,
  sandParticles = true,
  mirage = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W = width ?? container.clientWidth;
    const H = height ?? container.clientHeight;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0xffd9a0, 0.0028);

    const camera = new THREE.PerspectiveCamera(55, W / H, 0.1, 4000);
    camera.position.set(-110, 65, 160);
    camera.lookAt(0, 12, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W, H);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.10;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.minDistance = 12;
    controls.maxDistance = 800;
    controls.maxPolarAngle = Math.PI * 0.49;
    controls.target.set(0, 12, 0);
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 0.25;

    // ─── Sky dome ──────────────────────────────────────────────────────────
    // Same three-palette structure as FluffyGrass, retuned for desert: dustier
    // day, hotter dusk, deeper purple night with more visible stars.
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
        uniform float uTimeOfDay;
        varying vec3 vDir;

        // Desert-tuned palettes. zenith ↘ horizon.
        vec3 skyDay(float h) {
          return mix(vec3(0.96, 0.86, 0.66),  // hazy yellow horizon
                     vec3(0.32, 0.55, 0.90),  // washed blue zenith
                     pow(clamp(h, 0.0, 1.0), 0.5));
        }
        vec3 skyDusk(float h) {
          return mix(vec3(1.00, 0.42, 0.20),  // hot red horizon
                     vec3(0.20, 0.10, 0.32),  // violet zenith
                     pow(clamp(h, 0.0, 1.0), 0.55));
        }
        vec3 skyNight(float h) {
          return mix(vec3(0.06, 0.05, 0.13),
                     vec3(0.005, 0.005, 0.04),
                     pow(clamp(h, 0.0, 1.0), 0.7));
        }

        void main() {
          vec3 d = normalize(vDir);
          float h = clamp(d.y, 0.0, 1.0);

          float dayW   = smoothstep(-0.05, 0.30, uSunDir.y);
          float duskW  = exp(-pow(uSunDir.y * 4.0, 2.0));
          float nightW = smoothstep(0.05, -0.10, uSunDir.y);

          vec3 col = skyDay(h)   * dayW
                   + skyDusk(h)  * duskW
                   + skyNight(h) * nightW;

          // Sun + warm dust glow.
          float sunDot = clamp(dot(d, normalize(uSunDir)), 0.0, 1.0);
          float disc = smoothstep(0.9985, 0.9999, sunDot);
          float glow = pow(sunDot, 16.0) * 0.50 + pow(sunDot, 3.0) * 0.18;
          vec3 sunCol = mix(vec3(1.0, 0.55, 0.25), vec3(1.0, 0.95, 0.80), dayW);
          col += sunCol * disc * 6.5;
          col += sunCol * glow * (0.80 + duskW * 1.6);

          // Stars at night — slightly brighter than the grass version since
          // the desert sky has no light pollution.
          float starN = fract(sin(dot(d.xz * 700.0, vec2(12.989, 78.233))) * 43758.55);
          float star = smoothstep(0.9960, 0.9990, starN) * nightW * smoothstep(0.0, 0.4, h);
          col += vec3(star) * 1.4;

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(2400, 32, 16), skyMaterial);
    scene.add(sky);

    // ─── Lights ────────────────────────────────────────────────────────────
    const ambient = new THREE.HemisphereLight(0xffe0a0, 0x4a1f0b, 0.55);
    scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xfff1c4, 1.6);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.left = -200;
    sun.shadow.camera.right = 200;
    sun.shadow.camera.top = 200;
    sun.shadow.camera.bottom = -200;
    sun.shadow.camera.near = 1;
    sun.shadow.camera.far = 800;
    sun.shadow.bias = -0.0006;
    sun.shadow.radius = 4;
    scene.add(sun);
    scene.add(sun.target);

    // ─── Dune field ────────────────────────────────────────────────────────
    // CPU-side height generation for crisp normals. Same shape as the GLSL
    // duneHeight() but with the field rotated so dune crests run
    // perpendicular to the wind.
    const cosW = Math.cos(-windDirRad);
    const sinW = Math.sin(-windDirRad);

    const cpuRidged = (x: number, y: number) => 1 - Math.abs(cpuVnoise(x, y));
    const cpuDuneHeight = (worldX: number, worldZ: number): number => {
      // Pre-rotate so the long axis of the dunes is perpendicular to wind.
      const x = cosW * worldX - sinW * worldZ;
      const z = sinW * worldX + cosW * worldZ;
      let h = 0;
      let amp = 1;
      let freq = 0.012;
      for (let i = 0; i < 5; i++) {
        const r = cpuRidged(x * freq, z * freq);
        h += r * r * amp;
        amp *= 0.5;
        freq *= 2;
      }
      return h * 22;
    };

    const fieldGeo = new THREE.PlaneGeometry(fieldSize, fieldSize, fieldSegments, fieldSegments);
    fieldGeo.rotateX(-Math.PI / 2);
    {
      const pos = fieldGeo.attributes.position as THREE.BufferAttribute;
      for (let i = 0; i < pos.count; i++) {
        pos.setY(i, cpuDuneHeight(pos.getX(i), pos.getZ(i)));
      }
      pos.needsUpdate = true;
      fieldGeo.computeVertexNormals();
    }

    // Sand material — slope-tinted color, micro-ripple bump, sparkle
    // glints, sun-direction shading. Ripples drift over time so the
    // field looks alive.
    const sandUniforms = {
      uTime: { value: 0 },
      uWindDir: { value: new THREE.Vector2(Math.cos(windDirRad), Math.sin(windDirRad)) },
      uSunDir: skyUniforms.uSunDir,
      uSunColor: { value: new THREE.Color(0xfff1c4) },
      uAmbient: { value: new THREE.Color(0x6a4830) },
      uSandLit:    { value: new THREE.Color(0xf5d597) }, // lit crest color
      uSandShaded: { value: new THREE.Color(0xa86b3a) }, // shaded lee color
      uSandDeep:   { value: new THREE.Color(0x5a3818) }, // deep trough
    };

    const sandMaterial = new THREE.ShaderMaterial({
      uniforms: sandUniforms,
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
        uniform float uTime;
        uniform vec2  uWindDir;
        uniform vec3  uSunDir;
        uniform vec3  uSunColor;
        uniform vec3  uAmbient;
        uniform vec3  uSandLit;
        uniform vec3  uSandShaded;
        uniform vec3  uSandDeep;

        varying vec3 vWorld;
        varying vec3 vNormal;

        ${NOISE_GLSL}

        void main() {
          // Micro-ripples: high-frequency noise along the wind axis, low
          // along the dune-axis. Drifts slowly with time. Used only for
          // shading, not for displacement (would cost too much at this LOD).
          vec2 wAxis = normalize(uWindDir);
          vec2 cAxis = vec2(-wAxis.y, wAxis.x);
          float along = dot(vWorld.xz, wAxis);
          float across = dot(vWorld.xz, cAxis);
          float ripple = vnoise(vec2(along * 0.18 + uTime * 0.45, across * 0.04));
          // Synthetic normal perturbation in tangent space.
          vec3 rippleN = vec3(ripple * 0.45, 0.0, 0.0);
          vec3 N = normalize(vNormal + rippleN);

          // Slope cues: faces leaning into the sun get warm-lit, away get
          // shaded. We blend three sand tones based on slope + height for
          // dune-crest brightness.
          float lit  = max(dot(N, normalize(uSunDir)), 0.0);
          float slope = 1.0 - clamp(N.y, 0.0, 1.0);
          float crestT = smoothstep(8.0, 22.0, vWorld.y);

          vec3 col = mix(uSandShaded, uSandLit, lit * 0.85 + 0.15);
          col = mix(col, uSandDeep, smoothstep(0.4, 0.9, slope));   // dark slip face
          col = mix(col, uSandLit * 1.05, crestT * 0.4);            // bright crest

          // Glints — tiny specular hits from grains catching the sun.
          // Power 64 → very tight highlight; rare hash per fragment so the
          // glints look like individual grains.
          vec3 viewV = normalize(cameraPosition - vWorld);
          vec3 reflV = reflect(-normalize(uSunDir), N);
          float spec = pow(max(dot(reflV, viewV), 0.0), 64.0);
          float glintHash = fract(sin(dot(floor(vWorld.xz * 4.0), vec2(12.9, 78.2))) * 43758.55);
          float glint = spec * step(0.94, glintHash) * smoothstep(0.05, 0.3, uSunDir.y);
          col += vec3(1.0, 0.95, 0.78) * glint * 1.3;

          // Lighting: wrap diffuse + ambient.
          float wrap = lit * 0.5 + 0.5;
          col = col * (uAmbient * 0.55 + uSunColor * wrap);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const dunes = new THREE.Mesh(fieldGeo, sandMaterial);
    dunes.receiveShadow = true;
    dunes.castShadow = true;
    scene.add(dunes);

    // ─── Distant mountain rim ──────────────────────────────────────────────
    // Rocky saw-tooth ring well past the dune field. Same horizon palette as
    // sky/fog so the silhouette dissolves cleanly into the haze.
    const ringR = 1400;
    const segs = 384;
    const mtnPos: number[] = [];

    const peak = (a: number) => {
      const h =
        80 +
        Math.sin(a * 4.1) * 50 +
        Math.sin(a * 9.7 + 1.2) * 30 +
        Math.sin(a * 17.3 + 2.4) * 16 +
        Math.sin(a * 31.0) * 8;
      return Math.max(20, h);
    };

    for (let i = 0; i < segs; i++) {
      const a0 = (i / segs) * Math.PI * 2;
      const a1 = ((i + 1) / segs) * Math.PI * 2;
      const h0 = peak(a0);
      const h1 = peak(a1);
      const x0 = Math.cos(a0) * ringR;
      const z0 = Math.sin(a0) * ringR;
      const x1 = Math.cos(a1) * ringR;
      const z1 = Math.sin(a1) * ringR;
      const yBase = -10;
      mtnPos.push(x0, yBase, z0,  x1, yBase, z1,  x1, h1, z1);
      mtnPos.push(x0, yBase, z0,  x1, h1, z1,    x0, h0, z0);
    }

    const mtnGeo = new THREE.BufferGeometry();
    mtnGeo.setAttribute('position', new THREE.Float32BufferAttribute(mtnPos, 3));

    const mtnMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uHorizonDay:   { value: new THREE.Color(0.96, 0.86, 0.66) },
        uHorizonDusk:  { value: new THREE.Color(1.00, 0.42, 0.20) },
        uHorizonNight: { value: new THREE.Color(0.06, 0.05, 0.13) },
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
          vec3 horiz = uHorizonDay  * uDayW
                     + uHorizonDusk * uDuskW
                     + uHorizonNight* uNightW;
          float alt = clamp(vH / 130.0, 0.0, 1.0);
          // Mountains darker than horizon at the base, near horizon-color at
          // the rim — reads as "rocky silhouette in haze".
          vec3 col = mix(horiz * 0.18, horiz * 0.70, alt);
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const mountains = new THREE.Mesh(mtnGeo, mtnMaterial);
    mountains.frustumCulled = false;
    scene.add(mountains);

    // ─── Airborne sand particles ───────────────────────────────────────────
    let sandPoints: THREE.Points | null = null;
    if (sandParticles) {
      const sandCount = 1500;
      const positions = new Float32Array(sandCount * 3);
      const seeds = new Float32Array(sandCount);
      for (let i = 0; i < sandCount; i++) {
        const x = (Math.random() - 0.5) * fieldSize * 0.9;
        const z = (Math.random() - 0.5) * fieldSize * 0.9;
        const baseY = cpuDuneHeight(x, z);
        positions[i * 3] = x;
        positions[i * 3 + 1] = baseY + Math.random() * 6;
        positions[i * 3 + 2] = z;
        seeds[i] = Math.random();
      }
      const sGeo = new THREE.BufferGeometry();
      sGeo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      sGeo.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 1));

      const sMat = new THREE.ShaderMaterial({
        uniforms: {
          uTime: sandUniforms.uTime,
          uWindDir: sandUniforms.uWindDir,
          uPixel: { value: renderer.getPixelRatio() },
          uSunColor: sandUniforms.uSunColor,
          uIntensity: { value: 0 },
        },
        transparent: true,
        depthWrite: false,
        blending: THREE.NormalBlending,
        vertexShader: /* glsl */ `
          uniform float uTime;
          uniform vec2 uWindDir;
          uniform float uPixel;
          attribute float aSeed;
          varying float vDist;
          void main() {
            vec3 p = position;
            // Drift along the wind, with a small vertical sway. Wraps the
            // field like a torus so grains never fall off the edge.
            float t = uTime + aSeed * 60.0;
            vec2 drift = uWindDir * (t * 6.0 + aSeed * 80.0);
            p.x += drift.x;
            p.z += drift.y;
            // Wrap inside ±half-field.
            float halfF = ${(fieldSize / 2).toFixed(1)};
            p.x = mod(p.x + halfF, 2.0 * halfF) - halfF;
            p.z = mod(p.z + halfF, 2.0 * halfF) - halfF;
            p.y += sin(t * 1.2 + aSeed * 6.28) * 0.6;
            vec4 mvp = modelViewMatrix * vec4(p, 1.0);
            gl_Position = projectionMatrix * mvp;
            gl_PointSize = (1.6 + aSeed * 1.4) * uPixel * (60.0 / -mvp.z);
            vDist = -mvp.z;
          }
        `,
        fragmentShader: /* glsl */ `
          uniform vec3 uSunColor;
          uniform float uIntensity;
          varying float vDist;
          void main() {
            vec2 c = gl_PointCoord - 0.5;
            float r = length(c);
            float a = smoothstep(0.5, 0.0, r) * uIntensity;
            // Fade with distance so close grains read clearly while far
            // ones become haze. Tinted by the current sun color.
            float far = smoothstep(800.0, 60.0, vDist);
            a *= far;
            if (a < 0.02) discard;
            gl_FragColor = vec4(uSunColor * 0.95, a * 0.7);
          }
        `,
      });

      sandPoints = new THREE.Points(sGeo, sMat);
      sandPoints.frustumCulled = false;
      scene.add(sandPoints);
    }

    // ─── Mirage / heat shimmer ─────────────────────────────────────────────
    // A thin transparent strip just above the horizon distorts what's
    // behind it via a tiny vertical wobble. Active only at high sun.
    let mirageMesh: THREE.Mesh | null = null;
    if (mirage) {
      const mGeo = new THREE.PlaneGeometry(2200, 36, 80, 1);
      mGeo.translate(0, 18, -1100); // sits in the sky, near horizon line
      const mMat = new THREE.ShaderMaterial({
        uniforms: {
          uTime: sandUniforms.uTime,
          uIntensity: { value: 0 },
        },
        transparent: true,
        depthWrite: false,
        side: THREE.DoubleSide,
        vertexShader: /* glsl */ `
          varying vec2 vUv;
          void main() {
            vUv = uv;
            gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
          }
        `,
        fragmentShader: /* glsl */ `
          uniform float uTime;
          uniform float uIntensity;
          varying vec2 vUv;
          void main() {
            // Banded warm haze that ripples slowly. We don't actually
            // distort the framebuffer (no render target needed) — we just
            // overlay a faint warm warp that reads as heat shimmer at the
            // horizon. Cheap, looks the part.
            float band = smoothstep(0.0, 0.3, vUv.y) * smoothstep(1.0, 0.6, vUv.y);
            float wob = sin(vUv.x * 90.0 + uTime * 3.0) * 0.5 + 0.5;
            float a = band * wob * 0.10 * uIntensity;
            gl_FragColor = vec4(1.0, 0.85, 0.55, a);
          }
        `,
      });
      mirageMesh = new THREE.Mesh(mGeo, mMat);
      mirageMesh.frustumCulled = false;
      // Keep mirage strip facing the camera horizontally — re-aimed each frame.
      scene.add(mirageMesh);
    }

    // ─── Post-processing ───────────────────────────────────────────────────
    const composer = new EffectComposer(renderer);
    composer.setSize(W, H);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    composer.addPass(new RenderPass(scene, camera));
    composer.addPass(new UnrealBloomPass(
      new THREE.Vector2(W, H),
      /* strength */ 0.65,
      /* radius   */ 0.55,
      /* threshold*/ 0.88,
    ));
    composer.addPass(new OutputPass());

    // ─── Time-of-day driver ────────────────────────────────────────────────
    const horizonDay   = new THREE.Color(0.96, 0.86, 0.66);
    const horizonDusk  = new THREE.Color(1.00, 0.42, 0.20);
    const horizonNight = new THREE.Color(0.06, 0.05, 0.13);

    const updateTimeOfDay = (tod: number) => {
      const angle = tod * Math.PI * 2;
      // Z tilt 0.18 → sun stays slightly south of zenith at noon.
      const dir = new THREE.Vector3(Math.cos(angle), Math.sin(angle), 0.18).normalize();
      skyUniforms.uSunDir.value.copy(dir);
      sun.position.copy(dir).multiplyScalar(600);
      sun.target.position.set(0, 0, 0);

      const dayW   = THREE.MathUtils.smoothstep(dir.y, -0.05, 0.30);
      const nightW = THREE.MathUtils.smoothstep(-dir.y, -0.05, 0.10);
      const duskW  = Math.exp(-Math.pow(dir.y * 4.0, 2.0));

      const sunWarm = new THREE.Color(0xff6022);
      const sunCool = new THREE.Color(0xfff1c4);
      sun.color.copy(sunWarm).lerp(sunCool, dayW);
      sun.intensity = 0.06 + 1.85 * dayW;

      ambient.intensity = 0.22 + 0.65 * dayW + 0.04 * nightW;
      ambient.color.setHSL(0.10, 0.5, 0.4 + 0.30 * dayW);
      ambient.groundColor.setHSL(0.06, 0.6, 0.10 + 0.20 * dayW);

      // Fog tint = sky horizon palette (component-wise mix).
      const fogCol = new THREE.Color(
        horizonDay.r * dayW + horizonDusk.r * duskW + horizonNight.r * nightW,
        horizonDay.g * dayW + horizonDusk.g * duskW + horizonNight.g * nightW,
        horizonDay.b * dayW + horizonDusk.b * duskW + horizonNight.b * nightW,
      );
      (scene.fog as THREE.FogExp2).color.copy(fogCol);
      (scene.fog as THREE.FogExp2).density = 0.0020 + 0.0012 * (1 - dayW);
      renderer.setClearColor(fogCol, 1);

      mtnMaterial.uniforms.uDayW.value   = dayW;
      mtnMaterial.uniforms.uDuskW.value  = duskW;
      mtnMaterial.uniforms.uNightW.value = nightW;

      sandUniforms.uSunColor.value.copy(sun.color).multiplyScalar(0.6 + dayW * 0.7);
      // Sand gets cooler/violet at night, hot at dusk.
      sandUniforms.uAmbient.value.setRGB(
        0.20 + 0.30 * dayW + 0.18 * duskW,
        0.16 + 0.30 * dayW + 0.10 * duskW,
        0.16 + 0.20 * dayW,
      );

      skyUniforms.uTimeOfDay.value = tod;

      if (sandPoints) {
        const m = sandPoints.material as THREE.ShaderMaterial;
        m.uniforms.uIntensity.value = 0.25 + 0.55 * dayW;
      }

      if (mirageMesh) {
        const m = mirageMesh.material as THREE.ShaderMaterial;
        // Mirage only when sun is genuinely high — peaks ~tod=0.25.
        m.uniforms.uIntensity.value = THREE.MathUtils.smoothstep(dir.y, 0.5, 0.95);
      }
    };

    // ─── Animation loop ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    let bobApplied = 0;

    const animate = () => {
      const elapsed = clock.getElapsedTime();
      sandUniforms.uTime.value = elapsed;

      const tod = dayLengthSeconds > 0
        ? (elapsed / dayLengthSeconds) % 1
        : fixedTimeOfDay;
      updateTimeOfDay(tod);

      // Re-aim the mirage strip to stay perpendicular to the camera azimuth
      // so it always sits at the visible horizon.
      if (mirageMesh) {
        const az = Math.atan2(camera.position.x, camera.position.z);
        mirageMesh.position.set(
          -Math.sin(az) * 1100,
          18,
          -Math.cos(az) * 1100,
        );
        mirageMesh.rotation.y = az;
      }

      camera.position.y -= bobApplied;
      controls.update();
      bobApplied = autoRotate
        ? Math.sin(elapsed * 0.45) * 0.6 + Math.sin(elapsed * 0.21 + 1.0) * 0.35
        : 0;
      camera.position.y += bobApplied;

      composer.render();
      raf = requestAnimationFrame(animate);
    };

    updateTimeOfDay(fixedTimeOfDay);
    animate();

    // ─── Resize ────────────────────────────────────────────────────────────
    const onResize = () => {
      const w = container.clientWidth;
      const h = container.clientHeight;
      if (w === 0 || h === 0) return;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
      composer.setSize(w, h);
    };
    const ro = new ResizeObserver(onResize);
    ro.observe(container);

    // ─── Cleanup ───────────────────────────────────────────────────────────
    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      controls.dispose();
      composer.dispose();
      fieldGeo.dispose();
      sandMaterial.dispose();
      mtnGeo.dispose();
      mtnMaterial.dispose();
      sky.geometry.dispose();
      skyMaterial.dispose();
      if (sandPoints) {
        sandPoints.geometry.dispose();
        (sandPoints.material as THREE.Material).dispose();
      }
      if (mirageMesh) {
        mirageMesh.geometry.dispose();
        (mirageMesh.material as THREE.Material).dispose();
      }
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [
    width,
    height,
    fieldSize,
    fieldSegments,
    windDirRad,
    dayLengthSeconds,
    fixedTimeOfDay,
    autoRotate,
    sandParticles,
    mirage,
  ]);

  return <Box ref={containerRef} sx={{ width: '100%', height: '100%', overflow: 'hidden' }} />;
};

export default SandDunes;
