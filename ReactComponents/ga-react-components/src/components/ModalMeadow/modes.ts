/**
 * Modal Meadow — per-mode configuration.
 *
 * v0 ships two modes: Ionian (warm + stable) and Phrygian (dark + dusky).
 * v1 will add the other 5 diatonic modes; v2 may add MIDI-driven chord input.
 *
 * Each mode bundles:
 *  - colour palette (grass base + tip + sky tint)
 *  - wind behaviour (speed + strength + droop)
 *  - chord progression as MIDI note numbers (each chord = array of MIDI ints)
 *  - HUD descriptor string
 *
 * MIDI numbers chosen so progressions sit in the same approximate register
 * (~C4..C5) so crossfade volume stays even.
 */

import * as THREE from 'three';

export interface ModeConfig {
  /** Display name, e.g. "Ionian". */
  name: string;
  /** One-line "feel" descriptor for the HUD. */
  descriptor: string;
  /** Grass blade base colour (shadow side). */
  baseColor: THREE.Color;
  /** Grass blade tip colour (lit side). */
  tipColor: THREE.Color;
  /** Sky horizon tint. */
  skyColor: THREE.Color;
  /** Wind oscillation frequency. */
  windSpeed: number;
  /** Wind bend amplitude. */
  windStrength: number;
  /** Extra blade-base droop in radians (0 = upright, 0.35 ≈ noticeable lean). */
  droop: number;
  /** Ambient chord progression as arrays of MIDI note numbers. */
  progression: number[][];
  /** Seconds per chord. */
  chordDurationSec: number;
}

/**
 * C major: C – F – G – C (I – IV – V – I). All triads voiced root-position
 * in the C4–C5 octave so they sit warm without being muddy.
 */
const IONIAN_PROGRESSION: number[][] = [
  [60, 64, 67],     // C major  (C4 E4 G4)
  [65, 69, 72],     // F major  (F4 A4 C5)
  [67, 71, 74],     // G major  (G4 B4 D5)
  [60, 64, 67],     // C major
];

/**
 * E Phrygian: i – bII – bvii – i (Em – F – Dm – Em).
 * The bII (F-major chord against an E tonic) is the Phrygian signature move
 * — the half-step from E to F is what makes the mode read as dark/Spanish.
 */
const PHRYGIAN_PROGRESSION: number[][] = [
  [64, 67, 71],     // E minor   (E4 G4 B4)
  [65, 69, 72],     // F major   (F4 A4 C5)  ← bII, the Phrygian colour
  [62, 65, 69],     // D minor   (D4 F4 A4)  ← bvii
  [64, 67, 71],     // E minor
];

export const IONIAN: ModeConfig = {
  name: 'Ionian',
  descriptor: 'Bright, stable, the home base of major-key tonality',
  baseColor: new THREE.Color('#3a6b2a'),
  tipColor: new THREE.Color('#a8d670'),
  skyColor: new THREE.Color('#f3e0a8'),
  windSpeed: 0.6,
  windStrength: 0.25,
  droop: 0.0,
  progression: IONIAN_PROGRESSION,
  chordDurationSec: 4.0,
};

export const PHRYGIAN: ModeConfig = {
  name: 'Phrygian',
  descriptor: 'Dark, modal, evokes Spanish and Middle-Eastern idioms',
  baseColor: new THREE.Color('#1d2a1a'),
  tipColor: new THREE.Color('#5a6b5a'),
  skyColor: new THREE.Color('#6b5d7a'),
  windSpeed: 1.6,
  windStrength: 0.55,
  droop: 0.35,
  progression: PHRYGIAN_PROGRESSION,
  chordDurationSec: 4.0,
};

/**
 * Both v0 regions, ordered left-to-right along world x.
 * Ionian centred at x=-60, Phrygian centred at x=+60, with the soft
 * transition band straddling x=0.
 */
export const MODES: ModeConfig[] = [IONIAN, PHRYGIAN];

/**
 * Half-width of the transition band along x. Inside ±BLEND_HALF metres the
 * two modes are mixed; outside, only the local mode is heard/seen.
 */
export const BLEND_HALF_METERS = 25;

/**
 * Returns the [ionianWeight, phrygianWeight] pair for a given world-x.
 * Always sums to 1.0. At x = -BLEND_HALF: pure Ionian. At x = +BLEND_HALF:
 * pure Phrygian. Smoothstep in between to avoid a linear ramp's mid-mix dip.
 */
export const modeWeightsForX = (x: number): [number, number] => {
  const t = THREE.MathUtils.smoothstep(x, -BLEND_HALF_METERS, BLEND_HALF_METERS);
  return [1 - t, t];
};

/**
 * Which mode the player is "in" — used for HUD label, picked by the
 * dominant weight. Returns Ionian for ties (left bias is fine for v0).
 */
export const dominantModeForX = (x: number): ModeConfig => {
  const [iW, pW] = modeWeightsForX(x);
  return pW > iW ? PHRYGIAN : IONIAN;
};
