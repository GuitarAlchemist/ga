import { FretboardPosition } from '../types/fretboard.types';

/**
 * Mock data for demonstrating different fretboard visualizations
 * 
 * In a real application, this data would come from the backend API
 * which performs all music theory calculations.
 */

/**
 * C Major chord (open position)
 */
export const cMajorChordPositions: FretboardPosition[] = [
  { string: 0, fret: 0, label: 'E', color: '#4CAF50' }, // High E (open)
  { string: 1, fret: 1, label: 'C', color: '#2196F3', emphasized: true }, // B string, 1st fret (root)
  { string: 2, fret: 0, label: 'G', color: '#4CAF50' }, // G (open)
  { string: 3, fret: 2, label: 'E', color: '#4CAF50' }, // D string, 2nd fret
  { string: 4, fret: 3, label: 'C', color: '#2196F3', emphasized: true }, // A string, 3rd fret (root)
  // Low E string is muted (not included)
];

/**
 * G Major chord (open position)
 */
export const gMajorChordPositions: FretboardPosition[] = [
  { string: 0, fret: 3, label: 'G', color: '#2196F3', emphasized: true }, // High E, 3rd fret (root)
  { string: 1, fret: 0, label: 'B', color: '#4CAF50' }, // B (open)
  { string: 2, fret: 0, label: 'G', color: '#2196F3', emphasized: true }, // G (open, root)
  { string: 3, fret: 0, label: 'D', color: '#4CAF50' }, // D (open)
  { string: 4, fret: 2, label: 'B', color: '#4CAF50' }, // A string, 2nd fret
  { string: 5, fret: 3, label: 'G', color: '#2196F3', emphasized: true }, // Low E, 3rd fret (root)
];

/**
 * C Major scale (one octave, starting from 3rd fret)
 */
export const cMajorScalePositions: FretboardPosition[] = [
  // C Major scale pattern (CDEFGABC)
  { string: 4, fret: 3, label: 'C', color: '#2196F3', emphasized: true }, // Root
  { string: 4, fret: 5, label: 'D', color: '#9C27B0' },
  { string: 3, fret: 2, label: 'E', color: '#9C27B0' },
  { string: 3, fret: 3, label: 'F', color: '#9C27B0' },
  { string: 3, fret: 5, label: 'G', color: '#9C27B0' },
  { string: 2, fret: 2, label: 'A', color: '#9C27B0' },
  { string: 2, fret: 4, label: 'B', color: '#9C27B0' },
  { string: 2, fret: 5, label: 'C', color: '#2196F3', emphasized: true }, // Octave
];

/**
 * A Minor Pentatonic scale (box pattern 1)
 */
export const aMinorPentatonicPositions: FretboardPosition[] = [
  // 5th position box pattern
  { string: 0, fret: 5, label: 'A', color: '#2196F3', emphasized: true },
  { string: 0, fret: 8, label: 'C', color: '#FF9800' },
  { string: 1, fret: 5, label: 'G', color: '#FF9800' },
  { string: 1, fret: 8, label: 'A', color: '#2196F3', emphasized: true },
  { string: 2, fret: 5, label: 'D', color: '#FF9800' },
  { string: 2, fret: 7, label: 'E', color: '#FF9800' },
  { string: 3, fret: 5, label: 'A', color: '#2196F3', emphasized: true },
  { string: 3, fret: 7, label: 'C', color: '#FF9800' },
  { string: 4, fret: 5, label: 'E', color: '#FF9800' },
  { string: 4, fret: 7, label: 'G', color: '#FF9800' },
  { string: 5, fret: 5, label: 'A', color: '#2196F3', emphasized: true },
  { string: 5, fret: 8, label: 'C', color: '#FF9800' },
];

/**
 * D Dorian mode (7th position)
 */
export const dDorianModePositions: FretboardPosition[] = [
  // D Dorian mode pattern
  { string: 0, fret: 7, label: 'B', color: '#E91E63' },
  { string: 0, fret: 8, label: 'C', color: '#E91E63' },
  { string: 0, fret: 10, label: 'D', color: '#2196F3', emphasized: true },
  { string: 1, fret: 6, label: 'F', color: '#E91E63' },
  { string: 1, fret: 8, label: 'G', color: '#E91E63' },
  { string: 1, fret: 10, label: 'A', color: '#E91E63' },
  { string: 2, fret: 7, label: 'D', color: '#2196F3', emphasized: true },
  { string: 2, fret: 9, label: 'E', color: '#E91E63' },
  { string: 2, fret: 10, label: 'F', color: '#E91E63' },
  { string: 3, fret: 7, label: 'A', color: '#E91E63' },
  { string: 3, fret: 9, label: 'B', color: '#E91E63' },
  { string: 3, fret: 10, label: 'C', color: '#E91E63' },
];

/**
 * C Major arpeggio (across the fretboard)
 */
export const cMajorArpeggioPositions: FretboardPosition[] = [
  // C Major triad (C-E-G) across multiple positions
  { string: 4, fret: 3, label: 'C', color: '#2196F3', emphasized: true },
  { string: 3, fret: 2, label: 'E', color: '#00BCD4' },
  { string: 2, fret: 0, label: 'G', color: '#00BCD4' },
  { string: 2, fret: 5, label: 'C', color: '#2196F3', emphasized: true },
  { string: 1, fret: 5, label: 'E', color: '#00BCD4' },
  { string: 0, fret: 3, label: 'G', color: '#00BCD4' },
  { string: 0, fret: 8, label: 'C', color: '#2196F3', emphasized: true },
];

/**
 * E Minor arpeggio (sweep picking pattern)
 */
export const eMinorArpeggioPositions: FretboardPosition[] = [
  { string: 5, fret: 0, label: 'E', color: '#2196F3', emphasized: true },
  { string: 4, fret: 2, label: 'B', color: '#00BCD4' },
  { string: 3, fret: 2, label: 'E', color: '#2196F3', emphasized: true },
  { string: 2, fret: 0, label: 'G', color: '#00BCD4' },
  { string: 1, fret: 0, label: 'B', color: '#00BCD4' },
  { string: 0, fret: 0, label: 'E', color: '#2196F3', emphasized: true },
];

/**
 * All mock data organized by display mode
 */
export const mockDataByMode = {
  chord: {
    'C Major': cMajorChordPositions,
    'G Major': gMajorChordPositions,
  },
  scale: {
    'C Major Scale': cMajorScalePositions,
    'A Minor Pentatonic': aMinorPentatonicPositions,
  },
  mode: {
    'D Dorian': dDorianModePositions,
  },
  arpeggio: {
    'C Major Arpeggio': cMajorArpeggioPositions,
    'E Minor Arpeggio': eMinorArpeggioPositions,
  },
};

