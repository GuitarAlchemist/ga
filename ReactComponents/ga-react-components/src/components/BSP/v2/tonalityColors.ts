/**
 * Color mapping for BSP tonal regions.
 *
 * The DOOM aesthetic uses high-saturation hues so each region is identifiable
 * at a glance. Colors are HSL-anchored for consistency: each tonality type
 * occupies a 30°-wide slice of the hue wheel.
 */

import * as THREE from 'three';

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
  Major:      0x4ade80, // green   — stable / consonant
  Minor:      0x60a5fa, // blue    — melancholic
  Modal:      0xd946ef, // magenta — exotic
  Atonal:     0xef4444, // red     — chaotic
  Chromatic:  0xfacc15, // yellow  — transitional
  Dominant:   0xfb923c, // orange  — tense / unresolved
  Diminished: 0x06b6d4, // cyan    — unstable
  Augmented:  0xec4899, // pink    — ambiguous
  Unknown:    0x9ca3af, // gray    — fallback
};

export function colorFor(tonalityType: string): THREE.Color {
  const key = (tonalityType as TonalityType) in PALETTE ? (tonalityType as TonalityType) : 'Unknown';
  return new THREE.Color(PALETTE[key]);
}

export function hexFor(tonalityType: string): number {
  const key = (tonalityType as TonalityType) in PALETTE ? (tonalityType as TonalityType) : 'Unknown';
  return PALETTE[key];
}
