// src/components/PrimeRadiant/shaders/SkyboxNebulaTSL.ts
// TSL deep-space skybox — Hubble/Webb-style vacuum aesthetic.
//
// Design principles:
//   1. Background is NEAR-BLACK — space is dark; stars pop against vacuum.
//   2. Stars are SHARP points with brightness variance. Top ~1% render
//      diffraction spikes (Webb-signature 4-point cross).
//   3. Nebulae are small, sparse, subtle regional glows — NOT sky-wide haze.
//   4. Cosmic-web voids ATTENUATE (darken) rather than add colored glow.
//   5. Galaxies are rare, distinct, elliptical/redshift-tinted smudges.
//   6. Quasars are ultra-sparse bright points with color-shifted halos.
//
// Rendered on the BackSide of a large enclosing sphere. Fully procedural.
// Quality tier skips galaxy/quasar/diffraction layers on weak GPUs.

import { MeshBasicNodeMaterial } from 'three/webgpu';
import * as THREE from 'three';
import {
  Fn, float, vec3, vec2,
  positionLocal, floor, fract,
  mix, smoothstep, step,
  normalize, length, max, abs, sin, cos, pow,
} from 'three/tsl';
import { fbm6, hash3 } from './TSLNoiseLib';
import type { QualityTier } from './TSLUniforms';

export interface SkyboxNebulaOptions {
  /** Render quality — trims galaxy/quasar/diffraction layers. */
  quality?: QualityTier;
}

/**
 * Create a TSL-based deep-space skybox material.
 * Rendered on the BackSide of a large sphere. All procedural.
 */
export function createSkyboxNebulaMaterialTSL(
  options: SkyboxNebulaOptions = {},
): MeshBasicNodeMaterial {
  const { quality = 'high' } = options;

  const material = new MeshBasicNodeMaterial();
  material.side = THREE.BackSide;
  material.depthWrite = false;
  material.transparent = false;

  material.colorNode = Fn(() => {
    const dir = normalize(positionLocal);

    // ─── Near-black vacuum background ───
    // Barely perceptible blue cast (like real dark-sky photos), not a purple dome.
    const baseColor = vec3(0.0006, 0.0006, 0.0014).toVar();

    // ─── Cosmic web as DARKENING ───
    // Real voids are where filaments aren't — we model filaments as subtle
    // brightness (barely), voids as additional darkness. No purple-ish tint.
    const web = fbm6(dir.mul(1.3).add(vec3(11.3, 4.7, 8.1)));
    const voidMask = smoothstep(0.55, 0.35, web); // dark where noise is low
    baseColor.mulAssign(float(1.0).sub(voidMask.mul(0.55)));
    // Very subtle warm hint in filament crests
    const filamentHint = smoothstep(0.55, 0.72, web).mul(0.003);
    baseColor.addAssign(vec3(1.0, 0.6, 0.4).mul(filamentHint));

    // ─── Regional nebulae — small, sparse, tight masks ───
    // Orion-like (pink-red)
    const orionCenter = normalize(vec3(0.5, 0.2, -0.8));
    const orionDist = length(dir.sub(orionCenter));
    const orionMask = smoothstep(0.45, 0.05, orionDist);
    const orionNoise = fbm6(dir.mul(5.0).add(vec3(1.3, 2.7, 0.5)));
    baseColor.addAssign(vec3(0.07, 0.015, 0.025).mul(orionMask).mul(orionNoise));

    // Carina-like (blue-teal)
    const carinaCenter = normalize(vec3(-0.6, -0.3, 0.7));
    const carinaDist = length(dir.sub(carinaCenter));
    const carinaMask = smoothstep(0.40, 0.05, carinaDist);
    const carinaNoise = fbm6(dir.mul(6.0).add(vec3(3.1, 0.4, 1.8)));
    baseColor.addAssign(vec3(0.012, 0.035, 0.055).mul(carinaMask).mul(carinaNoise));

    // Grid helpers
    const cell = (scale: number) => floor(dir.mul(scale));
    const cellLocal = (scale: number) => fract(dir.mul(scale)).sub(0.5);

    // ─── Deep-field galaxies — rare elliptical smudges (medium+) ───
    if (quality !== 'low') {
      const galCell = cell(22.0);
      const galLocal = cellLocal(22.0);
      const galSeed = hash3(galCell);
      const galSeed2 = hash3(galCell.add(vec3(7.0, 3.0, 1.0)));
      const galSeed3 = hash3(galCell.add(vec3(2.0, 5.0, 9.0)));
      const galExists = step(0.96, galSeed); // ~4% of cells
      const galAngle = galSeed2.mul(6.283);
      const c = cos(galAngle); const s = sin(galAngle);
      const gx = galLocal.x.mul(c).sub(galLocal.y.mul(s));
      const gy = galLocal.x.mul(s).add(galLocal.y.mul(c));
      const major = galSeed3.mul(0.20).add(0.06);
      const minor = major.mul(float(0.30).add(galSeed3.mul(0.45)));
      const galDist = length(vec2(gx.div(major), gy.div(minor)));
      // Gaussian-like core for smooth falloff
      const galCore = pow(smoothstep(1.0, 0.0, galDist), float(2.0)).mul(1.2);
      // Redshift palette
      const galColor = mix(
        vec3(0.18, 0.12, 0.06), // near: warm elliptical
        vec3(0.09, 0.03, 0.14), // far: cool redshifted
        galSeed2,
      );
      baseColor.addAssign(galColor.mul(galCore).mul(galExists));
    }

    // ─── Quasars — ultra-sparse bright points with halos (medium+) ───
    if (quality !== 'low') {
      const qsoCell = cell(80.0);
      const qsoLocal = cellLocal(80.0);
      const qsoSeed = hash3(qsoCell);
      const qsoSeed2 = hash3(qsoCell.add(vec3(13.0, 17.0, 19.0)));
      const qsoExists = step(0.9985, qsoSeed); // ~0.15%
      const qsoDist = length(qsoLocal);
      const qsoCore = smoothstep(0.05, 0.0, qsoDist);
      const qsoHalo = smoothstep(0.25, 0.04, qsoDist).mul(0.12);
      const qsoColor = mix(
        vec3(1.1, 0.35, 0.15), // high-Z redshift fireball
        vec3(0.50, 0.75, 1.15), // low-Z blue-white
        qsoSeed2,
      );
      baseColor.addAssign(qsoColor.mul(qsoCore.add(qsoHalo)).mul(qsoExists));
    }

    // ─── Bright named stars — sharp points with diffraction spikes (medium+) ───
    // ~0.15% density, variable brightness, top ~20% get Webb-style 4-point cross.
    if (quality !== 'low') {
      const brtCell = cell(90.0);
      const brtLocal = cellLocal(90.0);
      const brtSeed = hash3(brtCell);
      const brtSeed2 = hash3(brtCell.add(vec3(29.0, 37.0, 43.0)));
      const brtExists = step(0.9985, brtSeed);
      // Sharp point
      const brtDist = length(brtLocal);
      const brtCore = smoothstep(0.08, 0.0, brtDist);
      // 4-point diffraction cross: two thin bars along local axes
      const spikeH = smoothstep(0.015, 0.0, abs(brtLocal.y))
        .mul(smoothstep(0.40, 0.05, abs(brtLocal.x)));
      const spikeV = smoothstep(0.015, 0.0, abs(brtLocal.x))
        .mul(smoothstep(0.40, 0.05, abs(brtLocal.y)));
      const spikes = max(spikeH, spikeV).mul(0.65).mul(step(0.80, brtSeed2));
      // Brightness variance: 0.7..1.8
      const brtIntensity = brtSeed2.mul(1.1).add(0.7);
      // Star color by temperature
      const brtColor = mix(
        mix(vec3(1.00, 0.55, 0.30), vec3(1.00, 0.90, 0.75), brtSeed2), // red→yellow
        mix(vec3(1.00, 0.98, 0.94), vec3(0.65, 0.80, 1.10), brtSeed2), // white→blue
        step(0.5, brtSeed2),
      );
      baseColor.addAssign(brtColor.mul(brtCore.add(spikes)).mul(brtIntensity).mul(brtExists));
    }

    // ─── Nearby star field — varied brightness, sharp points ───
    const nearCell = cell(180.0);
    const nearLocal = cellLocal(180.0);
    const nearSeed = hash3(nearCell);
    const nearSeed2 = hash3(nearCell.add(vec3(31.0, 41.0, 53.0)));
    const nearExists = step(0.993, nearSeed); // ~0.7% density
    const nearDist = length(nearLocal);
    const nearCore = smoothstep(0.06, 0.0, nearDist);
    const nearWarm = mix(vec3(1.00, 0.55, 0.30), vec3(1.00, 0.90, 0.75), nearSeed2);
    const nearCool = mix(vec3(1.00, 0.98, 0.94), vec3(0.65, 0.80, 1.05), nearSeed2);
    const nearColor = mix(nearWarm, nearCool, step(0.5, nearSeed2));
    const nearIntensity = nearSeed2.mul(0.7).add(0.4); // 0.4..1.1
    baseColor.addAssign(nearColor.mul(nearCore).mul(nearIntensity).mul(nearExists));

    // ─── Mid-distance stars — tiny dots ───
    const midCell = cell(400.0);
    const midSeed = hash3(midCell);
    const midExists = step(0.997, midSeed);
    const midColor = mix(
      vec3(0.85, 0.90, 1.05),
      vec3(1.05, 0.95, 0.75),
      hash3(midCell.add(vec3(7.0))),
    );
    baseColor.addAssign(midColor.mul(midExists).mul(0.35));

    // ─── Far faint stars — background hiss ───
    const farCell = cell(900.0);
    const farSeed = hash3(farCell);
    const farExists = step(0.9985, farSeed);
    baseColor.addAssign(vec3(0.55, 0.65, 0.85).mul(farExists).mul(0.18));

    return max(baseColor, vec3(0.0));
  })();

  return material;
}
