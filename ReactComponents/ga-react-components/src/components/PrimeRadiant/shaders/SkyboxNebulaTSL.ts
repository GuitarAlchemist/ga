// src/components/PrimeRadiant/shaders/SkyboxNebulaTSL.ts
// TSL deep-space skybox — cosmic web + deep field + nearby stars.
//
// Rendered on the BackSide of a large enclosing sphere. All procedural,
// no textures. Layered cosmos:
//   • Laniakea supercluster — large-scale FBM filaments (the cosmic web)
//   • Deep-field galaxies    — procedural elliptical smudges, redshifted
//   • Quasars                — sparse bright points, wavelength-shifted
//   • Nebulae                — Orion + Carina regional glows
//   • Nearby bright stars    — temperature-colored point sources
//   • Mid / far star fields  — hash-dotted distant populations
//
// Quality tier controls which layers are active on weaker GPUs.

import { MeshBasicNodeMaterial } from 'three/webgpu';
import * as THREE from 'three';
import {
  Fn, float, vec3, vec2,
  positionLocal, floor, fract,
  mix, smoothstep, step,
  normalize, length, max, sin, cos, dot,
} from 'three/tsl';
import { fbm6, hash3 } from './TSLNoiseLib';
import type { QualityTier } from './TSLUniforms';

export interface SkyboxNebulaOptions {
  /** Render quality — trims galaxy/quasar layers on low-end devices. */
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
  // Opaque: AdditiveBlending on a full-coverage BackSide sphere stacks
  // with other additive layers under WebGPU and dominates the view.
  material.transparent = false;

  material.colorNode = Fn(() => {
    const dir = normalize(positionLocal);

    // ─── Base gradient: near-black with faint blue-purple at higher Y ───
    const baseColor = mix(
      vec3(0.001, 0.001, 0.006),
      vec3(0.004, 0.002, 0.012),
      dir.y.mul(0.5).add(0.5),
    ).toVar();

    // ─── Laniakea supercluster — large-scale cosmic web ───
    // Low-frequency FBM creates diffuse filament structure; two layers
    // at different scales give the flowing-sheets appearance of the
    // local supercluster. Subtle, sits behind everything.
    const webCoarse = fbm6(dir.mul(0.9).add(vec3(11.3, 4.7, 8.1)));
    const webFine = fbm6(dir.mul(2.3).add(vec3(5.5, 9.1, 2.7)));
    const webPhase = webCoarse.mul(0.65).add(webFine.mul(0.35));
    const webFilament = smoothstep(0.48, 0.68, webPhase);
    const webTint = mix(
      vec3(0.020, 0.008, 0.030),  // deep violet
      vec3(0.045, 0.018, 0.022),  // dusty rose
      webFine,
    );
    baseColor.addAssign(webTint.mul(webFilament).mul(0.9));

    // Cold voids between filaments — subtle negative contrast
    const voidMask = smoothstep(0.48, 0.30, webPhase).mul(0.4);
    baseColor.mulAssign(float(1.0).sub(voidMask.mul(0.3)));

    // ─── Orion-like nebula (reddish-pink regional glow) ───
    const orionCenter = normalize(vec3(0.5, 0.2, -0.8));
    const orionDist = length(dir.sub(orionCenter));
    const orionMask = smoothstep(1.1, 0.15, orionDist);
    const orionNoise = fbm6(dir.mul(4.0).add(vec3(1.3, 2.7, 0.5)));
    baseColor.addAssign(vec3(0.15, 0.03, 0.04).mul(orionMask).mul(orionNoise));

    // ─── Carina-like nebula (blue-teal regional glow) ───
    const carinaCenter = normalize(vec3(-0.6, -0.3, 0.7));
    const carinaDist = length(dir.sub(carinaCenter));
    const carinaMask = smoothstep(0.95, 0.12, carinaDist);
    const carinaNoise = fbm6(dir.mul(5.0).add(vec3(3.1, 0.4, 1.8)));
    baseColor.addAssign(vec3(0.025, 0.060, 0.085).mul(carinaMask).mul(carinaNoise));

    // Helper: project dir to a 2D grid lookup via signed octahedron parameterization.
    // We use dir directly * scale and floor into integer cells — since dir is
    // on a unit sphere, the grid is slightly non-uniform but visually fine.
    const cell = (scale: number) => floor(dir.mul(scale));
    const cellLocal = (scale: number) => fract(dir.mul(scale)).sub(0.5);

    // ─── Deep-field galaxies — procedural elliptical smudges (medium+) ───
    if (quality !== 'low') {
      const galGrid = float(18.0);
      const galCell = cell(18.0);
      const galLocal = cellLocal(18.0);
      const galSeed = hash3(galCell);                          // 0..1 existence
      const galSeed2 = hash3(galCell.add(vec3(7.0, 3.0, 1.0))); // 0..1 color/rot
      const galSeed3 = hash3(galCell.add(vec3(2.0, 5.0, 9.0))); // 0..1 size/axis
      // Only ~6% of cells host a galaxy
      const galExists = step(0.94, galSeed);
      // Rotate local coords for ellipse orientation
      const galAngle = galSeed2.mul(6.283);
      const c = cos(galAngle); const s = sin(galAngle);
      // rotate (galLocal.x, galLocal.y)
      const gx = galLocal.x.mul(c).sub(galLocal.y.mul(s));
      const gy = galLocal.x.mul(s).add(galLocal.y.mul(c));
      // Elliptical distance: major axis varies
      const major = galSeed3.mul(0.25).add(0.08);
      const minor = major.mul(float(0.35).add(galSeed3.mul(0.5)));
      const galDist = length(vec2(gx.div(major), gy.div(minor)));
      const galCore = smoothstep(1.0, 0.0, galDist).mul(0.9);
      // Redshift-tinted palette: closer = warmer, further = cooler-red
      const galColor = mix(
        vec3(0.18, 0.14, 0.10),  // near galaxies: warm dust
        vec3(0.10, 0.04, 0.14),  // far: cool violet (mild redshift)
        galSeed2,
      );
      baseColor.addAssign(galColor.mul(galCore).mul(galExists).mul(0.85));
      // Suppressing unused-variable warning (galGrid declared for readability).
      baseColor.addAssign(vec3(0.0).mul(galGrid));
    }

    // ─── Quasars — sparse bright points with wavelength shift (medium+) ───
    if (quality !== 'low') {
      const qsoCell = cell(60.0);
      const qsoLocal = cellLocal(60.0);
      const qsoSeed = hash3(qsoCell);
      const qsoSeed2 = hash3(qsoCell.add(vec3(13.0, 17.0, 19.0)));
      // Rare: ~0.2% of cells
      const qsoExists = step(0.998, qsoSeed);
      // Circular point with tight halo
      const qsoDist = length(qsoLocal);
      const qsoCore = smoothstep(0.08, 0.0, qsoDist);
      const qsoHalo = smoothstep(0.35, 0.05, qsoDist).mul(0.18);
      // Redshift-to-blueshift spectrum by host seed
      const qsoColor = mix(
        vec3(0.95, 0.35, 0.20),  // high-Z: redshifted fireball
        vec3(0.55, 0.75, 1.05),  // low-Z: bluewhite
        qsoSeed2,
      );
      baseColor.addAssign(qsoColor.mul(qsoCore.add(qsoHalo)).mul(qsoExists).mul(0.8));
    }

    // ─── Nearby bright stars — temperature-colored points ───
    const nearCell = cell(100.0);
    const nearLocal = cellLocal(100.0);
    const nearSeed = hash3(nearCell);
    const nearSeed2 = hash3(nearCell.add(vec3(31.0, 41.0, 53.0)));
    const nearExists = step(0.995, nearSeed);   // ~0.5% of cells
    const nearDist = length(nearLocal);
    const nearCore = smoothstep(0.12, 0.0, nearDist);
    const nearHalo = smoothstep(0.30, 0.10, nearDist).mul(0.25);
    // Temperature classes: M(red) · K(orange) · G(yellow) · F(white) · A(blue-white) · B(blue)
    const warm = mix(vec3(1.00, 0.55, 0.30), vec3(1.00, 0.90, 0.75), nearSeed2);
    const cool = mix(vec3(1.00, 0.98, 0.94), vec3(0.65, 0.80, 1.05), nearSeed2);
    const nearColor = mix(warm, cool, step(0.5, nearSeed2));
    baseColor.addAssign(nearColor.mul(nearCore.add(nearHalo)).mul(nearExists).mul(0.5));

    // ─── Mid-distance stars — small dotted field ───
    const midCell = cell(260.0);
    const midSeed = hash3(midCell);
    const midExists = step(0.997, midSeed);
    const midColor = mix(
      vec3(0.80, 0.85, 1.00),
      vec3(1.00, 0.95, 0.80),
      hash3(midCell.add(vec3(7.0))),
    );
    baseColor.addAssign(midColor.mul(midExists).mul(0.16));

    // ─── Far faint stars — the background hiss ───
    const farCell = cell(700.0);
    const farSeed = hash3(farCell);
    const farExists = step(0.9985, farSeed);
    baseColor.addAssign(vec3(0.55, 0.65, 0.85).mul(farExists).mul(0.08));

    // ─── Dust wisps — breaks up any flat regions ───
    const dust = fbm6(dir.mul(2.5).add(vec3(7.3, 3.1, 5.7)));
    baseColor.addAssign(vec3(0.004, 0.003, 0.005).mul(dust));

    return max(baseColor, vec3(0.0));
  })();

  return material;
}
