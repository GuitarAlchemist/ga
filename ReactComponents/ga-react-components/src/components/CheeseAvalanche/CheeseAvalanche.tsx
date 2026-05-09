/**
 * Cheese Avalanche — apéricube + babybel tumbling down a mountain.
 *
 * A cheerful French snack-food physics sim. Two body types:
 *
 *  - Apéricubes — small foil-wrapped cubes in primary colors (red, yellow,
 *    blue, green, gold, silver, white).
 *  - Babybels — small wheels of red-wax cheese with a thin equatorial rim.
 *
 * They rain onto a noisy conical mountain and tumble down all sides under
 * gravity, with heightfield collision and rolling friction. When a body
 * falls past the bottom it respawns at the apex so the avalanche is
 * continuous. Camera orbits the mountain; bloom + day-cycle reused from
 * the FluffyGrass / SandDunes idiom.
 *
 * The physics is intentionally hand-rolled (no cannon / rapier dep): ~300
 * bodies × heightfield-only collision + tangent-friction rolling, which
 * fits comfortably in 60fps on modest hardware.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { Box } from '@mui/material';

export interface CheeseAvalancheProps {
  /** Total bodies to simulate. Cubes get 2/3 of the budget by default. */
  bodyCount?: number;
  /** Fraction in [0,1] of bodies that are babybel wheels (rest are apéricubes). */
  babybelFraction?: number;
  /** Mountain peak height in world units. */
  mountainHeight?: number;
  /** Mountain base radius. */
  mountainRadius?: number;
  /** m/s² downward acceleration. */
  gravity?: number;
  /** Auto-orbit the camera. */
  autoRotate?: boolean;
  /** Cycle the sky/sun across `dayLengthSeconds`. 0 → freeze at fixedTimeOfDay. */
  dayLengthSeconds?: number;
  /** When dayLengthSeconds=0, the constant time-of-day in [0,1). */
  fixedTimeOfDay?: number;
}

// Apéricube foil palette — chosen to read clearly against the green
// mountain at any time of day.
const FOIL_COLORS: number[] = [
  0xc62828, // red
  0xffd54f, // yellow
  0x1976d2, // blue
  0x43a047, // green
  0xffb300, // gold
  0xeeeeee, // silver/white
  0xff7043, // orange
];

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

// CPU mirror of the noise so terrain heights and collisions agree exactly.
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

interface Body {
  pos: THREE.Vector3;
  vel: THREE.Vector3;
  rot: THREE.Quaternion;
  angVel: THREE.Vector3;
  type: 'cube' | 'wheel';
  slot: number;     // index inside its InstancedMesh
  radius: number;   // bounding sphere radius
}

const CheeseAvalanche: React.FC<CheeseAvalancheProps> = ({
  bodyCount = 360,
  babybelFraction = 0.35,
  mountainHeight = 28,
  mountainRadius = 32,
  gravity = 28,
  autoRotate = true,
  dayLengthSeconds = 90,
  fixedTimeOfDay = 0.22,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W = container.clientWidth || 1280;
    const H = container.clientHeight || 720;

    // ─── Scene + Camera + Renderer ─────────────────────────────────────────
    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0xa8c8e8, 0.005);

    const camera = new THREE.PerspectiveCamera(55, W / H, 0.1, 1000);
    camera.position.set(70, 38, 70);
    camera.lookAt(0, 8, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W, H);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.minDistance = 12;
    controls.maxDistance = 200;
    controls.maxPolarAngle = Math.PI * 0.49;
    controls.target.set(0, 6, 0);
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 0.3;

    // ─── Sky dome (reused idiom from FluffyGrass) ──────────────────────────
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
        vec3 skyDay(float h)   { return mix(vec3(0.55,0.78,0.95), vec3(0.16,0.42,0.78), pow(clamp(h,0.0,1.0), 0.7)); }
        vec3 skyDusk(float h)  { return mix(vec3(0.96,0.55,0.30), vec3(0.20,0.10,0.32), pow(clamp(h,0.0,1.0), 0.6)); }
        vec3 skyNight(float h) { return mix(vec3(0.04,0.05,0.13), vec3(0.005,0.01,0.05), pow(clamp(h,0.0,1.0), 0.7)); }
        void main() {
          vec3 d = normalize(vDir);
          float h = clamp(d.y, 0.0, 1.0);
          float dayW   = smoothstep(-0.05, 0.30, uSunDir.y);
          float duskW  = exp(-pow(uSunDir.y * 4.0, 2.0));
          float nightW = smoothstep(0.05, -0.10, uSunDir.y);
          vec3 col = skyDay(h)*dayW + skyDusk(h)*duskW + skyNight(h)*nightW;
          float sunDot = clamp(dot(d, normalize(uSunDir)), 0.0, 1.0);
          float disc = smoothstep(0.9985, 0.9999, sunDot);
          float glow = pow(sunDot, 24.0) * 0.35 + pow(sunDot, 4.0) * 0.10;
          vec3 sunCol = mix(vec3(1.0, 0.55, 0.25), vec3(1.0, 0.95, 0.85), dayW);
          col += sunCol * disc * 6.0 + sunCol * glow * (0.6 + duskW * 1.6);
          float starN = fract(sin(dot(d.xz * 600.0, vec2(12.989, 78.233))) * 43758.55);
          col += vec3(smoothstep(0.9965, 0.9990, starN) * nightW * smoothstep(0.05, 0.4, h));
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(500, 32, 16), skyMaterial);
    scene.add(sky);

    // ─── Lights ────────────────────────────────────────────────────────────
    const ambient = new THREE.HemisphereLight(0xa7c4ff, 0x2a1f10, 0.6);
    scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xfff1d8, 1.4);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.left = -60;
    sun.shadow.camera.right = 60;
    sun.shadow.camera.top = 60;
    sun.shadow.camera.bottom = -60;
    sun.shadow.camera.near = 0.5;
    sun.shadow.camera.far = 250;
    sun.shadow.bias = -0.0006;
    scene.add(sun);
    scene.add(sun.target);

    // ─── Mountain ──────────────────────────────────────────────────────────
    // Cone profile + noise riding on top. Exactly the same function on CPU
    // (for collision) and GPU (for terrain mesh).
    const mountainAt = (x: number, z: number): number => {
      const r = Math.sqrt(x * x + z * z);
      const cone = Math.max(0, mountainHeight * (1 - r / mountainRadius));
      if (cone <= 0) return 0;
      const nA = cpuVnoise(x * 0.10, z * 0.10);
      const nB = cpuVnoise(x * 0.30, z * 0.30) * 0.45;
      // Scale noise so it mostly affects the upper half — adds bumps that
      // make the avalanche tumble unpredictably without cratering the base.
      const noiseAmp = (cone / mountainHeight) * 1.4;
      return cone + (nA + nB) * noiseAmp;
    };

    const mountainNormal = (x: number, z: number): THREE.Vector3 => {
      const eps = 0.4;
      const hx = mountainAt(x + eps, z) - mountainAt(x - eps, z);
      const hz = mountainAt(x, z + eps) - mountainAt(x, z - eps);
      return new THREE.Vector3(-hx, 2 * eps, -hz).normalize();
    };

    const fieldSize = mountainRadius * 2.6;
    const fieldGeo = new THREE.PlaneGeometry(fieldSize, fieldSize, 192, 192);
    fieldGeo.rotateX(-Math.PI / 2);
    {
      const pos = fieldGeo.attributes.position as THREE.BufferAttribute;
      for (let i = 0; i < pos.count; i++) {
        pos.setY(i, mountainAt(pos.getX(i), pos.getZ(i)));
      }
      pos.needsUpdate = true;
      fieldGeo.computeVertexNormals();
    }

    const mountainMaterial = new THREE.ShaderMaterial({
      uniforms: {
        uGrass: { value: new THREE.Color(0x4a7c2e) },
        uRock:  { value: new THREE.Color(0x6e6256) },
        uSnow:  { value: new THREE.Color(0xf2eeea) },
        uSunDir: skyUniforms.uSunDir,
        uSunColor: { value: new THREE.Color(0xfff1d8) },
        uAmbient:  { value: new THREE.Color(0x506890) },
        uPeakY: { value: mountainHeight },
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
        uniform vec3 uGrass, uRock, uSnow;
        uniform vec3 uSunDir, uSunColor, uAmbient;
        uniform float uPeakY;
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}
        void main() {
          float slope = 1.0 - clamp(vNormal.y, 0.0, 1.0);
          float h = clamp(vWorld.y / uPeakY, 0.0, 1.0);
          float patchy = vnoise(vWorld.xz * 0.18) * 0.5 + 0.5;

          // Layered biome: grass at the base → rock on cliffs → snow at peak.
          vec3 col = mix(uGrass, uRock, smoothstep(0.30, 0.7, slope) + h * 0.25);
          col = mix(col, uSnow, smoothstep(0.78, 0.95, h));
          col *= 0.85 + patchy * 0.30;

          float lit = max(dot(vNormal, normalize(uSunDir)), 0.0);
          col = col * (uAmbient * 0.4 + uSunColor * (lit * 0.7 + 0.4));
          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    const mountain = new THREE.Mesh(fieldGeo, mountainMaterial);
    mountain.receiveShadow = true;
    scene.add(mountain);

    // ─── Bodies (apéricube cubes + babybel wheels) ─────────────────────────
    const wheelCount = Math.round(bodyCount * babybelFraction);
    const cubeCount  = bodyCount - wheelCount;

    const cubeSize    = 0.7;
    const wheelRadius = 0.85;
    const wheelHeight = 0.45;

    // Cube material — slightly metallic foil, instanceColor for variety.
    const cubeGeo = new THREE.BoxGeometry(cubeSize, cubeSize, cubeSize);
    const cubeMat = new THREE.MeshStandardMaterial({
      metalness: 0.55,
      roughness: 0.35,
      vertexColors: false,
    });
    const cubeMesh = new THREE.InstancedMesh(cubeGeo, cubeMat, cubeCount);
    cubeMesh.castShadow = true;
    cubeMesh.receiveShadow = true;
    // instanceColor — three's MeshStandardMaterial picks this up automatically.
    const cubeColors = new Float32Array(cubeCount * 3);
    for (let i = 0; i < cubeCount; i++) {
      const c = new THREE.Color(FOIL_COLORS[Math.floor(Math.random() * FOIL_COLORS.length)]);
      cubeColors[i * 3 + 0] = c.r;
      cubeColors[i * 3 + 1] = c.g;
      cubeColors[i * 3 + 2] = c.b;
    }
    cubeMesh.instanceColor = new THREE.InstancedBufferAttribute(cubeColors, 3);
    scene.add(cubeMesh);

    // Wheel material — red wax with a slightly darker rim via shader tweak.
    const wheelGeo = new THREE.CylinderGeometry(wheelRadius, wheelRadius, wheelHeight, 28);
    const wheelMat = new THREE.MeshStandardMaterial({
      color: 0xc62828,
      metalness: 0.10,
      roughness: 0.55,
    });
    const wheelMesh = new THREE.InstancedMesh(wheelGeo, wheelMat, wheelCount);
    wheelMesh.castShadow = true;
    wheelMesh.receiveShadow = true;
    scene.add(wheelMesh);

    // Spawn parameters: bodies materialise just above the apex with a small
    // outward toss so they land somewhere on the slope rather than straight
    // down the same path each time.
    const spawnY = mountainHeight + 6;
    const spawnRadius = 1.2;

    const bodies: Body[] = [];
    for (let i = 0; i < cubeCount; i++) {
      bodies.push(makeBody('cube', i, cubeSize * 0.7, spawnY, spawnRadius));
    }
    for (let i = 0; i < wheelCount; i++) {
      bodies.push(makeBody('wheel', i, wheelRadius, spawnY, spawnRadius));
    }

    function makeBody(
      type: 'cube' | 'wheel',
      slot: number,
      radius: number,
      spawnYBase: number,
      spawnR: number,
    ): Body {
      const a = Math.random() * Math.PI * 2;
      const r = Math.random() * spawnR;
      const pos = new THREE.Vector3(
        Math.cos(a) * r,
        spawnYBase + Math.random() * 8,
        Math.sin(a) * r,
      );
      const v = new THREE.Vector3(
        (Math.random() - 0.5) * 4,
        -2 - Math.random() * 2,
        (Math.random() - 0.5) * 4,
      );
      const rot = new THREE.Quaternion();
      rot.setFromEuler(new THREE.Euler(
        Math.random() * Math.PI,
        Math.random() * Math.PI,
        Math.random() * Math.PI,
      ));
      return {
        pos,
        vel: v,
        rot,
        angVel: new THREE.Vector3(
          (Math.random() - 0.5) * 4,
          (Math.random() - 0.5) * 4,
          (Math.random() - 0.5) * 4,
        ),
        type,
        slot,
        radius,
      };
    }

    // ─── Bloom ─────────────────────────────────────────────────────────────
    const composer = new EffectComposer(renderer);
    composer.setSize(W, H);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    composer.addPass(new RenderPass(scene, camera));
    composer.addPass(new UnrealBloomPass(new THREE.Vector2(W, H), 0.45, 0.45, 0.92));
    composer.addPass(new OutputPass());

    // ─── Time-of-day driver ────────────────────────────────────────────────
    const updateTimeOfDay = (tod: number) => {
      const angle = tod * Math.PI * 2;
      const dir = new THREE.Vector3(Math.cos(angle), Math.sin(angle), 0.25).normalize();
      skyUniforms.uSunDir.value.copy(dir);
      sun.position.copy(dir).multiplyScalar(120);
      sun.target.position.set(0, 0, 0);

      const dayW   = THREE.MathUtils.smoothstep(dir.y, -0.05, 0.30);
      const duskW  = Math.exp(-Math.pow(dir.y * 4.0, 2.0));
      const nightW = THREE.MathUtils.smoothstep(-dir.y, -0.05, 0.10);

      const sunWarm = new THREE.Color(0xff8a3c);
      const sunCool = new THREE.Color(0xfff1d8);
      sun.color.copy(sunWarm).lerp(sunCool, dayW);
      sun.intensity = 0.05 + 1.55 * dayW;

      ambient.intensity = 0.18 + 0.55 * dayW + 0.05 * nightW;
      ambient.color.setHSL(0.6, 0.5, 0.4 + 0.25 * dayW);
      ambient.groundColor.setHSL(0.08, 0.4, 0.05 + 0.10 * dayW);

      const horizonDay   = new THREE.Color(0.55, 0.78, 0.95);
      const horizonDusk  = new THREE.Color(0.96, 0.55, 0.30);
      const horizonNight = new THREE.Color(0.04, 0.05, 0.13);
      const fogCol = new THREE.Color(
        horizonDay.r * dayW + horizonDusk.r * duskW + horizonNight.r * nightW,
        horizonDay.g * dayW + horizonDusk.g * duskW + horizonNight.g * nightW,
        horizonDay.b * dayW + horizonDusk.b * duskW + horizonNight.b * nightW,
      );
      (scene.fog as THREE.FogExp2).color.copy(fogCol);
      renderer.setClearColor(fogCol, 1);

      mountainMaterial.uniforms.uSunColor.value.copy(sun.color).multiplyScalar(0.7 + dayW * 0.6);
      mountainMaterial.uniforms.uAmbient.value.set(
        0.15 + 0.10 * dayW,
        0.20 + 0.18 * dayW,
        0.30 + 0.18 * dayW,
      );
      skyUniforms.uTimeOfDay.value = tod;
    };

    // ─── Physics step + rendering loop ─────────────────────────────────────
    const tmpDummy = new THREE.Object3D();
    const tmpEuler = new THREE.Euler();
    const tmpQuat  = new THREE.Quaternion();
    const tmpV     = new THREE.Vector3();
    const tmpV2    = new THREE.Vector3();

    const physicsStep = (dt: number) => {
      const dtClamped = Math.min(dt, 1 / 30);
      for (const b of bodies) {
        // Integrate.
        b.vel.y -= gravity * dtClamped;
        b.pos.addScaledVector(b.vel, dtClamped);

        // Angular update — small euler integration.
        tmpEuler.set(
          b.angVel.x * dtClamped,
          b.angVel.y * dtClamped,
          b.angVel.z * dtClamped,
        );
        tmpQuat.setFromEuler(tmpEuler);
        b.rot.premultiply(tmpQuat).normalize();

        // Heightfield collision.
        const groundY = mountainAt(b.pos.x, b.pos.z);
        const bottom = b.pos.y - b.radius;
        if (bottom < groundY) {
          // Push out.
          b.pos.y = groundY + b.radius;
          const n = mountainNormal(b.pos.x, b.pos.z);
          // Reflect normal velocity component (restitution 0.45).
          const vn = b.vel.dot(n);
          if (vn < 0) {
            b.vel.addScaledVector(n, -vn * 1.45);
          }
          // Tangent friction → linear damp + induced spin.
          tmpV.copy(b.vel);
          tmpV2.copy(n).multiplyScalar(b.vel.dot(n));
          tmpV.sub(tmpV2); // tangent component
          const tSpeed = tmpV.length();
          if (tSpeed > 0.05) {
            const spinAxis = new THREE.Vector3().crossVectors(n, tmpV).normalize();
            // Wheels spin much more readily than cubes — they're round.
            const spinGain = b.type === 'wheel' ? 1.2 : 0.45;
            b.angVel.copy(spinAxis).multiplyScalar(tSpeed / b.radius * spinGain);
          }
          // Friction multiplier — wheels keep more energy.
          const friction = b.type === 'wheel' ? 0.92 : 0.78;
          b.vel.multiplyScalar(friction);
        }

        // Out of bounds → respawn at apex.
        const r = Math.sqrt(b.pos.x * b.pos.x + b.pos.z * b.pos.z);
        if (b.pos.y < -8 || r > mountainRadius * 1.6 || (r > mountainRadius * 1.1 && b.pos.y < 1)) {
          const reset = makeBody(b.type, b.slot, b.radius, spawnY, spawnRadius);
          b.pos.copy(reset.pos);
          b.vel.copy(reset.vel);
          b.rot.copy(reset.rot);
          b.angVel.copy(reset.angVel);
        }
      }

      // Push transforms to instanced meshes.
      for (const b of bodies) {
        tmpDummy.position.copy(b.pos);
        tmpDummy.quaternion.copy(b.rot);
        if (b.type === 'wheel') {
          // Wheel cylinder is upright by default; it tumbles with rot.
          tmpDummy.scale.set(1, 1, 1);
          tmpDummy.updateMatrix();
          wheelMesh.setMatrixAt(b.slot, tmpDummy.matrix);
        } else {
          tmpDummy.scale.set(1, 1, 1);
          tmpDummy.updateMatrix();
          cubeMesh.setMatrixAt(b.slot, tmpDummy.matrix);
        }
      }
      cubeMesh.instanceMatrix.needsUpdate = true;
      wheelMesh.instanceMatrix.needsUpdate = true;
    };

    // ─── Animation loop ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    let bobApplied = 0;

    const animate = () => {
      const dt = clock.getDelta();
      const elapsed = clock.elapsedTime;

      const tod = dayLengthSeconds > 0
        ? (elapsed / dayLengthSeconds) % 1
        : fixedTimeOfDay;
      updateTimeOfDay(tod);

      physicsStep(dt);

      camera.position.y -= bobApplied;
      controls.update();
      bobApplied = autoRotate
        ? Math.sin(elapsed * 0.45) * 0.4 + Math.sin(elapsed * 0.21 + 1.0) * 0.22
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

    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      controls.dispose();
      composer.dispose();
      fieldGeo.dispose();
      mountainMaterial.dispose();
      cubeGeo.dispose();
      cubeMat.dispose();
      wheelGeo.dispose();
      wheelMat.dispose();
      sky.geometry.dispose();
      skyMaterial.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [
    bodyCount,
    babybelFraction,
    mountainHeight,
    mountainRadius,
    gravity,
    autoRotate,
    dayLengthSeconds,
    fixedTimeOfDay,
  ]);

  return <Box ref={containerRef} sx={{ width: '100%', height: '100%', overflow: 'hidden' }} />;
};

export default CheeseAvalanche;
