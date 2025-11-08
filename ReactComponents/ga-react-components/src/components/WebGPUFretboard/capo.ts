/**
 * Capo geometry and logic
 */
import { fretXmm } from './spacing';

export interface CapoTransform {
  mmOffset: number;      // Physical offset in mm
  semitoneOffset: number; // Transposition amount
}

/**
 * Calculate capo transform
 * @param scaleLengthMM - Scale length in mm
 * @param capoFret - Capo position (0 = no capo)
 */
export function capoTransform(
  scaleLengthMM: number,
  capoFret: number
): CapoTransform {
  return {
    mmOffset: fretXmm(capoFret, scaleLengthMM),
    semitoneOffset: capoFret,
  };
}

/**
 * Transpose a note by semitones
 */
export function transposeNote(note: string, semitones: number): string {
  const notes = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
  const index = notes.indexOf(note.toUpperCase());
  if (index === -1) return note;
  
  const newIndex = (index + semitones) % 12;
  return notes[newIndex < 0 ? newIndex + 12 : newIndex];
}

