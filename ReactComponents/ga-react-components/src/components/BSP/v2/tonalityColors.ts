/**
 * Color mapping for BSP tonal regions.
 *
 * Two-tier strategy:
 *   1. If the region carries `MusicalLeafMeta` (curated tree), colour by
 *      the leaf's circle-of-fifths hue — adjacent keys get adjacent
 *      colours. This is the chromatic colour wheel a guitarist already
 *      visualises (Lerdahl, Krumhansl) projected into the world.
 *   2. Otherwise (legacy or API tree), fall back to a tonality-type
 *      palette so existing consumers still get something sensible.
 *
 * Saturation and lightness are kept consistent with the GA dark
 * palette (#0d1117 backdrop) — high-saturation pastels at L≈55% give
 * good contrast against the floor without being eye-watering.
 */

import * as THREE from 'three';
import type { BSPRegion } from '../BSPApiService';
import type { MusicalRegion } from './musicalTree';

export type TonalityType =
  | 'Major'
  | 'Minor'
  | 'Modal'
  | 'Atonal'
  | 'Chromatic'
  | 'Dominant'
  | 'Diminished'
  | 'Augmented'
  | 'Unknown';

const PALETTE: Record<TonalityType, number> = {
  Major:      0x4ade80,
  Minor:      0x60a5fa,
  Modal:      0xd946ef,
  Atonal:     0xef4444,
  Chromatic:  0xfacc15,
  Dominant:   0xfb923c,
  Diminished: 0x06b6d4,
  Augmented:  0xec4899,
  Unknown:    0x9ca3af,
};

function hslToHex(h: number, s: number, l: number): number {
  const c = new THREE.Color();
  c.setHSL((h % 360) / 360, s, l);
  return c.getHex();
}

function isMusicalRegion(r: BSPRegion): r is MusicalRegion {
  return (r as MusicalRegion).music !== undefined;
}

export function colorForRegion(region: BSPRegion): THREE.Color {
  if (isMusicalRegion(region) && region.music) {
    return new THREE.Color(hslToHex(region.music.hue, 0.55, 0.55));
  }
  return new THREE.Color(hexForTonality(region.tonalityType));
}

export function hexForRegion(region: BSPRegion): number {
  if (isMusicalRegion(region) && region.music) {
    return hslToHex(region.music.hue, 0.55, 0.55);
  }
  return hexForTonality(region.tonalityType);
}

export function hexForTonality(tonalityType: string): number {
  const key = (tonalityType as TonalityType) in PALETTE ? (tonalityType as TonalityType) : 'Unknown';
  return PALETTE[key];
}

// --- Backwards-compatible aliases (legacy callsites) -------------------

/** @deprecated use `colorForRegion(region)` instead. */
export function colorFor(tonalityType: string): THREE.Color {
  return new THREE.Color(hexForTonality(tonalityType));
}

/** @deprecated use `hexForRegion(region)` instead. */
export function hexFor(tonalityType: string): number {
  return hexForTonality(tonalityType);
}
