// src/components/PrimeRadiant/shaders/LocalProxyVolumetricTSL.ts
// TSL material for Local Proxy Raymarching (LPR) — bounded volumetric
// lighting confined to a jurisdiction's AABB.
//
// Governance meaning: screen-space volumetrics ("fog everywhere") treats
// every pixel equally, which is democratic but untruthful — authority is
// bounded. LPR renders volumetric scattering only inside the bounding
// volume of a jurisdiction. The bounds of the effect ARE the bounds of
// the jurisdiction's authority. A beam that bled outside its mesh would
// be visible jurisdictional overreach; containment is correctness.
//
// Technique: render a box proxy with BackSide → fragment shader has the
// ray EXIT point in world space (positionWorld). Compute the ENTRY point
// via ray-AABB slab intersection from the camera. Raymarch between them,
// sampling analytic density (FBM) weighted by HG phase and jittered by
// hash noise to break banding.
//
// Reference: Maxime Heckel's volumetric lighting article
// (https://blog.maximeheckel.com/posts/shaping-light-volumetric-lighting-with-post-processing-and-raymarching/)
// ported from WebGL2/GLSL/postprocessing-lib to native WebGPU/TSL.

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec4,
  uniform, time,
  positionWorld, cameraPosition,
  normalize, length, dot, max, min, exp, pow,
  mix, sin, cos, smoothstep, abs,
} from 'three/tsl';
import { fbm3, hash3, noise3 } from './TSLNoiseLib';
import type { QualityTier } from './TSLUniforms';

// ── Options ──

export interface LocalProxyVolumetricOptions {
  /** Jurisdiction color (matches VoronoiShell CLUSTER_COLORS palette) */
  color: THREE.Color;
  /** Quality tier — drives step count */
  quality: QualityTier;
  /**
   * HG anisotropy parameter g ∈ [-1, 1].
   * Positive = forward scattering (institutional openness / transparent authority).
   * Negative = back scattering (opaque / hidden authority).
   * 0 = isotropic (ambient).
   * Governance semantic: map to institutional-openness score per jurisdiction.
   */
  anisotropy?: number;
  /** Extinction coefficient (how quickly density absorbs light). Higher = denser fog. */
  extinction?: number;
  /** Overall scattering intensity multiplier (before alpha pre-multiply). */
  intensity?: number;
  /** Analytic density noise scale (worldPos multiplier). */
  densityScale?: number;
  /**
   * Swirl rate in radians/second around the jurisdiction's vertical axis.
   * Governance semantic: rate of authority circulation within the jurisdiction.
   * Constitutional bodies swirl slowly (dignity); personas fast (activity).
   * 0 = static fog.
   */
  swirlRate?: number;
}

// ── Material factory ──

/**
 * Create a TSL material for a local proxy volumetric pass.
 *
 * Attach to a BoxGeometry sized to the jurisdiction's AABB. The box acts
 * as a ray-interval locator — rasterizing its backfaces gives the exit
 * point, slab intersection provides the entry, raymarching fills between.
 *
 * @returns Material + uniforms for per-frame updates from JurisdictionVolumetrics.
 */
export function createLocalProxyVolumetricMaterial(
  options: LocalProxyVolumetricOptions,
): {
  material: MeshBasicNodeMaterial;
  /** Jurisdiction AABB min corner (world space) — update per frame */
  boxMin: THREE.Uniform<THREE.Vector3>;
  /** Jurisdiction AABB max corner (world space) — update per frame */
  boxMax: THREE.Uniform<THREE.Vector3>;
  /** Authority light direction (normalized) — update if sun/authority moves */
  lightDir: THREE.Uniform<THREE.Vector3>;
} {
  const {
    color,
    quality,
    anisotropy = 0.35,
    extinction = 0.8,
    intensity = 1.0,
    densityScale = 0.08,
    swirlRate = 0.12,
  } = options;

  const material = new MeshBasicNodeMaterial();
  // BackSide so the fragment IS the ray exit point. Must be paired with
  // depthTest=false (below) otherwise opaque scene geometry sitting BETWEEN
  // the camera and the back wall of the proxy would cull our fragments,
  // and we'd never raymarch those pixels (blank holes behind every node).
  material.side = THREE.BackSide;
  material.transparent = true;
  material.depthWrite = false;
  // depthTest=true: scene geometry in front of backface culls our fragment,
  // huge perf saving with minimal visual loss (the "hole behind node spheres"
  // is small because nodes are tiny dots, mostly not in front of box walls).
  material.depthTest = true;
  material.blending = THREE.AdditiveBlending;

  // ── Uniforms ──
  const boxMinU = new THREE.Uniform(new THREE.Vector3(-1, -1, -1));
  const boxMaxU = new THREE.Uniform(new THREE.Vector3(1, 1, 1));
  const lightDirU = new THREE.Uniform(new THREE.Vector3(0.3, 1.0, 0.4).normalize());

  const uBoxMin = uniform(boxMinU.value);
  const uBoxMax = uniform(boxMaxU.value);
  const uLightDir = uniform(lightDirU.value);
  const uColor = uniform(color);
  const uG = uniform(anisotropy);
  const uExt = uniform(extinction);
  const uIntensity = uniform(intensity);
  const uDensityScale = uniform(densityScale);
  const uSwirlRate = uniform(swirlRate);

  // ── Step count by quality ──
  // 12/6: aggressive reduction for 60FPS target. Temporal jitter + lower
  // intensity (0.35) means fewer steps still reads as smooth fog.
  const stepCount = quality === 'high' ? 12 : quality === 'medium' ? 6 : 0;
  if (stepCount === 0) {
    // Low quality: material is inert. Caller can hide the mesh entirely.
    material.colorNode = Fn(() => vec3(0, 0, 0))();
    return { material, boxMin: boxMinU, boxMax: boxMaxU, lightDir: lightDirU };
  }

  // ── Henyey-Greenstein phase function ──
  // Classic volumetric scattering phase: p(cosθ, g). g=0 isotropic, g→1 forward.
  // Returns scalar intensity multiplier for a ray given its angle to light.
  const henyeyGreenstein = Fn(([cosTheta, g]) => {
    const g2 = g.mul(g);
    const denom = float(1).add(g2).sub(float(2).mul(g).mul(cosTheta));
    // pow(denom, 1.5) — use pow() so TSL emits the right WGSL call
    return float(1).sub(g2).div(float(4).mul(Math.PI).mul(pow(denom, float(1.5))));
  });

  material.colorNode = Fn(() => {
    const exit = positionWorld; // backface fragment = ray exit (world space)
    const ro = cameraPosition;
    const rd = normalize(exit.sub(ro));

    // ── Ray-AABB slab intersection (inlined) ──
    // TSL Fn() can't return JS objects — destructured fields become null
    // in the compiled shader. Inline the math so both tEnter/tExit remain
    // as live TSL nodes. Camera inside box → tEnter < 0 → clamped to 0.
    const invD = vec3(1.0, 1.0, 1.0).div(rd);
    const t0 = uBoxMin.sub(ro).mul(invD);
    const t1 = uBoxMax.sub(ro).mul(invD);
    const tmin = min(t0, t1);
    const tmax = max(t0, t1);
    const tEnterRaw = max(max(tmin.x, tmin.y), tmin.z);
    // tExit not needed — we use positionWorld as exit. Silence unused.
    void tmax;
    const tEnter = max(tEnterRaw, float(0));
    const entry = ro.add(rd.mul(tEnter));
    const rayLen = max(length(exit.sub(entry)), float(0.001));
    const dt = rayLen.div(float(stepCount));

    // Temporal jitter via hash of world position + time. Keeps banding
    // invisible without needing a blue-noise texture (MVP optimization).
    // Full blue-noise texture → v2 for higher-quality dithering.
    const jitter = hash3(entry.add(vec3(time.mul(7.13), time.mul(3.29), time.mul(11.7))))
      .sub(0.5)
      .mul(dt);

    const cosTheta = dot(rd, uLightDir);
    const phase = henyeyGreenstein(cosTheta, uG);

    const accum = vec3(0, 0, 0).toVar();
    const transmittance = float(1.0).toVar();

    // Unrolled step loop — matches codebase convention (see VoronoiShellTSL).
    // Step count is known at material-creation time, so JS-side unrolling
    // is cheap and avoids TSL Loop() node uncertainty.
    // Jurisdiction center & half-extents for swirl coordinate frame
    const center = uBoxMin.add(uBoxMax).mul(0.5);
    const halfExt = uBoxMax.sub(uBoxMin).mul(0.5);

    for (let i = 0; i < stepCount; i++) {
      const t = dt.mul(float(i + 0.5)).add(jitter);
      const p = entry.add(rd.mul(t));

      // ── Swirl: rotate sampling position around jurisdiction's Y axis ──
      // Rate scales with inverse distance-from-center (center spins fast,
      // edges lag) — creates a vortex/cyclone look instead of rigid rotation.
      // Governance: authority circulates fastest at the institutional core,
      // slows at jurisdictional edges where it meets other authorities.
      const local = p.sub(center);
      const radius2D = length(vec3(local.x, float(0), local.z));
      const spinSpeed = float(1.0).div(radius2D.mul(0.15).add(1.0));
      const angle = time.mul(uSwirlRate).mul(spinSpeed);
      const ca = cos(angle), sa = sin(angle);
      const spunLocal = vec3(
        local.x.mul(ca).sub(local.z.mul(sa)),
        local.y,
        local.x.mul(sa).add(local.z.mul(ca)),
      );
      const spun = spunLocal.add(center);

      // ── FBM density (domain warp removed for perf) ──
      // Three noise3 warp calls per step were 48 extra noise calls/pixel
      // at 16 steps, ~150M calls/frame at 1280x720. Dropping the warp
      // saves ~35% shader cost, visual loss is modest — swirl + vertical
      // gradient + radial falloff carry most of the shape.
      const slow = vec3(time.mul(0.04));
      const q = spun.mul(uDensityScale);
      const base = fbm3(q.add(slow));
      const detail = noise3(q.mul(3.5).add(vec3(time.mul(0.12)))).mul(0.22);
      const raw = base.add(detail);

      // ── Ellipsoidal falloff (the CRITICAL fix for visible box edges) ──
      // Without this, rays grazing the box at oblique angles traverse
      // more density than center rays, producing visible rectangular
      // silhouettes ("bright elliptical rings"). Normalizing position
      // to the unit sphere inscribed in the AABB, then fading density
      // with radius, gives a smooth ellipsoidal volume that LOOKS like
      // a jurisdiction instead of a box.
      const normPos = vec3(
        local.x.div(halfExt.x.add(0.001)),
        local.y.div(halfExt.y.add(0.001)),
        local.z.div(halfExt.z.add(0.001)),
      );
      const rNorm = length(normPos); // 0 at center, 1 at box face-center
      // Smooth falloff: full density until 0.4, fade to 0 by 0.95
      const radialFade = smoothstep(float(0.95), float(0.4), rNorm);

      // Governance: authority concentrates at the "working layer" (mid-Y),
      // dissipates at ceiling (abstract doctrine) and floor (implementation).
      const yNorm = local.y.div(halfExt.y.add(0.001));
      const vertProfile = float(1.0).sub(abs(yNorm).mul(abs(yNorm)));
      const vert = smoothstep(float(0.0), float(0.4), vertProfile);

      // Concentrate density — FBM is ~[0,1], threshold creates filaments
      const density = max(raw.sub(0.38), float(0)).mul(2.2).mul(vert).mul(radialFade);

      const sigma = density.mul(uExt);

      // ── Two-tone color gradient ──
      // Low density → deep saturated core color.
      // High density → warm highlighted filament (mix toward warm-white).
      // Governance: wispy outer fog = "soft authority"; bright cores =
      // "concentrated mandate." The visual tiers authority visibility.
      const dTone = smoothstep(float(0.1), float(0.9), density);
      const warmTint = uColor.mul(0.6).add(vec3(0.4, 0.35, 0.22));
      const tintedColor = mix(uColor.mul(0.8), warmTint, dTone);

      // ── Emission hotspots ──
      // pow(density, 2.5) boosts bright filaments non-linearly — same
      // shader cost, dramatic visual. Classic godrays-into-fog look.
      // Coefficient tuned so hotspots accent without washing out.
      const emission = pow(density, float(2.5)).mul(0.5);

      // Combined scatter: phase-weighted + emission hotspots
      const scatter = tintedColor.mul(density.add(emission))
        .mul(phase)
        .mul(transmittance)
        .mul(dt);
      accum.addAssign(scatter);

      // Beer-Lambert extinction along the ray
      transmittance.mulAssign(exp(sigma.negate().mul(dt)));

      // Note: no early-out Break() — unrolled loop can't break cleanly in
      // TSL. Final transmittance multiplied steps contribute near-zero
      // after saturation, so the cost is ~constant per pixel anyway.
    }

    // Physically-correct output: scattered radiance bounded by alpha.
    // alpha = 1 - transmittance. accum is already premultiplied by
    // transmittance-at-each-step, so it represents emitted light reaching
    // the camera. Final: accum scaled by intensity, capped by alpha so
    // proxies don't compound to white under AdditiveBlending.
    const alpha = float(1.0).sub(transmittance);
    // Inside-camera fade: VoronoiShell uses FrontSide to hide when camera
    // enters a cluster. LPR can't do that (needs BackSide to get exit
    // point), so instead we fade radiance based on tEnter. When camera
    // is inside the box, tEnterRaw < 0, so the smoothstep returns 0.
    // Governance semantic: jurisdictions are viewed from outside; when
    // you enter one, the atmospheric effect quietly dissipates to let
    // you see the nodes clearly.
    const insideFade = smoothstep(float(0.0), float(12.0), tEnterRaw);
    return accum.mul(uIntensity).mul(alpha).mul(insideFade);
  })();

  return { material, boxMin: boxMinU, boxMax: boxMaxU, lightDir: lightDirU };
}

// Silence unused-import warning for vec4 that some TypeScript
// configs flag even though TSL imports are evaluated at module load.
void vec4;
