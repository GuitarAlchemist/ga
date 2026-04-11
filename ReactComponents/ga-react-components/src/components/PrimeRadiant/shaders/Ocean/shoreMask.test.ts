// src/components/PrimeRadiant/shaders/Ocean/shoreMask.test.ts
//
// Unit tests for the path-independent ocean-shading math in
// shoreMask.ts. These validate the shore attenuation and
// Beer-Lambert color curves WITHOUT requiring a WebGPU runtime,
// Three.js WebGL context, or a visible browser — so they run in
// plain Node / Vitest / Jest.
//
// Test strategy (v3 spec testing-layer 1):
//   Layer 1 — pure math unit tests (this file)
//   Layer 2 — ray-sphere intersection math in JS (separate file,
//             tests the shader-side math without the shader)
//   Layer 3 — Playwright visual regression at pass/pivot zoom
//             thresholds (GA already has Playwright per CLAUDE.md)
//   Layer 4 — the AC5 frame-diff luminance test from v3 spec

import { describe, it, expect } from 'vitest';
import {
  waveAttenuation,
  waterColor,
  decodeDepthMeters,
  WAVE_FULL_DEPTH_M,
  COLOR_CHARACTERISTIC_DEPTH_M,
  COLOR_SHALLOW,
  COLOR_DEEP,
  MAX_DEPTH_M,
} from './shoreMask';

describe('decodeDepthMeters', () => {
  it('maps 0 to 0 m (land)', () => {
    expect(decodeDepthMeters(0)).toBe(0);
  });

  it('maps 1 to MAX_DEPTH_M (Challenger Deep)', () => {
    expect(decodeDepthMeters(1)).toBe(MAX_DEPTH_M);
  });

  it('maps 0.5 linearly to half max depth', () => {
    expect(decodeDepthMeters(0.5)).toBeCloseTo(MAX_DEPTH_M / 2, 5);
  });

  it('clamps negative input to 0', () => {
    expect(decodeDepthMeters(-0.25)).toBe(0);
  });

  it('clamps > 1 input to MAX_DEPTH_M', () => {
    expect(decodeDepthMeters(1.5)).toBe(MAX_DEPTH_M);
  });
});

describe('waveAttenuation', () => {
  it('returns 0 at the shoreline (depth 0)', () => {
    expect(waveAttenuation(0)).toBe(0);
  });

  it('returns 0 for negative depth (land pixel)', () => {
    expect(waveAttenuation(-10)).toBe(0);
  });

  it('returns 1 at WAVE_FULL_DEPTH_M (50 m)', () => {
    expect(waveAttenuation(WAVE_FULL_DEPTH_M)).toBe(1);
  });

  it('returns 1 beyond WAVE_FULL_DEPTH_M', () => {
    expect(waveAttenuation(100)).toBe(1);
    expect(waveAttenuation(5000)).toBe(1);
  });

  it('is monotonically increasing across the attenuation band', () => {
    const samples = [0, 5, 10, 20, 30, 40, 49, 50];
    let prev = -Infinity;
    for (const d of samples) {
      const att = waveAttenuation(d);
      expect(att).toBeGreaterThanOrEqual(prev);
      prev = att;
    }
  });

  it('has smooth hermite curve shape (t=0.5 → 0.5)', () => {
    // Hermite smoothstep: h(0.5) = 0.5² * (3 - 2*0.5) = 0.25 * 2 = 0.5
    expect(waveAttenuation(WAVE_FULL_DEPTH_M / 2)).toBeCloseTo(0.5, 5);
  });

  it('has zero derivative at the boundaries (t=0 and t=1)', () => {
    // Numerical derivative check. Hermite smoothstep's key property
    // is f'(0) = f'(1) = 0 — that's what makes it "smooth" across
    // the boundary into the clamped regions.
    const eps = 0.0001;
    const derivAt0 = (waveAttenuation(eps) - waveAttenuation(0)) / eps;
    const derivAt1 = (waveAttenuation(WAVE_FULL_DEPTH_M) - waveAttenuation(WAVE_FULL_DEPTH_M - eps)) / eps;
    expect(Math.abs(derivAt0)).toBeLessThan(0.01);
    expect(Math.abs(derivAt1)).toBeLessThan(0.01);
  });
});

describe('waterColor (Beer-Lambert extinction)', () => {
  it('returns shallow color at depth 0', () => {
    const c = waterColor(0);
    expect(c.r).toBeCloseTo(COLOR_SHALLOW.r, 5);
    expect(c.g).toBeCloseTo(COLOR_SHALLOW.g, 5);
    expect(c.b).toBeCloseTo(COLOR_SHALLOW.b, 5);
  });

  it('returns shallow color at negative depth (clamped)', () => {
    const c = waterColor(-100);
    expect(c.r).toBeCloseTo(COLOR_SHALLOW.r, 5);
    expect(c.g).toBeCloseTo(COLOR_SHALLOW.g, 5);
    expect(c.b).toBeCloseTo(COLOR_SHALLOW.b, 5);
  });

  it('is ~63% of the way to deep color at the characteristic depth', () => {
    // Beer-Lambert definition: at depth L, t = 1 - e^-1 ≈ 0.632
    const c = waterColor(COLOR_CHARACTERISTIC_DEPTH_M);
    const tExpected = 1 - Math.exp(-1);
    const rExpected = COLOR_SHALLOW.r * (1 - tExpected) + COLOR_DEEP.r * tExpected;
    expect(c.r).toBeCloseTo(rExpected, 5);
  });

  it('is ~95% of the way to deep color at 3× characteristic depth', () => {
    // At 3L, t = 1 - e^-3 ≈ 0.9502
    const c = waterColor(3 * COLOR_CHARACTERISTIC_DEPTH_M);
    const tExpected = 1 - Math.exp(-3);
    const rExpected = COLOR_SHALLOW.r * (1 - tExpected) + COLOR_DEEP.r * tExpected;
    expect(c.r).toBeCloseTo(rExpected, 5);
  });

  it('approaches deep color asymptotically at great depth', () => {
    const c = waterColor(10000);
    expect(c.r).toBeCloseTo(COLOR_DEEP.r, 3);
    expect(c.g).toBeCloseTo(COLOR_DEEP.g, 3);
    expect(c.b).toBeCloseTo(COLOR_DEEP.b, 3);
  });

  it('is monotonically approaching deep color in all channels', () => {
    // Beer-Lambert is monotone: deeper water is always at least as
    // "deep-colored" as shallower water.
    const depths = [0, 10, 30, 80, 160, 320, 640, 1280];
    // The "distance from shallow" metric should be monotonically
    // non-decreasing. Use the red channel since it has the
    // largest spread between shallow and deep.
    let prevDistance = -Infinity;
    for (const d of depths) {
      const c = waterColor(d);
      const distance = Math.abs(c.r - COLOR_SHALLOW.r);
      expect(distance).toBeGreaterThanOrEqual(prevDistance);
      prevDistance = distance;
    }
  });

  it('every channel stays within [0, 1]', () => {
    const depths = [0, 1, 50, 200, 1000, 5000, 10000];
    for (const d of depths) {
      const c = waterColor(d);
      expect(c.r).toBeGreaterThanOrEqual(0);
      expect(c.r).toBeLessThanOrEqual(1);
      expect(c.g).toBeGreaterThanOrEqual(0);
      expect(c.g).toBeLessThanOrEqual(1);
      expect(c.b).toBeGreaterThanOrEqual(0);
      expect(c.b).toBeLessThanOrEqual(1);
    }
  });
});

describe('cross-function sanity', () => {
  it('matches the v3 spec R-S3 contract (0-50 m attenuation range)', () => {
    // At 0 m: no waves, shallow color
    const shore = waveAttenuation(0);
    expect(shore).toBe(0);

    // At 50 m: full waves, slightly darker than pure shallow
    const shelf = waveAttenuation(50);
    expect(shelf).toBe(1);

    // At 200 m: full waves, significantly into deep color
    const drop = waveAttenuation(200);
    expect(drop).toBe(1);

    const colorDrop = waterColor(200);
    // 200 m is 2.5 × COLOR_CHARACTERISTIC_DEPTH_M (80), so t ≈ 1 - e^-2.5 ≈ 0.918
    const tExpected = 1 - Math.exp(-200 / COLOR_CHARACTERISTIC_DEPTH_M);
    expect(tExpected).toBeGreaterThan(0.9);
    // The red channel at 200 m should be much closer to DEEP than SHALLOW
    expect(colorDrop.r - COLOR_DEEP.r).toBeLessThan(COLOR_SHALLOW.r - colorDrop.r);
  });

  it('shore + color pair produces a plausible coastline gradient', () => {
    // Sample the curve at 10 m intervals from 0 to 100 m.
    // Both curves should be monotonic and the shore attenuation
    // should reach 1 within the first half of the interval.
    const samples = Array.from({ length: 11 }, (_, i) => i * 10);
    const results = samples.map((d) => ({
      depth: d,
      attenuation: waveAttenuation(d),
      color: waterColor(d),
    }));

    // Attenuation reaches 1 at depth 50 m exactly (tested above)
    // and stays at 1 beyond.
    expect(results[5]!.attenuation).toBe(1);
    expect(results[10]!.attenuation).toBe(1);

    // Color's red channel decreases monotonically (SHALLOW.r > DEEP.r).
    for (let i = 1; i < results.length; i++) {
      expect(results[i]!.color.r).toBeLessThanOrEqual(results[i - 1]!.color.r);
    }
  });
});
