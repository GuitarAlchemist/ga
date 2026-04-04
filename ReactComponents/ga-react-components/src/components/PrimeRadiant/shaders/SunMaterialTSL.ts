// src/components/PrimeRadiant/shaders/SunMaterialTSL.ts
// TSL (Three.js Shading Language) procedural sun material
// Uses shared noise library — composable TypeScript nodes
// Works with both WebGPU and WebGL renderers (auto-compiles to GLSL fallback)

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3,
  texture, uv, time,
  mix, positionLocal, normalWorld,
  cameraPosition, positionWorld,
  sin, pow, abs, smoothstep,
} from 'three/tsl';
import { noise3, fbm6, fbm3 } from './TSLNoiseLib';
import type { QualityTier } from './TSLUniforms';

export interface SunMaterialOptions {
  sunTexture: THREE.Texture;
  quality?: QualityTier;
}

/**
 * Create a TSL-based procedural sun material.
 * Fully emissive (no lighting) with animated plasma, convection, sunspots,
 * prominences, and edge glow. Quality-adaptive FBM octaves.
 */
export function createSunMaterialTSL(options: SunMaterialOptions): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  const { sunTexture, quality = 'high' } = options;
  const fbm = quality === 'low' ? fbm3 : fbm6;

  sunTexture.colorSpace = THREE.SRGBColorSpace;
  sunTexture.minFilter = THREE.LinearMipmapLinearFilter;
  sunTexture.magFilter = THREE.LinearFilter;

  const sunTex = texture(sunTexture);
  const t = time.mul(0.08);

  material.colorNode = Fn(() => {
    const pos = positionLocal;
    const uvCoord = uv();

    // Base texture color
    const baseTex = sunTex.sample(uvCoord).rgb;

    // Multi-scale convection (solar granulation)
    const fineGran = fbm(pos.mul(18.0).add(vec3(t.mul(0.4), t.mul(0.3), t.mul(0.35))));
    const medGran = fbm(pos.mul(6.0).add(vec3(t.mul(0.15), t.negate().mul(0.1), t.mul(0.12))));
    const flow = fbm(pos.mul(2.0).add(vec3(t.mul(0.05), t.mul(0.03), t.negate().mul(0.04))));

    // Sunspots
    const spotNoise = fbm(pos.mul(4.5).add(vec3(t.mul(0.02), t.mul(0.015), t.negate().mul(0.01))));
    const spots = smoothstep(0.58, 0.68, spotNoise);
    const penumbra = smoothstep(0.52, 0.58, spotNoise).mul(float(1.0).sub(spots));
    const faculae = smoothstep(0.45, 0.52, spotNoise).mul(float(1.0).sub(spots)).mul(float(1.0).sub(penumbra));

    // Vivid photosphere palette
    const deepRed = vec3(0.7, 0.15, 0.02);
    const warmOrange = vec3(1.0, 0.5, 0.08);
    const brightYellow = vec3(1.0, 0.75, 0.3);
    const hotWhite = vec3(1.0, 0.9, 0.6);
    const faculaeBright = vec3(1.0, 0.85, 0.5);

    const photosphere = mix(brightYellow, hotWhite, fineGran.mul(0.5)).toVar();
    photosphere.assign(mix(photosphere, warmOrange, float(1.0).sub(medGran).mul(0.2)));

    const col = mix(baseTex.mul(1.05), photosphere, 0.6).toVar();

    col.assign(mix(col, deepRed, spots.mul(0.75)));
    col.assign(mix(col, warmOrange, penumbra.mul(0.4)));
    col.assign(mix(col, faculaeBright, faculae.mul(0.25)));

    // Fresnel
    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const edgeFresnel = float(1.0).sub(abs(normalWorld.dot(viewDir)));

    // Edge glow (all tiers)
    col.addAssign(vec3(1.0, 0.4, 0.08).mul(pow(edgeFresnel, float(4.0)).mul(0.2)));

    // Center boost (all tiers)
    const centerBoost = pow(float(1.0).sub(edgeFresnel), float(2.0));
    col.mulAssign(float(0.9).add(centerBoost.mul(0.15)));

    // Medium+: prominences + magnetic field
    if (quality !== 'low') {
      const prominence = smoothstep(0.7, 0.95, edgeFresnel).mul(
        smoothstep(0.6, 0.75, noise3(pos.mul(5.0).add(vec3(t.mul(0.3))))),
      );
      col.addAssign(vec3(1.0, 0.4, 0.1).mul(prominence.mul(0.5)));

      const magField = sin(pos.y.mul(30.0).add(flow.mul(5.0))).mul(0.02).mul(edgeFresnel);
      col.addAssign(vec3(1.0, 0.8, 0.4).mul(magField));
    }

    // High only: coronal bright points
    if (quality === 'high') {
      const coronalPt = smoothstep(0.78, 0.88,
        noise3(pos.mul(12.0).add(vec3(t.mul(0.8), t.negate().mul(0.5), t.mul(0.6)))),
      );
      col.addAssign(vec3(0.5, 0.6, 1.0).mul(coronalPt.mul(0.15)));
    }

    // Gentle pulsing
    const pulse = float(1.0).add(sin(time.mul(0.3)).mul(0.02)).add(sin(time.mul(0.8)).mul(0.01));
    col.mulAssign(pulse);

    return col;
  })();

  return material;
}
