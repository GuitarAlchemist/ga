/**
 * Modal Meadow — per-mode configuration (v0.8).
 *
 * v0.8 ships ALL seven diatonic modes laid out left-to-right along world-x
 * in modal-brightness order (Lydian = brightest, Locrian = darkest). Each
 * adjacent pair differs by one note flatted/sharped, so walking the field
 * is literally a tour down the modal brightness curve.
 *
 * Per mode we bundle:
 *  - colour palette (grass base + tip, sky tint, pond tint)
 *  - wind behaviour (speed + strength + droop, plus an optional
 *    `windReversalSec` for Locrian which can't decide which way to blow)
 *  - chord progression as MIDI note numbers (each chord = array of MIDI ints)
 *  - HUD descriptor string
 *  - regionCenterX: world-x of this region's centre
 *
 * MIDI numbers chosen so all progressions sit in the same approximate
 * register (~C4..C5) so crossfade volume stays even across modes.
 */

import * as THREE from 'three';

export interface ModeConfig {
  /** Display name, e.g. "Ionian". */
  name: string;
  /** One-line "feel" descriptor for the HUD (≤80 chars). */
  descriptor: string;
  /** Grass blade base colour (shadow side). */
  baseColor: THREE.Color;
  /** Grass blade tip colour (lit side). */
  tipColor: THREE.Color;
  /** Sky horizon tint. */
  skyColor: THREE.Color;
  /** Pond water tint (used by reflective ponds). */
  pondColor: THREE.Color;
  /** Wind oscillation frequency. */
  windSpeed: number;
  /** Wind bend amplitude. */
  windStrength: number;
  /** Extra blade-base droop in radians (0 = upright, 0.35 ≈ noticeable lean). */
  droop: number;
  /**
   * If present, wind direction reverses every `windReversalSec` seconds.
   * Used only by Locrian (4s) to make the tritone-root instability visible.
   */
  windReversalSec?: number;
  /**
   * If present, blades inside this region randomly flash with a brief
   * lightness pulse (~5% chance per frame per blade). Locrian's shimmer.
   */
  shimmer?: boolean;
  /** Ambient chord progression as arrays of MIDI note numbers. */
  progression: number[][];
  /** Seconds per chord. */
  chordDurationSec: number;
  /** World-x of this region's centre (v0.8 layout). */
  regionCenterX: number;
}

// ─── Progressions ────────────────────────────────────────────────────────────
// All voiced in C-ish register so cross-region volume stays even. The tonic
// of each mode is in the comment; not all modes start on C (Phrygian = E,
// Locrian = B) but the progressions still live within the C4–C5 octave.

/** C Lydian: C – D – Em7 – C  (I – II – iii7 – I; brightening). */
const LYDIAN_PROGRESSION: number[][] = [
  [60, 64, 67],         // C major   (C4 E4 G4)
  [62, 66, 69],         // D major   (D4 F#4 A4) ← the Lydian #4 colour
  [64, 67, 71, 74],     // Em7       (E4 G4 B4 D5)
  [60, 64, 67],         // C major
];

/** C Ionian: C – F – G – C  (I – IV – V – I) — kept from v0.5. */
const IONIAN_PROGRESSION: number[][] = [
  [60, 64, 67],         // C major  (C4 E4 G4)
  [65, 69, 72],         // F major  (F4 A4 C5)
  [67, 71, 74],         // G major  (G4 B4 D5)
  [60, 64, 67],         // C major
];

/** C Mixolydian: C – F – Bb – C  (I – IV – bVII – I; rock/folk). */
const MIXOLYDIAN_PROGRESSION: number[][] = [
  [60, 64, 67],         // C major   (C4 E4 G4)
  [65, 69, 72],         // F major   (F4 A4 C5)
  [58, 62, 65],         // Bb major  (Bb3 D4 F4) ← the bVII colour
  [60, 64, 67],         // C major
];

/** C Dorian: Cm7 – F – Gm7 – Cm7  (i7 – IV – v7 – i7; jazz/modal-minor). */
const DORIAN_PROGRESSION: number[][] = [
  [60, 63, 67, 70],     // Cm7       (C4 Eb4 G4 Bb4)
  [65, 69, 72],         // F major   (F4 A4 C5)   ← the raised-6 colour
  [67, 70, 74, 77],     // Gm7       (G4 Bb4 D5 F5)
  [60, 63, 67, 70],     // Cm7
];

/** C Aeolian: Cm – Ab – Bb – Cm  (i – bVI – bVII – i; natural minor). */
const AEOLIAN_PROGRESSION: number[][] = [
  [60, 63, 67],         // Cm        (C4 Eb4 G4)
  [56, 60, 63],         // Ab major  (Ab3 C4 Eb4) ← bVI
  [58, 62, 65],         // Bb major  (Bb3 D4 F4)  ← bVII
  [60, 63, 67],         // Cm
];

/** E Phrygian: Em – F – Dm – Em  (i – bII – bvii – i) — kept from v0.5. */
const PHRYGIAN_PROGRESSION: number[][] = [
  [64, 67, 71],         // E minor   (E4 G4 B4)
  [65, 69, 72],         // F major   (F4 A4 C5)   ← bII, the Phrygian colour
  [62, 65, 69],         // D minor   (D4 F4 A4)   ← bvii
  [64, 67, 71],         // E minor
];

/** B Locrian: B° – C – Am – B°  (i° – bII – bvii – i°; tritone-root = unstable). */
const LOCRIAN_PROGRESSION: number[][] = [
  [59, 62, 65],         // B diminished (B3 D4 F4) ← tritone root colour
  [60, 64, 67],         // C major      (C4 E4 G4)   ← bII
  [57, 60, 64],         // A minor      (A3 C4 E4)   ← bvii
  [59, 62, 65],         // B diminished
];

// ─── Mode configs ────────────────────────────────────────────────────────────

export const LYDIAN: ModeConfig = {
  name: 'Lydian',
  descriptor: 'Floating, magical — the major scale\'s #4 raises the roof',
  baseColor: new THREE.Color('#5a8a35'),
  tipColor: new THREE.Color('#caf080'),
  skyColor: new THREE.Color('#fff8d8'),
  pondColor: new THREE.Color('#88e6ff'),  // bright cyan-blue
  windSpeed: 0.5,
  windStrength: 0.22,
  droop: 0.0,
  progression: LYDIAN_PROGRESSION,
  chordDurationSec: 4.0,
  regionCenterX: -210,
};

export const IONIAN: ModeConfig = {
  name: 'Ionian',
  descriptor: 'Bright, stable, the home base of major-key tonality',
  baseColor: new THREE.Color('#3a6b2a'),
  tipColor: new THREE.Color('#a8d670'),
  skyColor: new THREE.Color('#f3e0a8'),
  pondColor: new THREE.Color('#a8c8e0'),  // warm gold-blue (kept from v0.7)
  windSpeed: 0.6,
  windStrength: 0.25,
  droop: 0.0,
  progression: IONIAN_PROGRESSION,
  chordDurationSec: 4.0,
  regionCenterX: -140,
};

export const MIXOLYDIAN: ModeConfig = {
  name: 'Mixolydian',
  descriptor: 'Major scale with a flat 7 — rock, folk, modal jazz',
  baseColor: new THREE.Color('#3d6b30'),
  tipColor: new THREE.Color('#9bc560'),
  skyColor: new THREE.Color('#e8ccc0'),
  pondColor: new THREE.Color('#c8a8c0'),  // warm rose-blue
  windSpeed: 0.85,
  windStrength: 0.30,
  droop: 0.05,
  progression: MIXOLYDIAN_PROGRESSION,
  chordDurationSec: 4.0,
  regionCenterX: -70,
};

export const DORIAN: ModeConfig = {
  name: 'Dorian',
  descriptor: 'Minor with a raised 6 — the jazz-ballad mode',
  baseColor: new THREE.Color('#2f5a40'),
  tipColor: new THREE.Color('#7caa78'),
  skyColor: new THREE.Color('#c8c8d4'),
  pondColor: new THREE.Color('#7ca0c0'),  // cool blue
  windSpeed: 1.0,
  windStrength: 0.35,
  droop: 0.10,
  progression: DORIAN_PROGRESSION,
  chordDurationSec: 4.0,
  regionCenterX: 0,
};

export const AEOLIAN: ModeConfig = {
  name: 'Aeolian',
  descriptor: 'Natural minor — the melancholic home',
  baseColor: new THREE.Color('#2a4a3a'),
  tipColor: new THREE.Color('#6c9078'),
  skyColor: new THREE.Color('#a0a0b0'),
  pondColor: new THREE.Color('#5a7090'),  // cool steel-blue
  windSpeed: 1.25,
  windStrength: 0.42,
  droop: 0.18,
  progression: AEOLIAN_PROGRESSION,
  chordDurationSec: 4.0,
  regionCenterX: 70,
};

export const PHRYGIAN: ModeConfig = {
  name: 'Phrygian',
  descriptor: 'Dark, modal, evokes Spanish and Middle-Eastern idioms',
  baseColor: new THREE.Color('#1d2a1a'),
  tipColor: new THREE.Color('#5a6b5a'),
  skyColor: new THREE.Color('#6b5d7a'),
  pondColor: new THREE.Color('#605580'),  // violet-blue (kept from v0.7)
  windSpeed: 1.6,
  windStrength: 0.55,
  droop: 0.35,
  progression: PHRYGIAN_PROGRESSION,
  chordDurationSec: 4.0,
  regionCenterX: 140,
};

export const LOCRIAN: ModeConfig = {
  name: 'Locrian',
  descriptor: 'Diminished root — never resolves, always unstable',
  baseColor: new THREE.Color('#1a1a25'),
  tipColor: new THREE.Color('#3a3a55'),
  // Sky color: deeper-dusky violet but with enough luminance to read as a
  // sky, not a void. Brief specified `#3a3a55` but that paired against
  // similarly-dark grass produces a featureless black render at the test
  // viewport; #4a4263 retains the "shadowy purple-black" mood while keeping
  // contrast against the blades.
  skyColor: new THREE.Color('#4a4263'),
  pondColor: new THREE.Color('#2a1a3a'),  // shadowy purple-black
  windSpeed: 1.8,
  windStrength: 0.45,           // not the strongest — it's *erratic*, not big
  droop: 0.22,
  windReversalSec: 4.0,         // direction flips every chord change
  shimmer: true,                // blades randomly desaturate-flash
  progression: LOCRIAN_PROGRESSION,
  chordDurationSec: 4.0,
  regionCenterX: 210,
};

/**
 * All seven modes ordered left-to-right along world-x in modal-brightness
 * order. Same order as `regionCenterX`. The audio engine, shader, and HUD
 * all index into this array by region.
 *
 *   x =  -210   -140    -70     0      +70    +140   +210
 *      Lydian Ionian Mixo  Dorian Aeolian Phrygian Locrian
 *      (brightest) ─────────────────→ (darkest)
 */
export const MODES: ModeConfig[] = [
  LYDIAN,
  IONIAN,
  MIXOLYDIAN,
  DORIAN,
  AEOLIAN,
  PHRYGIAN,
  LOCRIAN,
];

/** Half-width of each region along x. v0.8: 70m wide regions → 35m half-width. */
export const REGION_HALF_WIDTH = 35;

/**
 * Width of the smoothstep transition band at each region boundary. The
 * region itself is REGION_HALF_WIDTH wide; the band straddling the boundary
 * is ±BAND_HALF_METERS, inside which two adjacent modes are mixed.
 */
export const BAND_HALF_METERS = 15;

/**
 * Returns a 7-element array of per-region weights for a given world-x.
 * Always sums to ~1.0. Uses a Gaussian-like falloff around each region's
 * centre via a soft 1D RBF, then renormalises so the sum is exactly 1.
 *
 * This is the canonical "where am I in the brightness curve" function —
 * audio, sky, and shader all use it.
 *
 * Implementation detail: distance-squared / (2σ²) where σ = REGION_HALF_WIDTH
 * gives full-weight 1.0 at the centre and falls off so neighbours overlap
 * exactly inside the ±BAND_HALF_METERS transition band. Cheap.
 */
export const modeWeightsForX = (x: number): number[] => {
  const sigma = REGION_HALF_WIDTH;
  const invTwoSigmaSq = 1 / (2 * sigma * sigma);
  const weights: number[] = new Array(MODES.length);
  let total = 0;
  for (let i = 0; i < MODES.length; i++) {
    const d = x - MODES[i].regionCenterX;
    const w = Math.exp(-d * d * invTwoSigmaSq);
    weights[i] = w;
    total += w;
  }
  if (total > 0) {
    for (let i = 0; i < weights.length; i++) weights[i] /= total;
  } else {
    // Far outside any region — collapse to nearest mode for safety.
    let best = 0;
    let bestD = Math.abs(x - MODES[0].regionCenterX);
    for (let i = 1; i < MODES.length; i++) {
      const d = Math.abs(x - MODES[i].regionCenterX);
      if (d < bestD) {
        bestD = d;
        best = i;
      }
    }
    weights.fill(0);
    weights[best] = 1;
  }
  return weights;
};

/**
 * Which mode the player is "in" — picked by maximum weight.
 * Used for HUD label.
 */
export const dominantModeForX = (x: number): ModeConfig => {
  const w = modeWeightsForX(x);
  let bestI = 0;
  for (let i = 1; i < w.length; i++) {
    if (w[i] > w[bestI]) bestI = i;
  }
  return MODES[bestI];
};

/**
 * Total field extent along X. Auto-walk uses this; ground extends ±FIELD_HALF_X
 * with extra margin. Lydian centre is -210, Locrian is +210, both have a
 * 35m half-width → ±245m effective.
 */
export const FIELD_HALF_X = 245;
