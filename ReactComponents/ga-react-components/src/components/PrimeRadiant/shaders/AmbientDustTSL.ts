// src/components/PrimeRadiant/shaders/AmbientDustTSL.ts
// TSL GPU-driven ambient dust particle system.
// All motion computed in vertex shader -- zero CPU position updates per frame.
//
// Replaces the CPU dust in ForceRadiant.tsx (~lines 2509-2561, 2282-2299):
//   import { createAmbientDust } from './shaders/AmbientDustTSL';
//   const dust = createAmbientDust(2000, 200);
//   scene.add(dust.points);
//   // In animation loop: dust.update(time);
//   // On cleanup: dust.dispose();
//
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { PointsNodeMaterial } from 'three/webgpu';
import {
  Fn, attribute, uniform, float, vec2, vec3,
  sin, cos, positionLocal, mul, add, mod,
  dot, sqrt, smoothstep, pointUV,
} from 'three/tsl';

// ── Types ──

export interface AmbientDustHandle {
  points: THREE.Points;
  /** Call each frame with elapsed time (seconds). Only updates a single uniform. */
  update(time: number): void;
  dispose(): void;
}

// ── Factory ──

/**
 * GPU-driven ambient dust particles using TSL PointsNodeMaterial.
 * All motion computed in vertex shader -- zero CPU position updates per frame.
 *
 * Particles drift in Brownian-like paths using layered sine waves with
 * per-particle phase offsets derived from the seed attribute.
 *
 * @param count  Number of dust particles (default 2000)
 * @param range  Half-extent of the cubic volume (default 200)
 */
export function createAmbientDust(count: number = 2000, range: number = 200): AmbientDustHandle {
  const geometry = new THREE.BufferGeometry();

  // Initial random positions + per-particle phase seeds + colors
  const positions = new Float32Array(count * 3);
  const seeds = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);

  for (let i = 0; i < count; i++) {
    const i3 = i * 3;
    positions[i3]     = (Math.random() - 0.5) * range * 2;
    positions[i3 + 1] = (Math.random() - 0.5) * range * 2;
    positions[i3 + 2] = (Math.random() - 0.5) * range * 2;

    seeds[i3]     = Math.random() * 100;
    seeds[i3 + 1] = Math.random() * 100;
    seeds[i3 + 2] = Math.random() * 100;

    // Color distribution: warm gold (30%), cool cyan (30%), violet (40%)
    const r = Math.random();
    if (r < 0.3) {
      colors[i3] = 1.0; colors[i3 + 1] = 0.85; colors[i3 + 2] = 0.4;   // gold
    } else if (r < 0.6) {
      colors[i3] = 0.4; colors[i3 + 1] = 0.9;  colors[i3 + 2] = 1.0;   // cyan
    } else {
      colors[i3] = 0.7; colors[i3 + 1] = 0.4;  colors[i3 + 2] = 1.0;   // violet
    }
  }

  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  // Single uniform updated per frame (the only CPU work)
  const uTime = uniform(0.0);
  const uRange = uniform(range);

  // Per-particle seed attribute for phase offsets
  const seed = attribute('aSeed', 'vec3');
  const col = attribute('color', 'vec3');

  // ── Material ──

  const material = new PointsNodeMaterial();
  material.transparent = true;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;
  material.sizeAttenuation = true;

  // ── Size ──
  material.sizeNode = float(3.0);

  // ── Vertex: Brownian drift via layered sine waves ──

  material.positionNode = Fn(() => {
    const pos = positionLocal.toVar();
    const drift = vec3(
      sin(mul(uTime, float(0.15)).add(seed.x).mul(float(2.3))),
      cos(mul(uTime, float(0.12)).add(seed.y).mul(float(1.7))),
      sin(mul(uTime, float(0.18)).add(seed.z).mul(float(3.1))),
    ).mul(float(0.5));
    // Wrap around range boundaries so particles cycle seamlessly
    const rangeDouble = mul(uRange, float(2.0));
    return mod(add(pos, drift).add(uRange), rangeDouble).sub(uRange);
  })();

  // ── Color: per-particle from vertex attribute ──

  material.colorNode = Fn(() => {
    return col;
  })();

  // ── Opacity: soft circular disc with glow falloff ──

  material.opacityNode = Fn(() => {
    // pointUV is vec2 [0,1] for the point sprite quad
    const c = pointUV.sub(vec2(0.5, 0.5));
    const dist = dot(c, c);
    // Soft circular falloff: smoothstep from edge (0.5) to center (0.0)
    const discAlpha = smoothstep(float(0.5), float(0.1), sqrt(dist));
    return discAlpha.mul(float(0.35));
  })();

  // ── Mesh ──

  const points = new THREE.Points(geometry, material);
  points.name = 'ambient-dust-tsl';
  points.renderOrder = 1;
  points.frustumCulled = false;

  return {
    points,
    update(time: number) {
      uTime.value = time;
    },
    dispose() {
      geometry.dispose();
      material.dispose();
    },
  };
}
