// src/components/PrimeRadiant/shaders/RingGlowTSL.ts
// TSL ring glow material — time-animated golden glow with edge fade.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3,
  uv, positionLocal,
  sin, smoothstep, time,
} from 'three/tsl';

/**
 * Create a TSL ring glow material for Saturn's ring overlay.
 * Golden glow with pulsing animation and inner/outer edge fade.
 * Replaces the inline GLSL ring glow ShaderMaterial in SolarSystem.ts.
 */
export function createRingGlowMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();

  // Golden glow color
  material.colorNode = vec3(1.0, 0.85, 0.3);

  // Opacity: edge fade * pulse
  material.opacityNode = Fn(() => {
    const uvCoord = uv();
    const pulse = float(0.6).add(sin(time.mul(1.2)).mul(0.4));
    const edgeFade = smoothstep(float(0.0), float(0.3), uvCoord.x)
      .mul(smoothstep(float(1.0), float(0.7), uvCoord.x));
    return edgeFade.mul(pulse).mul(0.35);
  })();

  material.transparent = true;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;
  material.side = THREE.DoubleSide;

  return material;
}
