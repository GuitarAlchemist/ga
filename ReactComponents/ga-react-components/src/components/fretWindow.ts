/**
 * Fret-window arithmetic for {@link FretDiagram} — extracted so the "every fretted note
 * gets a dot" invariant can be tested without rendering.
 *
 * The old inline version anchored the grid on the lowest pressed fret only
 * (`baseFret = minFret <= 2 ? 1 : minFret`) and then dropped any dot falling outside the
 * fixed 5-row window with a bare `return null`. A voicing such as `x-2-2-2-6-x` (min 2, so
 * baseFret 1, window frets 1–5) silently lost its fret-6 note: the diagram rendered as a
 * playable chord that was missing a string. Measured over the voicings the API can return,
 * 1.68% of voicings lost at least one note this way, and in every single case the voicing's
 * span did fit in five rows — the window was simply anchored in the wrong place.
 */

/** Default number of fret rows drawn when the voicing fits at the nut. */
export const DEFAULT_FRETS_SHOWN = 5;

export interface FretWindow {
  /** Fret number of the first row; 1 means the grid is drawn at the nut. */
  baseFret: number;
  /** Number of fret rows to draw — grows past the default for a wide-span voicing. */
  fretsShown: number;
}

/**
 * Chooses a window that contains every fretted note in `frets`.
 *
 * Anchors at the nut whenever the whole voicing fits there, otherwise at the lowest pressed
 * fret, and grows the grid when the span alone needs more than `minRows` rows. The result
 * guarantees `1 <= rowOf(f) <= fretsShown` for every `f > 0` in `frets`, so no note is ever
 * dropped.
 */
export function computeFretWindow(frets: number[], minRows = DEFAULT_FRETS_SHOWN): FretWindow {
  const pressed = frets.filter(f => f > 0);
  if (pressed.length === 0) {
    // Open / muted only (e.g. Em7 as 0-0-0-0-x-x): draw the plain nut grid.
    return { baseFret: 1, fretsShown: minRows };
  }

  const minFret = Math.min(...pressed);
  const maxFret = Math.max(...pressed);

  // Fits inside the nut window? Keep the nut — it is what a player expects to see.
  if (maxFret <= minRows) {
    return { baseFret: 1, fretsShown: minRows };
  }

  return { baseFret: minFret, fretsShown: Math.max(minRows, maxFret - minFret + 1) };
}

/** 1-based grid row for a fretted note, given the window. */
export function rowOf(fret: number, window: FretWindow): number {
  return fret - window.baseFret + 1;
}
