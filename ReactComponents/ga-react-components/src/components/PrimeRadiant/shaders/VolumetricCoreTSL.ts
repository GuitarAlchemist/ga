// src/components/PrimeRadiant/shaders/VolumetricCoreTSL.ts
// TSL volumetric governance node core — fractal FBM glow
// Used by ForceRadiant for governance graph nodes.

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3,
  uv, time, mix, positionLocal, normalWorld,
  cameraPosition, positionWorld,
  pow, abs, smoothstep, clamp,
} from 'three/tsl';
import { fbm3, fbm6 } from './TSLNoiseLib';

export interface VolumetricCoreOptions {
  color: THREE.Color;
  complexity?: number; // 1-8, controls FBM octaves
  intensity?: number;
}

/**
 * Create a TSL-based volumetric core material for governance graph nodes.
 * Fractal FBM glow with fresnel rim and swirl layers.
 */
export function createVolumetricCoreMaterialTSL(options: VolumetricCoreOptions): MeshBasicNodeMaterial {
  const {
    color,
    complexity = 5,
    intensity = 1.0,
  } = options;

  const material = new MeshBasicNodeMaterial();
  material.transparent = true;
  material.blending = THREE.AdditiveBlending;
  material.depthWrite = false;
  material.side = THREE.FrontSide;

  // Select FBM based on complexity tier (compile-time branching)
  const fbm = complexity <= 2 ? fbm3 : fbm6;

  const uColor = vec3(color.r, color.g, color.b);
  const uIntensity = float(intensity);
  const t = time.mul(0.15);

  // Shared sub-function: compute fresnel + pattern (used by both color and opacity)
  const computeFields = Fn(() => {
    const pos = positionLocal;
    const uvCoord = uv();

    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const fresnel = pow(float(1.0).sub(abs(normalWorld.dot(viewDir))), float(2.0));

    const fractalNoise = fbm(
      pos.mul(3.0).add(vec3(t, t.mul(0.7), t.negate().mul(0.5))),
    );
    const swirl = fbm(
      pos.mul(6.9).add(vec3(t.negate().mul(0.3), t.mul(0.5), t.mul(0.8))),
    );

    const center = uvCoord.sub(vec3(0.5, 0.5, 0.0).xy);
    const dist = center.length();
    const coreBright = smoothstep(0.5, 0.0, dist);

    const pattern = mix(fractalNoise, swirl, 0.4).mul(coreBright);
    return { fresnel, coreBright, pattern };
  });

  // Pre-multiply color by alpha for guaranteed AdditiveBlending behavior
  // (opacityNode alone was not being honored for this combination under WebGPU,
  // causing cores to render at full brightness as huge radial starbursts).
  material.colorNode = Fn(() => {
    const { fresnel, coreBright, pattern } = computeFields();
    const col = uColor.mul(float(0.3).add(pattern.mul(1.5)))
      .add(uColor.mul(fresnel).mul(0.8))
      .add(vec3(1.0, 1.0, 1.0).mul(coreBright).mul(0.15));
    const alpha = clamp(
      coreBright.mul(0.8).add(fresnel.mul(0.6)).add(pattern.mul(0.3)),
      0.0,
      1.0,
    ).mul(uIntensity);
    return col.mul(alpha);
  })();

  return material;
}
