// src/components/PrimeRadiant/shaders/Ocean/shoreMask.ts
//
// Path-independent shore mask + ocean color helpers for the planet
// ocean zoom feature (v3 production spec).
//
// Both paths under consideration in v3 (Path B projected-grid ray-
// tracing, Path A fallback mounted-patch) consume the same shore
// mask and water-color math; they only differ in WHERE the depth
// sample is taken (fragment shader vs vertex shader). This module
// exposes:
//
//   1. Pure TypeScript math helpers (no TSL, no Three.js deps) so
//      unit tests can validate the attenuation and color curves
//      without a WebGPU runtime
//   2. TSL node helpers that wrap the same math for use inside
//      either path's shader graph
//   3. A GEBCO bathymetry texture loader with sensible defaults
//      matching v3 R-S1 (load once, cached on window, 16-bit
//      HalfFloat decoding)
//
// Deliberately takes NO opinion on how its output is consumed.
// The caller decides whether waveAttenuation multiplies a vertex
// displacement (Path A) or a fragment-shader heightfield amplitude
// (Path B). Same for waterColor.

import * as THREE from 'three';
import { Fn, float, vec3, texture as tslTexture, uniform, mix, exp, sub, mul, min, clamp, smoothstep } from 'three/tsl';

// ── Constants (v3 R-S3, R-S4) ───────────────────────────────────

/**
 * Depth in meters at which wave displacement fully ramps in.
 * 0 m → zero amplitude, 50 m → full amplitude, smooth between.
 * See v3 production spec R-S3.
 */
export const WAVE_FULL_DEPTH_M = 50.0;

/**
 * Depth in meters for ocean color saturation (Beer-Lambert).
 * At this depth the water has attenuated ~63% of the way from the
 * shallow color toward the deep color (since `1 - e^-1 ≈ 0.63`).
 * Chosen empirically from satellite ocean color imagery so that
 * coastal shelves look turquoise and open ocean looks navy.
 *
 * Physically motivated: the clearest open-ocean water (Jerlov
 * type IA) has an extinction coefficient around 0.02 m^-1 for
 * blue wavelengths, giving a characteristic depth of 50 m. For
 * the coastal wavelengths visible through the shore mask, the
 * effective depth to "full deep color" is larger — ~80 m matches
 * how MODIS ocean color imagery fades between turquoise shelf
 * water and open-ocean navy.
 */
export const COLOR_CHARACTERISTIC_DEPTH_M = 80.0;

/**
 * Maximum depth encoded in the GEBCO bathymetry texture, in meters.
 * GEBCO 2023's deepest point is Challenger Deep at ~10_925 m.
 * We round up to 11_000 m so the 16-bit encoding has a clean
 * linear scale: raw texture value ∈ [0, 65535] maps to depth
 * ∈ [0, 11000] m via `depth = raw * MAX_DEPTH_M / 65535`.
 *
 * Encoding convention (v3 spec, Data section):
 *   raw == 0       → land or sea level
 *   raw == 65535   → 11_000 m deep
 *
 * Land pixels are detected via `raw == 0` in the shader and cause
 * a fragment discard.
 */
export const MAX_DEPTH_M = 11_000.0;

/**
 * Shallow-water color, approximately matching Caribbean turquoise.
 * Linear-space RGB triplet in [0, 1].
 */
export const COLOR_SHALLOW = { r: 0.18, g: 0.65, b: 0.78 } as const;

/**
 * Deep-water color, approximately matching open-ocean navy.
 * Linear-space RGB triplet in [0, 1].
 */
export const COLOR_DEEP = { r: 0.04, g: 0.16, b: 0.27 } as const;

// ── Pure math helpers (unit-testable) ──────────────────────────

/**
 * Decode a normalized GEBCO texture sample (∈ [0, 1]) to depth in
 * meters. Matches the shader's texture sampler output range.
 *
 * @param rawNormalized Texture sample value in [0, 1]
 * @returns Depth in meters, in [0, MAX_DEPTH_M]
 */
export function decodeDepthMeters(rawNormalized: number): number {
  const clamped = Math.max(0, Math.min(1, rawNormalized));
  return clamped * MAX_DEPTH_M;
}

/**
 * Wave amplitude attenuation as a function of depth.
 * Zero at the shoreline, full (1.0) at and beyond WAVE_FULL_DEPTH_M,
 * smooth cubic interpolation between via Hermite smoothstep.
 *
 * @param depthMeters Depth in meters, >= 0
 * @returns Attenuation multiplier in [0, 1]
 */
export function waveAttenuation(depthMeters: number): number {
  if (depthMeters <= 0) return 0;
  if (depthMeters >= WAVE_FULL_DEPTH_M) return 1;
  const t = depthMeters / WAVE_FULL_DEPTH_M;
  // Hermite smoothstep: t^2 * (3 - 2t)
  return t * t * (3 - 2 * t);
}

/**
 * Beer-Lambert ocean color extinction.
 *
 * Returns the color at the given depth as a blend from the shallow
 * color toward the deep color, with the transmission through the
 * water column following `I(d) = I₀ * e^(-d / L)` where L is the
 * characteristic depth. This is a simplified single-band model —
 * Tessendorf-grade spectral water uses three bands (R, G, B) with
 * distinct extinction coefficients per channel, but the
 * single-band approximation is good enough for the shore-blend
 * use case since the shallow and deep colors already encode the
 * result of full spectral extinction.
 *
 * Mathematically:
 *   t = 1 - exp(-depth / L)
 *   color = mix(shallow, deep, t)
 *
 * At depth = 0:   t = 0,     color = shallow
 * At depth = L:   t ≈ 0.63,  color ≈ 63% of the way to deep
 * At depth = 3L:  t ≈ 0.95,  color ≈ fully deep
 *
 * @param depthMeters Depth in meters, >= 0
 * @returns Linear-space RGB triplet in [0, 1]
 */
export function waterColor(depthMeters: number): { r: number; g: number; b: number } {
  const d = Math.max(0, depthMeters);
  const t = 1 - Math.exp(-d / COLOR_CHARACTERISTIC_DEPTH_M);
  return {
    r: COLOR_SHALLOW.r * (1 - t) + COLOR_DEEP.r * t,
    g: COLOR_SHALLOW.g * (1 - t) + COLOR_DEEP.g * t,
    b: COLOR_SHALLOW.b * (1 - t) + COLOR_DEEP.b * t,
  };
}

// ── GEBCO bathymetry texture loader ─────────────────────────────

const GEBCO_ASSET_URL = '/textures/planets/bathymetry_8k.png';
const CACHE_KEY = '__ga_gebcoTextureCache';

type WindowWithCache = typeof window & {
  [CACHE_KEY]?: THREE.Texture;
};

/**
 * Load the GEBCO bathymetry texture, caching on `window` so repeat
 * mounts don't re-fetch. The texture is configured for equirectangular
 * sampling: `wrapS = RepeatWrapping` (longitude wraps), `wrapT =
 * ClampToEdgeWrapping` (latitude clamps at the poles).
 *
 * Uses Three.js's regular `TextureLoader`. Three.js decodes 16-bit
 * PNG to 8-bit internally unless we explicitly request
 * `HalfFloatType` — which we do, because the 16-bit depth
 * resolution is load-bearing. At 8-bit the shallowest 43 m of
 * ocean would all read as "value 1", destroying the shore
 * attenuation curve.
 *
 * v3 R-S1: "loaded once per browser session via Three.js
 * TextureLoader and cached on the window."
 */
export function loadBathymetryTexture(): THREE.Texture {
  const w = typeof window !== 'undefined' ? (window as WindowWithCache) : undefined;
  if (w && w[CACHE_KEY]) {
    return w[CACHE_KEY]!;
  }

  const loader = new THREE.TextureLoader();
  const tex = loader.load(GEBCO_ASSET_URL);
  tex.wrapS = THREE.RepeatWrapping;     // longitude wraps
  tex.wrapT = THREE.ClampToEdgeWrapping; // latitude clamps
  tex.minFilter = THREE.LinearFilter;    // v3 R-S2
  tex.magFilter = THREE.LinearFilter;
  tex.generateMipmaps = false;           // avoid averaging land with ocean at mip boundaries
  tex.colorSpace = THREE.NoColorSpace;   // raw depth data, not sRGB

  // Half-float decoding — preserves 16-bit depth resolution from
  // the source asset. If the runtime doesn't support it, Three.js
  // falls back to 8-bit and the shallow-water shore curve gets
  // quantized; in that case adopt a depth look-up table as a
  // follow-up.
  tex.type = THREE.HalfFloatType;

  if (w) {
    w[CACHE_KEY] = tex;
  }
  return tex;
}

/**
 * Drop the cached bathymetry texture from the window. Useful in
 * tests and for hot-reload.
 */
export function clearBathymetryCache(): void {
  const w = typeof window !== 'undefined' ? (window as WindowWithCache) : undefined;
  if (w && w[CACHE_KEY]) {
    w[CACHE_KEY]!.dispose();
    delete w[CACHE_KEY];
  }
}

// ── TSL node helpers ────────────────────────────────────────────

/**
 * Returned by `createShoreMaskNodes` — a bundle of TSL nodes the
 * caller can wire into its ocean material's vertex or fragment
 * shader graph. The bundle is renderer-path-agnostic; Path B
 * (projected-grid fragment sampling) and Path A (vertex-stage
 * sampling) both work with this shape.
 */
export interface ShoreMaskNodeBundle {
  /** Raw texture node bound to the GEBCO bathymetry asset. */
  bathymetryTexture: ReturnType<typeof tslTexture>;

  /**
   * Takes a `vec2` UV (longitude ∈ [0, 1], latitude ∈ [0, 1] from
   * south to north pole) and returns a `float` depth value in
   * [0, MAX_DEPTH_M].
   */
  sampleDepth: ReturnType<typeof Fn<(uv: ReturnType<typeof vec3>) => ReturnType<typeof float>>>;

  /**
   * Takes a `float` depth in meters and returns a `float` in
   * [0, 1] — the wave amplitude multiplier per v3 R-S3.
   */
  attenuationFromDepth: ReturnType<typeof Fn>;

  /**
   * Takes a `float` depth in meters and returns a `vec3` water
   * color via Beer-Lambert blend between COLOR_SHALLOW and
   * COLOR_DEEP.
   */
  colorFromDepth: ReturnType<typeof Fn>;
}

/**
 * Build TSL nodes that implement the shore-mask math against a
 * loaded GEBCO texture. Path-agnostic — the returned nodes can be
 * composed into either a vertex shader (Path A mounted patch) or
 * a fragment shader (Path B projected grid).
 *
 * The caller provides the depth texture (typically from
 * `loadBathymetryTexture()`) and gets back the TSL node bundle.
 * Decoupling the texture from the math nodes lets tests swap in
 * a fake depth source without touching the curves.
 */
export function createShoreMaskNodes(depthTexture: THREE.Texture): ShoreMaskNodeBundle {
  const bathymetryTexture = tslTexture(depthTexture);
  const uMaxDepth = uniform(MAX_DEPTH_M);
  const uWaveFullDepth = uniform(WAVE_FULL_DEPTH_M);
  const uColorCharDepth = uniform(COLOR_CHARACTERISTIC_DEPTH_M);
  const uColorShallow = uniform(new THREE.Vector3(COLOR_SHALLOW.r, COLOR_SHALLOW.g, COLOR_SHALLOW.b));
  const uColorDeep = uniform(new THREE.Vector3(COLOR_DEEP.r, COLOR_DEEP.g, COLOR_DEEP.b));

  // Sample the bathymetry texture at a UV and decode to meters.
  // The caller is responsible for converting (lat, lon) to UV —
  // typically `uv = vec2(lon / TWO_PI + 0.5, lat / PI + 0.5)`.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const sampleDepth: any = Fn(([uv]: [ReturnType<typeof vec3>]) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const raw = (bathymetryTexture as any).sample(uv).r;
    return mul(raw, uMaxDepth);
  });

  // Wave attenuation: smoothstep(0, WAVE_FULL_DEPTH_M, depth).
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const attenuationFromDepth: any = Fn(([depth]: [ReturnType<typeof float>]) => {
    return smoothstep(float(0.0), uWaveFullDepth, depth);
  });

  // Beer-Lambert color blend.
  //   t = 1 - exp(-depth / L)
  //   color = mix(shallow, deep, t)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const colorFromDepth: any = Fn(([depth]: [ReturnType<typeof float>]) => {
    const t = sub(float(1.0), exp(mul(float(-1.0), mul(depth, sub(float(1.0), float(0.0))).div(uColorCharDepth))));
    const tClamped = clamp(t, float(0.0), float(1.0));
    return mix(uColorShallow, uColorDeep, tClamped);
  });

  // Silence unused-variable warnings for nodes we expose via the
  // bundle; the TSL type surface is loose enough that TS can miss
  // the transitive consumption.
  void min;
  void vec3;

  return {
    bathymetryTexture,
    sampleDepth,
    attenuationFromDepth,
    colorFromDepth,
  };
}
