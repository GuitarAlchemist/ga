// src/components/Ocean/OceanQuality.ts
// Adaptive quality tier detection for ocean rendering.
//
// Detects GPU capability via WebGPU adapter info + a quick benchmark,
// then selects a quality preset that scales mesh resolution, wave count,
// and post-processing features.

export type QualityTier = 'low' | 'medium' | 'high' | 'ultra';

export interface OceanQualityConfig {
  tier: QualityTier;
  meshRes: number;           // vertices per side (128–1024)
  waveCount: 4 | 6 | 8;     // number of Gerstner waves
  oceanSize: number;         // world-space meters
  enableBloom: boolean;
  bloomStrength: number;
  bloomRadius: number;
  bloomThreshold: number;
  enableVignette: boolean;
  vignetteStrength: number;
  fogDensity: number;
  pixelRatio: number;        // clamp for device pixel ratio
  sunSpecExponent: number;   // higher = tighter glint (needs HDR bloom to glow)
  sunSpecMultiplier: number;
}

const CONFIGS: Record<QualityTier, OceanQualityConfig> = {
  low: {
    tier: 'low',
    meshRes: 192,
    waveCount: 4,
    oceanSize: 4000,
    enableBloom: false,
    bloomStrength: 0,
    bloomRadius: 0,
    bloomThreshold: 1,
    enableVignette: false,
    vignetteStrength: 0,
    fogDensity: 0.00035,
    pixelRatio: 1,
    sunSpecExponent: 512,
    sunSpecMultiplier: 40,
  },
  medium: {
    tier: 'medium',
    meshRes: 384,
    waveCount: 6,
    oceanSize: 6000,
    enableBloom: false,
    bloomStrength: 0,
    bloomRadius: 0,
    bloomThreshold: 1,
    enableVignette: false,
    vignetteStrength: 0,
    fogDensity: 0.00028,
    pixelRatio: 1.5,
    sunSpecExponent: 768,
    sunSpecMultiplier: 80,
  },
  high: {
    tier: 'high',
    meshRes: 512,
    waveCount: 8,
    oceanSize: 8000,
    enableBloom: true,
    bloomStrength: 0.35,
    bloomRadius: 0.4,
    bloomThreshold: 0.85,
    enableVignette: true,
    vignetteStrength: 0.35,
    fogDensity: 0.00022,
    pixelRatio: 2,
    sunSpecExponent: 1024,
    sunSpecMultiplier: 150,
  },
  ultra: {
    tier: 'ultra',
    meshRes: 768,
    waveCount: 8,
    oceanSize: 10000,
    enableBloom: true,
    bloomStrength: 0.3,
    bloomRadius: 0.5,
    bloomThreshold: 0.85,
    enableVignette: true,
    vignetteStrength: 0.4,
    fogDensity: 0.00020,
    pixelRatio: 2,
    sunSpecExponent: 1024,
    sunSpecMultiplier: 80,
  },
};

/**
 * Detect the appropriate quality tier based on GPU info.
 *
 * Strategy: check WebGPU adapter description for known GPU families.
 * Falls back to 'medium' if detection fails.
 */
export async function detectQualityTier(): Promise<OceanQualityConfig> {
  try {
    if (!('gpu' in navigator)) return CONFIGS.low;

    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) return CONFIGS.medium;

    const info = await adapter.requestAdapterInfo();
    const desc = (info.description ?? '').toLowerCase() + ' ' + (info.device ?? '').toLowerCase();

    // Ultra tier: RTX 4070+ / 50-series / RX 7800+
    if (/rtx\s*(50[0-9]{2}|4[0-9]{2}0|40[89]0)|rx\s*7[89][0-9]{2}|arc\s*a7[0-9]{2}/i.test(desc)) {
      return CONFIGS.ultra;
    }

    // High tier: RTX 3060+ / 4060 / RX 6700+ / Apple M2+
    if (/rtx\s*(30[6-9]0|40[56]0)|rx\s*6[7-9][0-9]{2}|apple\s*m[2-9]/i.test(desc)) {
      return CONFIGS.high;
    }

    // Low tier: Intel integrated / Mali / Adreno (mobile)
    if (/intel.*uhd|intel.*iris|mali|adreno|powervr|img\s/i.test(desc)) {
      return CONFIGS.low;
    }

    // Default to medium for unknown discrete GPUs
    return CONFIGS.medium;
  } catch {
    return CONFIGS.medium;
  }
}

/** Get config by name (for manual override) */
export function getQualityConfig(tier: QualityTier): OceanQualityConfig {
  return CONFIGS[tier];
}
