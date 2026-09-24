/**
 * Chord voicing data and scoring helpers for the IX Hand Voicing Lab.
 */

export interface ChordVoicing {
  name: string;
  /** null = muted, 0 = open, 1-12 = fret */
  frets: (number | null)[];
  /** Optional suggested finger per string (1-4, 0 = open/muted) */
  fingers?: number[];
}

export const STANDARD_TUNING = ['E', 'A', 'D', 'G', 'B', 'E'];

export const CHORDS: ChordVoicing[] = [
  { name: 'C major', frets: [null, 3, 2, 0, 1, 0], fingers: [0, 3, 2, 0, 1, 0] },
  { name: 'G major', frets: [3, 2, 0, 0, 0, 3], fingers: [2, 1, 0, 0, 0, 3] },
  { name: 'D major', frets: [null, null, 0, 2, 3, 2], fingers: [0, 0, 0, 1, 3, 2] },
  { name: 'A major', frets: [null, 0, 2, 2, 2, 0], fingers: [0, 0, 1, 2, 3, 0] },
  { name: 'E major', frets: [0, 2, 2, 1, 0, 0], fingers: [0, 2, 3, 1, 0, 0] },
  { name: 'A minor', frets: [null, 0, 2, 2, 1, 0], fingers: [0, 0, 2, 3, 1, 0] },
  { name: 'E minor', frets: [0, 2, 2, 0, 0, 0], fingers: [0, 2, 3, 0, 0, 0] },
];

/**
 * MediaPipe hand landmark indices for fingertips.
 */
export const FINGERTIP_INDICES = [4, 8, 12, 16, 20];

/**
 * Convert a normalized hand point (x, y) to a (string, fret) pair on a 6x12
 * fretboard. The camera feed is mirrored, so x is flipped.
 */
export function mapLandmarkToFretboard(x: number, y: number): {
  stringIndex: number;
  fretIndex: number | null;
} {
  const mirroredX = 1 - x;
  const stringIndex = Math.min(5, Math.max(0, Math.floor(mirroredX * 6)));
  // y is top-to-bottom in MediaPipe; map to fret 0 at top, 12 at bottom.
  const fretIndex = Math.min(12, Math.max(0, Math.floor(y * 12)));
  return { stringIndex, fretIndex };
}

/**
 * Score how well detected fret contacts match a target chord.
 * Returns a value from 0 to 100.
 */
export function scoreVoicing(
  detected: (number | null)[],
  target: ChordVoicing,
): number {
  let points = 0;
  let total = 0;
  for (let i = 0; i < 6; i++) {
    const targetFret = target.frets[i];
    if (targetFret === null) {
      // Muted string: point if not pressed.
      if (detected[i] === null) points += 1;
      total += 1;
      continue;
    }
    if (targetFret === 0) {
      // Open string: point if not pressed (or pressed at fret 0).
      if (detected[i] === null || detected[i] === 0) points += 1;
      total += 1;
      continue;
    }
    // Fretted string: point if pressed within ±1 fret.
    const d = detected[i];
    if (d !== null && d !== 0 && Math.abs(d - targetFret) <= 1) {
      points += 1 - Math.abs(d - targetFret) * 0.5;
    }
    total += 1;
  }
  return Math.round((points / total) * 100);
}

/**
 * Build a readable label for a detected fret, e.g. "x-3-2-0-1-0" for C major.
 */
export function fretsToString(frets: (number | null)[]): string {
  return frets.map((f) => (f === null ? 'x' : String(f))).join('-');
}
