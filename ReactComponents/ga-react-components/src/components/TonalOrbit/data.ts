/**
 * TonalOrbit — music-theory data model for the planetary system.
 *
 * Three orbital shells, anchored to a tonic at the centre:
 *
 *   1. PITCH ORBIT (12 bodies, fixed)
 *      The 12 pitch classes. Each is a "planet" rendered in circle-of-fifths
 *      order so adjacent planets share fifth-relations; colours come from
 *      `palette.ts`. Click a pitch to make it the tonic.
 *
 *   2. CHORD ORBIT (8 bodies per focused pitch)
 *      Chord families that "belong to" the focused pitch as their root:
 *      Maj, Min, Dim, Aug, Maj7, Min7, Dom7, Sus.
 *      Hidden until a pitch is focused; revealed in an orbit around it.
 *
 *   3. SCALE ORBIT (7 bodies per focused chord)
 *      Scale / modal families compatible with that chord (Major modes,
 *      melodic-minor modes, harmonic-minor modes, pentatonic / blues,
 *      symmetric scales). Hidden until a chord is focused.
 *
 * Why not the entire ~200-node hierarchy from the old Sunburst3DDemo?
 * Because the orbital metaphor breaks down past 3 shells (the outer
 * radius gets too large and the bodies become specks). 3 shells × 12 root
 * choices keeps every selectable body big enough to tap with a finger on
 * mobile, while still exposing 12 + 12×8 + 12×8×7 = 780 distinct items
 * via drill-down — comparable to the old tree's leaf count.
 */

import * as THREE from 'three';
import {
  PITCH_CLASS_NAMES,
  pitchClassColor,
  CHORD_FAMILY_COLORS,
  SCALE_FAMILY_COLORS,
} from './palette';

// ──────────────────────────────────────────────────────────────────────
// Pitch (inner orbit, 12 bodies)
// ──────────────────────────────────────────────────────────────────────

export interface PitchBody {
  kind: 'pitch';
  /** 0..11 chromatic */
  pc: number;
  /** "C", "C#", ... */
  name: string;
  /** MIDI note in the central octave, used by the audio drone. */
  midi: number;
  color: THREE.Color;
}

export const PITCH_BODIES: PitchBody[] = PITCH_CLASS_NAMES.map((name, pc) => ({
  kind: 'pitch',
  pc,
  name,
  midi: 60 + pc, // C4 = 60
  color: pitchClassColor(pc),
}));

// ──────────────────────────────────────────────────────────────────────
// Chord (mid orbit, 8 bodies per pitch)
// ──────────────────────────────────────────────────────────────────────

/**
 * A chord family — characterised by its intervals (semitones above the
 * root) and a short suffix. The intervals also drive the audio drone's
 * "third + fifth + seventh" voicing when this chord is focused.
 */
export interface ChordFamily {
  key: string;          // map key — "Major", "Min7", ...
  suffix: string;       // displayed alongside the root — "", "m", "maj7", ...
  intervals: number[];  // semitones from root, including the root (0)
  baseColor: THREE.Color;
  display: string;      // human-readable — "Major", "Minor", "Dim7", ...
}

export const CHORD_FAMILIES: ChordFamily[] = [
  { key: 'Major', display: 'Major',    suffix: '',    intervals: [0, 4, 7],       baseColor: CHORD_FAMILY_COLORS.Major },
  { key: 'Minor', display: 'Minor',    suffix: 'm',   intervals: [0, 3, 7],       baseColor: CHORD_FAMILY_COLORS.Minor },
  { key: 'Maj7',  display: 'Maj7',     suffix: 'maj7',intervals: [0, 4, 7, 11],   baseColor: CHORD_FAMILY_COLORS.Maj7  },
  { key: 'Min7',  display: 'Min7',     suffix: 'm7',  intervals: [0, 3, 7, 10],   baseColor: CHORD_FAMILY_COLORS.Min7  },
  { key: 'Dom7',  display: 'Dom7',     suffix: '7',   intervals: [0, 4, 7, 10],   baseColor: CHORD_FAMILY_COLORS.Dom7  },
  { key: 'Dim',   display: 'Dim',      suffix: '°',   intervals: [0, 3, 6],       baseColor: CHORD_FAMILY_COLORS.Dim   },
  { key: 'Aug',   display: 'Aug',      suffix: '+',   intervals: [0, 4, 8],       baseColor: CHORD_FAMILY_COLORS.Aug   },
  { key: 'Sus',   display: 'Sus4',     suffix: 'sus4',intervals: [0, 5, 7],       baseColor: CHORD_FAMILY_COLORS.Sus   },
];

export interface ChordBody {
  kind: 'chord';
  family: ChordFamily;
  /** The root pitch from the inner orbit this chord hangs off. */
  rootPc: number;
  /** Display name with root included — "Cmaj7", "Em", "G7". */
  displayName: string;
  color: THREE.Color;
}

export function chordsForPitch(pitch: PitchBody): ChordBody[] {
  return CHORD_FAMILIES.map((family) => ({
    kind: 'chord',
    family,
    rootPc: pitch.pc,
    displayName: `${pitch.name}${family.suffix}`,
    // Tint the family colour by the root colour so the chord ring reads
    // both as "this chord type" and "this root" without losing chroma.
    color: family.baseColor.clone().lerp(pitch.color, 0.35),
  }));
}

// ──────────────────────────────────────────────────────────────────────
// Scale (outer orbit, 7 bodies per chord)
// ──────────────────────────────────────────────────────────────────────

/**
 * A scale family — defined by its semitone intervals from the root,
 * a human display name, and a family bucket for colouring. The semitone
 * pattern feeds the optional pulse effect on the focused scale.
 */
export interface ScaleFamily {
  key: string;
  display: string;
  intervals: number[]; // semitones from root, 0..11
  familyBucket: keyof typeof SCALE_FAMILY_COLORS;
}

export const ALL_SCALES: ScaleFamily[] = [
  // Major modes
  { key: 'ionian',     display: 'Ionian',          intervals: [0, 2, 4, 5, 7, 9, 11], familyBucket: 'Modes' },
  { key: 'dorian',     display: 'Dorian',          intervals: [0, 2, 3, 5, 7, 9, 10], familyBucket: 'Modes' },
  { key: 'phrygian',   display: 'Phrygian',        intervals: [0, 1, 3, 5, 7, 8, 10], familyBucket: 'Modes' },
  { key: 'lydian',     display: 'Lydian',          intervals: [0, 2, 4, 6, 7, 9, 11], familyBucket: 'Modes' },
  { key: 'mixolydian', display: 'Mixolydian',      intervals: [0, 2, 4, 5, 7, 9, 10], familyBucket: 'Modes' },
  { key: 'aeolian',    display: 'Aeolian',         intervals: [0, 2, 3, 5, 7, 8, 10], familyBucket: 'Modes' },
  { key: 'locrian',    display: 'Locrian',         intervals: [0, 1, 3, 5, 6, 8, 10], familyBucket: 'Modes' },
  // Minor variants
  { key: 'harm-minor', display: 'Harmonic Minor',  intervals: [0, 2, 3, 5, 7, 8, 11], familyBucket: 'HarmonicMinor' },
  { key: 'mel-minor',  display: 'Melodic Minor',   intervals: [0, 2, 3, 5, 7, 9, 11], familyBucket: 'MelodicMinor' },
  { key: 'phr-dom',    display: 'Phrygian Dom.',   intervals: [0, 1, 4, 5, 7, 8, 10], familyBucket: 'HarmonicMinor' },
  { key: 'lyd-dom',    display: 'Lydian Dom.',     intervals: [0, 2, 4, 6, 7, 9, 10], familyBucket: 'MelodicMinor' },
  { key: 'altered',    display: 'Altered',         intervals: [0, 1, 3, 4, 6, 8, 10], familyBucket: 'MelodicMinor' },
  // Pentatonic / blues
  { key: 'pent-maj',   display: 'Major Pentatonic',intervals: [0, 2, 4, 7, 9],         familyBucket: 'Pentatonic' },
  { key: 'pent-min',   display: 'Minor Pentatonic',intervals: [0, 3, 5, 7, 10],        familyBucket: 'Pentatonic' },
  { key: 'blues',      display: 'Blues',           intervals: [0, 3, 5, 6, 7, 10],     familyBucket: 'Blues' },
  // Symmetric
  { key: 'whole-tone', display: 'Whole Tone',      intervals: [0, 2, 4, 6, 8, 10],     familyBucket: 'Symmetric' },
  { key: 'dim-wh',     display: 'Dim (W-H)',       intervals: [0, 2, 3, 5, 6, 8, 9, 11], familyBucket: 'Symmetric' },
  { key: 'dim-hw',     display: 'Dim (H-W)',       intervals: [0, 1, 3, 4, 6, 7, 9, 10], familyBucket: 'Symmetric' },
];

export interface ScaleBody {
  kind: 'scale';
  scale: ScaleFamily;
  rootPc: number;
  displayName: string;
  color: THREE.Color;
}

/**
 * Pick the 7 most relevant scales for a given chord family. The choice
 * is a deliberate curation — we don't dump all 18 scales into the outer
 * ring because that would put 18 tiny bodies on top of each other.
 *
 * Curation rules:
 *  - Major-type chords → Ionian, Lydian, Major Pentatonic, Mixolydian, Lydian Dom, Whole Tone, Blues
 *  - Minor-type chords → Aeolian, Dorian, Phrygian, Minor Pentatonic, Harmonic Minor, Melodic Minor, Blues
 *  - Dom7 → Mixolydian, Lydian Dom, Altered, Phrygian Dom, Blues, Whole Tone, Dim (H-W)
 *  - Dim / Min7b5 → Locrian, Dim (W-H), Dim (H-W), Phrygian, Altered, Harmonic Minor, Aeolian
 *  - Aug → Whole Tone, Lydian Aug equivalent (Mel Minor mode), Altered, Lydian Dom, Major, Ionian, Mixolydian
 *  - Sus → Mixolydian, Dorian, Major Pentatonic, Minor Pentatonic, Ionian, Aeolian, Phrygian Dom
 */
const SCALE_KEYS_BY_CHORD: Record<string, string[]> = {
  Major: ['ionian', 'lydian', 'pent-maj', 'mixolydian', 'lyd-dom', 'whole-tone', 'blues'],
  Minor: ['aeolian', 'dorian', 'phrygian', 'pent-min', 'harm-minor', 'mel-minor', 'blues'],
  Maj7:  ['ionian', 'lydian', 'pent-maj', 'mixolydian', 'lyd-dom', 'whole-tone', 'blues'],
  Min7:  ['aeolian', 'dorian', 'phrygian', 'pent-min', 'harm-minor', 'mel-minor', 'blues'],
  Dom7:  ['mixolydian', 'lyd-dom', 'altered', 'phr-dom', 'blues', 'whole-tone', 'dim-hw'],
  Dim:   ['locrian', 'dim-wh', 'dim-hw', 'phrygian', 'altered', 'harm-minor', 'aeolian'],
  Aug:   ['whole-tone', 'mel-minor', 'altered', 'lyd-dom', 'lydian', 'ionian', 'mixolydian'],
  Sus:   ['mixolydian', 'dorian', 'pent-maj', 'pent-min', 'ionian', 'aeolian', 'phr-dom'],
};

export function scalesForChord(chord: ChordBody): ScaleBody[] {
  const keys = SCALE_KEYS_BY_CHORD[chord.family.key] ?? [];
  return keys
    .map((k) => ALL_SCALES.find((s) => s.key === k))
    .filter((s): s is ScaleFamily => Boolean(s))
    .map((scale) => {
      const rootName = PITCH_CLASS_NAMES[chord.rootPc];
      const familyColor = SCALE_FAMILY_COLORS[scale.familyBucket] ?? new THREE.Color('#9ad0ff');
      return {
        kind: 'scale',
        scale,
        rootPc: chord.rootPc,
        displayName: `${rootName} ${scale.display}`,
        // Same trick as chord: lerp 35% toward the root colour so the
        // ring reads "this scale family" and "this root" simultaneously.
        color: familyColor.clone().lerp(chord.color, 0.25),
      };
    });
}

// ──────────────────────────────────────────────────────────────────────
// Focus state — what the camera + audio are currently centred on
// ──────────────────────────────────────────────────────────────────────

export type Body = PitchBody | ChordBody | ScaleBody;

export interface FocusState {
  pitch: PitchBody | null;
  chord: ChordBody | null;
  scale: ScaleBody | null;
}

/** Depth of focus: 0 = tonic only, 1 = pitch focused, 2 = chord, 3 = scale. */
export function focusDepth(f: FocusState): number {
  if (f.scale) return 3;
  if (f.chord) return 2;
  if (f.pitch) return 1;
  return 0;
}

/** Build a breadcrumb-like path of display names for the current focus. */
export function focusPath(f: FocusState): string[] {
  const out: string[] = ['Tonal Orbit'];
  if (f.pitch) out.push(f.pitch.name);
  if (f.chord) out.push(f.chord.displayName);
  if (f.scale) out.push(f.scale.displayName);
  return out;
}
