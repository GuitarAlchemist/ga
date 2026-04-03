// src/components/PrimeRadiant/shaders/ReactionDiffusionTSL.ts
// TSL material that overlays reaction-diffusion Turing patterns on governance
// node surfaces, encoding health as emergent pattern structure.
//
// Governance meaning:
//   - Stable spots/stripes: healthy governance, coordinated adoption
//   - Chaotic turbulence: governance breakdown (Seldon Crisis)
//   - Smooth/uniform: converged or stale (no differential adoption)
//
// Blends RD texture with the base crystal material color. The RD pattern
// modulates emissive intensity — crisis nodes glow with veins of color.

import * as THREE from 'three';
import { MeshStandardNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3,
  texture, uv,
  normalWorld, cameraPosition, positionWorld,
  mix, pow, abs, smoothstep,
} from 'three/tsl';
import type { QualityTier } from './TSLUniforms';

// ── Types ──

export interface CrisisMaterialOptions {
  /** Base material color from CrystalNodeMaterials */
  baseColor: THREE.Color;
  /** RD DataTexture from GrayScottGrid */
  rdTexture: THREE.DataTexture;
  /** Material roughness (from crystal material) */
  roughness?: number;
  /** Material metalness (from crystal material) */
  metalness?: number;
  /** Quality tier */
  quality: QualityTier;
}

// ── Material factory ──

/**
 * Create a TSL MeshStandardNodeMaterial that blends RD crisis texture
 * with the base governance node appearance.
 */
export function createCrisisMaterial(options: CrisisMaterialOptions): MeshStandardNodeMaterial {
  const { baseColor, rdTexture, roughness = 0.3, metalness = 0.8, quality } = options;
  const material = new MeshStandardNodeMaterial();
  material.roughness = roughness;
  material.metalness = metalness;

  const rdTex = texture(rdTexture);
  const base = vec3(baseColor.r, baseColor.g, baseColor.b);

  // ── Color: blend base crystal with RD pattern ──
  material.colorNode = Fn(() => {
    const uvCoord = uv();
    const rd = rdTex.sample(uvCoord);
    const rdIntensity = rd.r; // R channel holds pattern intensity

    // Blend: base crystal color + RD pattern as warm overlay
    const rdColor = vec3(1.0, 0.6, 0.2).mul(rdIntensity); // warm orange veins
    const blendAmount = quality === 'high' ? 0.4 : 0.25;
    return mix(base, base.add(rdColor), float(blendAmount));
  })();

  // ── Emissive: RD pattern drives emissive glow ──
  material.emissiveNode = Fn(() => {
    const uvCoord = uv();
    const rd = rdTex.sample(uvCoord);
    const rdIntensity = rd.r;

    // Fresnel: RD pattern more visible at glancing angles
    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const fresnel = float(1.0).sub(abs(normalWorld.dot(viewDir)));
    const fresnelBoost = quality === 'high'
      ? pow(fresnel, float(2.0)).mul(0.5).add(0.5)
      : float(1.0);

    // Crisis glow: warm veins that pulse with pattern intensity
    const emissiveColor = vec3(1.0, 0.4, 0.1); // warm crisis color
    const emissiveStrength = rdIntensity.mul(fresnelBoost).mul(0.6);

    return emissiveColor.mul(emissiveStrength);
  })();

  // ── Roughness: pattern areas are smoother (more reflective) ──
  if (quality === 'high') {
    material.roughnessNode = Fn(() => {
      const rd = rdTex.sample(uv());
      return float(roughness).sub(rd.r.mul(0.15)); // veins are shinier
    })();
  }

  return material;
}
