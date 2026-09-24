// fretWindow.test — pins the invariant the old inline window math broke: every fretted
// note in a voicing gets a row inside the grid. The regression cases below are voicings the
// previous `baseFret = minFret <= 2 ? 1 : minFret` rule pushed outside the fixed 5-row
// window, where FretDiagram dropped them with a bare `return null`.

import { describe, it, expect } from 'vitest';
import { computeFretWindow, rowOf, DEFAULT_FRETS_SHOWN } from './fretWindow';

/** Frets that would not be drawn for the window this voicing produces. */
function droppedNotes(frets: number[]): number[] {
  const w = computeFretWindow(frets);
  return frets.filter(f => f > 0).filter(f => {
    const r = rowOf(f, w);
    return r < 1 || r > w.fretsShown;
  });
}

describe('computeFretWindow', () => {
  it('keeps the nut window for an open-position chord', () => {
    // Open C major, low-to-high.
    expect(computeFretWindow([-1, 3, 2, 0, 1, 0])).toEqual({ baseFret: 1, fretsShown: 5 });
  });

  it('keeps the plain nut grid when nothing is fretted', () => {
    expect(computeFretWindow([0, 0, 0, 0, -1, -1])).toEqual({ baseFret: 1, fretsShown: 5 });
  });

  it('shifts the window up for a chord above the nut', () => {
    // 8th-position barre: min 8, max 10.
    expect(computeFretWindow([8, 10, 10, 9, 8, 8])).toEqual({ baseFret: 8, fretsShown: 5 });
  });

  // ── Regression: the silent-drop cases ────────────────────────────────────
  // minFret <= 2 forced baseFret = 1, so the window was frets 1..5 and every note
  // above fret 5 vanished.
  it.each([
    [[-1, 2, 2, 2, 6, -1], 6],
    [[2, 2, 2, 6, -1, -1], 6],
    [[1, 3, 3, 3, 6, 1], 6],
    [[2, 4, 4, 4, 6, 2], 6],
  ])('draws every note of %j (fret %i used to be dropped)', (frets, lost) => {
    expect(droppedNotes(frets as number[])).toEqual([]);

    const w = computeFretWindow(frets as number[]);
    const r = rowOf(lost as number, w);
    expect(r).toBeGreaterThanOrEqual(1);
    expect(r).toBeLessThanOrEqual(w.fretsShown);
  });

  it('grows the grid when the span exceeds the default height', () => {
    // A 7-fret stretch cannot fit in five rows; the grid must get taller, not lossy.
    const w = computeFretWindow([1, -1, -1, -1, 5, 8]);
    expect(w.baseFret).toBe(1);
    expect(w.fretsShown).toBeGreaterThanOrEqual(8);
    expect(droppedNotes([1, -1, -1, -1, 5, 8])).toEqual([]);
  });

  it('never drops a note across an exhaustive sweep of 5-fret-span voicings', () => {
    // Every voicing the index can hold: 6 strings, window span <= 4, frets 0..22.
    let checked = 0;
    for (let base = 0; base <= 18; base++) {
      for (let a = base; a <= base + 4; a++) {
        for (let b = base; b <= base + 4; b++) {
          const frets = [a, -1, 0, b, -1, 0];
          expect(droppedNotes(frets)).toEqual([]);
          checked++;
        }
      }
    }
    expect(checked).toBeGreaterThan(400);
  });

  it('exposes the default row count it was built with', () => {
    expect(DEFAULT_FRETS_SHOWN).toBe(5);
  });
});
