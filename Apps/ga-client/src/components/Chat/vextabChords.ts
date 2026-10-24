/**
 * Reads the `vextab` blocks the chatbot writes into positions VexFlow can draw.
 *
 * Two forms are read:
 * - VexTab (https://vexflow.com/vextab/tutorial.html), what the chatbot writes now:
 *   `tabstave` then `notes :w (3/5.2/4.0/3.1/2.0/1)`, fret first. Only the part of VexTab
 *   the chatbot uses is read: chords, single `fret/string` notes, durations and bar lines.
 * - The token list older answers carry, `5/3 4/2 3/0`, string first, with no stave.
 *
 * Anything else (techniques, annotations, standard notes...) returns null, so that the caller
 * shows the text rather than a drawing that leaves part of it out.
 */

export interface TabPosition {
  /** String number, 1 = high E, 6 = low E */
  str: number;
  fret: number;
}

/** Positions played together: a chord, or a single note */
export type TabChord = TabPosition[];

export interface ParsedChatTab {
  format: 'vextab' | 'ga-tokens';
  chords: TabChord[];
}

const MAX_STRING = 6;
const MAX_FRET = 36;

function position(str: number, fret: number): TabPosition | null {
  return Number.isInteger(str) && Number.isInteger(fret) && str >= 1 && str <= MAX_STRING && fret >= 0 && fret <= MAX_FRET
    ? { str, fret }
    : null;
}

/** `3/5` in VexTab: fret 3 on string 5 */
function vexTabNote(token: string): TabPosition | null {
  const match = /^(\d{1,2})\/(\d)$/.exec(token);
  return match ? position(Number(match[2]), Number(match[1])) : null;
}

function vexTabChord(token: string): TabChord | null {
  const match = /^\((.+)\)$/.exec(token);
  const notes = (match ? match[1].split('.') : [token]).map(vexTabNote);
  return notes.length > 0 && notes.every((n): n is TabPosition => n !== null) ? notes : null;
}

const DURATION = /^:(w|h|q|8|16|32)S?d?$/;
const BAR = /^(\||=\|\||=\|=|=:\||=\|:|=::)$/;

function parseVexTab(lines: string[]): ParsedChatTab | null {
  const chords: TabChord[] = [];
  for (const line of lines) {
    if (/^(tabstave|options)(\s|$)/.test(line)) {
      continue;
    }
    const notes = /^notes(?=\s|:|$)(.*)$/.exec(line);
    if (!notes) {
      return null;
    }
    for (const token of notes[1].trim().split(/\s+/).filter(Boolean)) {
      if (DURATION.test(token) || BAR.test(token)) {
        continue;
      }
      const chord = vexTabChord(token);
      if (!chord) {
        return null;
      }
      chords.push(chord);
    }
  }
  return chords.length > 0 ? { format: 'vextab', chords } : null;
}

/** `5/3` in the old token list: string 5, fret 3; one note per token */
function parseGaTokens(lines: string[]): ParsedChatTab | null {
  const chords: TabChord[] = [];
  for (const token of lines.join(' ').split(/\s+/).filter(Boolean)) {
    const match = /^(\d)\/(\d{1,2})$/.exec(token);
    const pos = match ? position(Number(match[1]), Number(match[2])) : null;
    if (!pos) {
      return null;
    }
    chords.push([pos]);
  }
  return chords.length > 0 ? { format: 'ga-tokens', chords } : null;
}

export function parseChatVexTab(text: string): ParsedChatTab | null {
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
  if (lines.length === 0) {
    return null;
  }
  return lines.some((line) => /^(tabstave|notes)(?=\s|:|$)/.test(line)) ? parseVexTab(lines) : parseGaTokens(lines);
}
