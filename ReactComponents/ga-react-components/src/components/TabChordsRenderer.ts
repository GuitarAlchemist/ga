import Vex from 'vexflow';

/** String number (1 = high E, 6 = low E) and fret */
export interface TabChordPosition {
  str: number;
  fret: number;
}

/**
 * Draws chords (or single notes) on a tab stave with VexFlow, one after the other, into
 * `container`, replacing what was there. Used by ga-client's chat to draw `vextab` blocks.
 */
export function renderTabChords(container: HTMLElement, chords: TabChordPosition[][]): void {
  const VF = Vex.Flow;
  container.innerHTML = '';

  const width = Math.max(260, 80 + chords.length * 60);
  const renderer = new VF.Renderer(container as HTMLDivElement, VF.Renderer.Backends.SVG);
  // Six lines 13 px apart, one line space above and below: 13 + 65 + 13, plus the frets' text.
  renderer.resize(width, 100);
  const context = renderer.getContext();

  const stave = new VF.TabStave(10, 0, width - 20, { space_above_staff_ln: 1, space_below_staff_ln: 1 });
  stave.addClef('tab').setContext(context).draw();

  // A little room between the clef and the first chord
  stave.setNoteStartX(stave.getNoteStartX() + 12);

  const notes = chords.map(
    (positions) => new VF.TabNote({ positions: positions.map((p) => ({ str: p.str, fret: p.fret })), duration: 'q' }),
  );
  const voice = new VF.Voice({ num_beats: notes.length, beat_value: 4 });
  voice.addTickables(notes);
  // formatToStave starts the notes after the clef
  new VF.Formatter().joinVoices([voice]).formatToStave([voice], stave);
  voice.draw(context, stave);
}
