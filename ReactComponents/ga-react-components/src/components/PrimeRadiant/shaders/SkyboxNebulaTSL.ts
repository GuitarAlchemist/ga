// src/components/PrimeRadiant/shaders/SkyboxNebulaTSL.ts
// TSL deep space nebula skybox — procedural background sphere
// Orion-like and Carina-like nebulae with hash-based starfield.

import { MeshBasicNodeMaterial } from 'three/webgpu';
import * as THREE from 'three';
import {
  Fn, vec3,
  positionLocal,
  mix, smoothstep, step,
  normalize, length,
} from 'three/tsl';
import { fbm6, hash3 } from './TSLNoiseLib';

/**
 * Create a TSL-based deep space nebula skybox material.
 * Renders on BackSide of a large sphere. All procedural, no textures.
 */
export function createSkyboxNebulaMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.side = THREE.BackSide;
  material.depthWrite = false;

  // Simple deep-space gradient for now. The original FBM-based nebula was
  // rendering as a bright cellular dome under WebGPU AdditiveBlending — needs
  // further investigation. This minimal version returns to clean deep space.
  material.colorNode = Fn(() => {
    const dir = normalize(positionLocal);
    return mix(
      vec3(0.002, 0.002, 0.008),
      vec3(0.005, 0.003, 0.012),
      dir.y.mul(0.5).add(0.5),
    );
  })();

  // Do NOT use AdditiveBlending on a full-coverage BackSide sphere — under
  // WebGPU it stacks with other additive layers and dominates the view.
  // Normal blending on an opaque base gives clean deep-space behind everything.
  material.transparent = false;

  return material;
}
