/**
 * TonalOrbit — perceptual colour palette for pitch classes.
 *
 * We avoid `category10` (twelve unrelated hues) and rainbow (perceptually
 * non-uniform) and instead colour the 12 pitch classes along the circle of
 * fifths using a Sinebow-style hue rotation. Adjacent fifths get adjacent
 * hues, which lines up with the way the ear groups them and gives the
 * inner-orbit ring a smooth chromatic gradient.
 *
 * Why circle-of-fifths order, not chromatic order:
 *   C → G → D → A → E → B → F# → C# → G# → D# → A# → F → C
 *   Adjacent slots differ by a fifth (the most consonant non-octave
 *   interval), so neighbouring planet colours visually express harmonic
 *   proximity. Chromatic colouring would put tritone-related (most
 *   dissonant) PCs next to each other, which lies to the eye about the
 *   harmonic structure.
 */
import * as THREE from 'three';

/** Pitch-class names, chromatic order (PC 0..11). */
export const PITCH_CLASS_NAMES: readonly string[] = [
  'C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B',
];

/**
 * Circle-of-fifths order, expressed as PC index per slot 0..11.
 * Slot 0 = C, slot 1 = G, ..., slot 11 = F.
 */
export const CIRCLE_OF_FIFTHS_PC: readonly number[] = [
  0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5,
];

/** Given a PC (0..11), return its slot index in the circle-of-fifths order. */
export function circleOfFifthsSlot(pc: number): number {
  const idx = CIRCLE_OF_FIFTHS_PC.indexOf(((pc % 12) + 12) % 12);
  return idx >= 0 ? idx : 0;
}

/**
 * Sinebow: three sine waves 120° apart yield a perceptually smoother
 * rainbow than HSV at constant saturation. Returns a THREE.Color in
 * linear sRGB ready to assign to a material.
 *
 * `t` ranges 0..1.
 */
export function sinebow(t: number): THREE.Color {
  const phase = 2 * Math.PI;
  const r = Math.sin(phase * (t + 0.0 / 3.0));
  const g = Math.sin(phase * (t + 1.0 / 3.0));
  const b = Math.sin(phase * (t + 2.0 / 3.0));
  // Map [-1, 1] -> [0, 1], then bias toward brighter colours so the
  // motes / rim-lights have something to push.
  const to01 = (v: number) => Math.max(0, Math.min(1, 0.5 + 0.5 * v));
  return new THREE.Color(to01(r), to01(g), to01(b));
}

/**
 * Pitch-class colour, indexed by chromatic PC (0..11). Hue is taken
 * from the circle-of-fifths slot so neighbouring fifths share neighbour
 * hues.
 */
export function pitchClassColor(pc: number): THREE.Color {
  const slot = circleOfFifthsSlot(pc);
  return sinebow(slot / 12);
}

/**
 * Chord-family base colour. Each family gets a fixed hue so the
 * mid-orbit reads as "chord type" rather than "another PC ring".
 * Chosen empirically to look distinct on the dark space background.
 */
export const CHORD_FAMILY_COLORS: Record<string, THREE.Color> = {
  Major:      new THREE.Color('#ffe089'),   // warm gold
  Minor:      new THREE.Color('#79b8ff'),   // cool blue
  Dim:        new THREE.Color('#b48eff'),   // violet
  Aug:        new THREE.Color('#ff7d9c'),   // pink
  Dom7:       new THREE.Color('#ffae5e'),   // amber
  Maj7:       new THREE.Color('#cdeac0'),   // soft mint
  Min7:       new THREE.Color('#5ecbff'),   // sky
  Sus:        new THREE.Color('#c5b8ff'),   // pale lavender
};

/**
 * Scale-family base colour. Outer ring — pulls cooler / dimmer than
 * the chord ring on purpose so the eye reads scales as ambient context.
 */
export const SCALE_FAMILY_COLORS: Record<string, THREE.Color> = {
  Major:          new THREE.Color('#a8d8ff'),
  Minor:          new THREE.Color('#7fb1ff'),
  Modes:          new THREE.Color('#b39ddb'),
  HarmonicMinor:  new THREE.Color('#c792ea'),
  MelodicMinor:   new THREE.Color('#dba0e7'),
  Pentatonic:     new THREE.Color('#90e0c8'),
  Blues:          new THREE.Color('#ff9aa2'),
  Symmetric:      new THREE.Color('#ffd28a'),
};

/** Background — deep space gradient (top / bottom of the radial). */
export const SPACE_TOP = new THREE.Color('#0a0e2a');
export const SPACE_BOTTOM = new THREE.Color('#000003');
