/**
 * Generic Fretboard Mathematics
 * 
 * These functions work for ANY stringed instrument with frets.
 * They are based on the physics of vibrating strings and equal temperament.
 */

import type { InstrumentConfig } from '../types/InstrumentConfig';

/**
 * Calculate the position of a fret along the scale length
 * 
 * Uses the equal temperament formula:
 * position = scaleLength * (1 - 2^(-fret/12))
 * 
 * @param fretNumber - Fret number (0 = nut, 1 = first fret, etc.)
 * @param scaleLength - Scale length in mm
 * @returns Distance from nut to fret in mm
 */
export function calculateFretPosition(
  fretNumber: number,
  scaleLength: number
): number {
  if (fretNumber === 0) return 0;
  return scaleLength * (1 - Math.pow(2, -fretNumber / 12));
}

/**
 * Calculate the position of a fret in 3D space
 * (Used for Three.js rendering)
 * 
 * @param fretNumber - Fret number
 * @param scaleLength - Scale length in mm
 * @returns Position in 3D space (centered at origin)
 */
export function calculateFretPosition3D(
  fretNumber: number,
  scaleLength: number
): number {
  return calculateFretPosition(fretNumber, scaleLength);
}

/**
 * Calculate the spacing between strings at a given position
 * 
 * Strings are typically closer together at the nut and wider at the bridge.
 * This function interpolates between nut width and bridge width.
 * 
 * @param stringIndex - String index (0 = highest pitch string)
 * @param stringCount - Total number of strings
 * @param nutWidth - Width at the nut in mm
 * @param bridgeWidth - Width at the bridge in mm
 * @param position - Position along the neck (0 = nut, 1 = bridge)
 * @returns Distance from center line in mm
 */
export function calculateStringSpacing(
  stringIndex: number,
  stringCount: number,
  nutWidth: number,
  bridgeWidth: number,
  position: number
): number {
  // Interpolate width based on position
  const width = nutWidth + (bridgeWidth - nutWidth) * position;
  
  // Calculate spacing (centered at 0)
  // For n strings, we have n-1 gaps
  const normalizedPosition = stringIndex / (stringCount - 1);
  return (normalizedPosition - 0.5) * width;
}

/**
 * Calculate string spacing for a specific fret
 * 
 * @param stringIndex - String index
 * @param fretNumber - Fret number
 * @param instrument - Instrument configuration
 * @returns Distance from center line in mm
 */
export function calculateStringSpacingAtFret(
  stringIndex: number,
  fretNumber: number,
  instrument: InstrumentConfig
): number {
  const position = fretNumber === 0 
    ? 0 
    : calculateFretPosition(fretNumber, instrument.scaleLength) / instrument.scaleLength;
  
  return calculateStringSpacing(
    stringIndex,
    instrument.tuning.length,
    instrument.nutWidth,
    instrument.bridgeWidth,
    position
  );
}

/**
 * Calculate all fret positions for an instrument
 * 
 * @param instrument - Instrument configuration
 * @returns Array of fret positions in mm from nut
 */
export function calculateAllFretPositions(
  instrument: InstrumentConfig
): number[] {
  const positions: number[] = [];
  for (let i = 0; i <= instrument.fretCount; i++) {
    positions.push(calculateFretPosition(i, instrument.scaleLength));
  }
  return positions;
}

/**
 * Calculate which frets should have position marker inlays
 * 
 * Standard pattern: 3, 5, 7, 9, 12 (double), 15, 17, 19, 21, 24 (double)
 * 
 * @param fretCount - Total number of frets
 * @returns Array of fret numbers that should have inlays
 */
export function getInlayFrets(fretCount: number): number[] {
  const singleInlays = [3, 5, 7, 9, 15, 17, 19, 21];
  const doubleInlays = [12, 24];
  
  const inlays: number[] = [];
  
  // Add single inlays
  for (const fret of singleInlays) {
    if (fret <= fretCount) {
      inlays.push(fret);
    }
  }
  
  // Add double inlays
  for (const fret of doubleInlays) {
    if (fret <= fretCount) {
      inlays.push(fret);
    }
  }
  
  return inlays.sort((a, b) => a - b);
}

/**
 * Check if a fret should have a double inlay (12th, 24th)
 * 
 * @param fretNumber - Fret number
 * @returns True if this fret should have a double inlay
 */
export function isDoubleInlay(fretNumber: number): boolean {
  return fretNumber === 12 || fretNumber === 24;
}

/**
 * Calculate the width of a fret at a given position
 * 
 * Frets are typically wider near the nut and narrower near the bridge.
 * 
 * @param fretNumber - Fret number
 * @param instrument - Instrument configuration
 * @returns Fret width in mm
 */
export function calculateFretWidth(
  fretNumber: number,
  instrument: InstrumentConfig
): number {
  const position = fretNumber === 0 
    ? 0 
    : calculateFretPosition(fretNumber, instrument.scaleLength) / instrument.scaleLength;
  
  return instrument.nutWidth + (instrument.bridgeWidth - instrument.nutWidth) * position;
}

/**
 * Calculate the distance between two frets
 * 
 * @param fret1 - First fret number
 * @param fret2 - Second fret number
 * @param scaleLength - Scale length in mm
 * @returns Distance between frets in mm
 */
export function calculateFretDistance(
  fret1: number,
  fret2: number,
  scaleLength: number
): number {
  const pos1 = calculateFretPosition(fret1, scaleLength);
  const pos2 = calculateFretPosition(fret2, scaleLength);
  return Math.abs(pos2 - pos1);
}

/**
 * Convert a pitch name to MIDI note number
 * 
 * @param pitchName - Pitch name (e.g., "E2", "A#3", "Bb4")
 * @returns MIDI note number (0-127)
 */
export function pitchToMidi(pitchName: string): number {
  const noteNames = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
  const flatToSharp: Record<string, string> = {
    'Db': 'C#', 'Eb': 'D#', 'Gb': 'F#', 'Ab': 'G#', 'Bb': 'A#'
  };
  
  // Parse pitch name
  const match = pitchName.match(/^([A-G][#b]?)(-?\d+)$/);
  if (!match) {
    throw new Error(`Invalid pitch name: ${pitchName}`);
  }
  
  let noteName = match[1];
  const octave = parseInt(match[2], 10);
  
  // Convert flats to sharps
  if (noteName in flatToSharp) {
    noteName = flatToSharp[noteName];
  }
  
  const noteIndex = noteNames.indexOf(noteName);
  if (noteIndex === -1) {
    throw new Error(`Invalid note name: ${noteName}`);
  }
  
  return (octave + 1) * 12 + noteIndex;
}

/**
 * Calculate the pitch at a given string and fret
 * 
 * @param stringIndex - String index (0 = highest pitch)
 * @param fretNumber - Fret number (0 = open string)
 * @param instrument - Instrument configuration
 * @returns MIDI note number
 */
export function calculatePitch(
  stringIndex: number,
  fretNumber: number,
  instrument: InstrumentConfig
): number {
  const openPitch = pitchToMidi(instrument.tuning[stringIndex]);
  return openPitch + fretNumber;
}

/**
 * Calculate the frequency of a pitch
 * 
 * @param midiNote - MIDI note number
 * @returns Frequency in Hz
 */
export function midiToFrequency(midiNote: number): number {
  // A4 (MIDI 69) = 440 Hz
  return 440 * Math.pow(2, (midiNote - 69) / 12);
}

/**
 * Calculate the frequency at a given string and fret
 * 
 * @param stringIndex - String index
 * @param fretNumber - Fret number
 * @param instrument - Instrument configuration
 * @returns Frequency in Hz
 */
export function calculateFrequency(
  stringIndex: number,
  fretNumber: number,
  instrument: InstrumentConfig
): number {
  const midiNote = calculatePitch(stringIndex, fretNumber, instrument);
  return midiToFrequency(midiNote);
}

/**
 * Calculate the optimal viewport dimensions for rendering
 * 
 * @param instrument - Instrument configuration
 * @param renderMode - Rendering mode
 * @returns Recommended width and height in pixels
 */
export function calculateViewportDimensions(
  instrument: InstrumentConfig,
  renderMode: '2d-svg' | '2d-canvas' | '3d-webgl' | '3d-webgpu'
): { width: number; height: number } {
  const stringCount = instrument.tuning.length;
  const fretCount = instrument.fretCount;
  
  if (renderMode.startsWith('3d')) {
    // 3D rendering - use aspect ratio based on scale length
    return {
      width: 800,
      height: 600
    };
  } else {
    // 2D rendering - calculate based on fret count and string count
    const fretWidth = 60; // pixels per fret
    const stringHeight = 40; // pixels per string
    
    return {
      width: Math.max(600, fretCount * fretWidth),
      height: Math.max(300, stringCount * stringHeight)
    };
  }
}

