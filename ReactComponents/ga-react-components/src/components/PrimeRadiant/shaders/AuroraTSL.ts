// src/components/PrimeRadiant/shaders/AuroraTSL.ts
// Aurora borealis flowing curtains for polar torus geometry.
// 1D noise-modulated curtains with green-purple color mix.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3,
  uv, time,
  sin, pow, abs, mix, fract, floor,
} from 'three/tsl';

// ── Inline 1D noise: hash-based with linear interpolation ──

const hash1 = Fn(([p]: [ReturnType<typeof float>]) => {
  return fract(sin(p.mul(127.1)).mul(43758.5453));
});

const noise1d = Fn(([p_immutable]: [ReturnType<typeof float>]) => {
  const p = float(p_immutable);
  const i = floor(p);
  const f = fract(p);
  // Smoothstep interpolation
  const u = f.mul(f).mul(float(3.0).sub(f.mul(2.0)));
  return mix(hash1(i), hash1(i.add(1.0)), u);
});

/**
 * Create an aurora borealis curtain material.
 * Designed for a polar torus mesh — flowing green-purple curtains
 * modulated by 1D noise with vertical fade.
 */
export function createAuroraMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();

  material.colorNode = Fn(() => {
    const uvCoord = uv();

    // Curtain wave
    const curtainWave = sin(uvCoord.x.mul(20.0).add(time.mul(2.0))).mul(0.5).add(0.5);
    const curtainNoise = noise1d(uvCoord.x.mul(30.0).add(time.mul(1.5)));
    const curtain = curtainWave.mul(curtainNoise);

    // Vertical fade — peaks at center (uv.y = 0.5), falls off to edges
    const vFade = pow(float(1.0).sub(abs(uvCoord.y.sub(0.5)).mul(2.0)), float(0.8));

    // Green-purple color mix
    const green = vec3(0.1, 0.9, 0.3);
    const purple = vec3(0.5, 0.1, 0.8);
    const colorMix = sin(uvCoord.x.mul(8.0).add(time.mul(0.7))).mul(0.5).add(0.5);
    const col = mix(green, purple, colorMix.mul(0.4));

    return col;
  })();

  material.opacityNode = Fn(() => {
    const uvCoord = uv();

    const curtainWave = sin(uvCoord.x.mul(20.0).add(time.mul(2.0))).mul(0.5).add(0.5);
    const curtainNoise = noise1d(uvCoord.x.mul(30.0).add(time.mul(1.5)));
    const curtain = curtainWave.mul(curtainNoise);

    const vFade = pow(float(1.0).sub(abs(uvCoord.y.sub(0.5)).mul(2.0)), float(0.8));

    return curtain.mul(vFade).mul(0.5);
  })();

  material.transparent = true;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;
  material.side = THREE.DoubleSide;

  return material;
}
