/**
 * Fretboard Mathematics
 * Realistic fret positioning and string layout
 */

export interface Scale {
  scaleLengthMM: number;
  nutWidthMM: number;
  bridgeWidthMM: number;
}

// Standard guitar scales
export const SCALES = {
  classical: { scaleLengthMM: 650, nutWidthMM: 52, bridgeWidthMM: 62 },
  acoustic: { scaleLengthMM: 645, nutWidthMM: 43, bridgeWidthMM: 55 },
  electric: { scaleLengthMM: 648, nutWidthMM: 42, bridgeWidthMM: 52 },
  bass: { scaleLengthMM: 864, nutWidthMM: 45, bridgeWidthMM: 72 },
};

/**
 * Calculate fret position in mm from nut
 * Uses the 12th root of 2 formula: distance = scale * (1 - 2^(-fret/12))
 */
export function fretPositionMM(fretNumber: number, scaleLengthMM: number): number {
  if (fretNumber === 0) return 0;
  const distanceFromNut = scaleLengthMM - scaleLengthMM / Math.pow(2, fretNumber / 12);
  return distanceFromNut;
}

/**
 * Calculate string X position given string number
 * Distributes strings evenly across the fretboard width
 */
export function stringX(
  stringNumber: number,
  totalStrings: number,
  widthMM: number,
  edgeMM: number = 3
): number {
  const innerWidth = widthMM - edgeMM * 2;
  return edgeMM + (stringNumber * innerWidth) / (totalStrings - 1);
}

/**
 * Get string gauge in mm for standard tuning
 * E-B-G-D-A-E (inches converted to mm)
 */
export function getStringGauge(stringNumber: number, isWound: boolean = false): number {
  const gaugesInches = [0.046, 0.036, 0.026, 0.017, 0.013, 0.010];
  const mmPerInch = 25.4;
  
  if (stringNumber < 0 || stringNumber >= gaugesInches.length) {
    return 0.013 * mmPerInch;
  }
  
  return gaugesInches[stringNumber] * mmPerInch;
}

/**
 * Determine if a string is wound (E, A, D strings typically)
 */
export function isStringWound(stringNumber: number): boolean {
  return stringNumber <= 2; // E, A, D strings
}

/**
 * Calculate fretboard dimensions in pixels
 */
export function calculateFretboardDimensions(
  scale: Scale,
  dpi: number = 4,
  fretCount: number = 22
): { width: number; height: number; visibleLength: number } {
  // Visible fretboard length (chevalet hors cadre)
  const visibleLength = scale.scaleLengthMM * 0.71;
  
  // Width at 12th fret (average of nut and bridge)
  const widthAt12 = (scale.nutWidthMM + scale.bridgeWidthMM) / 2;
  
  const width = Math.round(visibleLength * dpi);
  const height = Math.round(widthAt12 * dpi * 1.1); // 10% extra for margins
  
  return { width, height, visibleLength };
}

/**
 * Get fret marker positions (dots on fretboard)
 */
export function getFretMarkers(fretCount: number): number[] {
  const markers = [3, 5, 7, 9, 12, 15, 17, 19, 21];
  return markers.filter(f => f <= fretCount);
}

/**
 * Calculate fret spacing for schematic (linear) mode
 */
export function calculateSchematicFretPosition(
  fretNumber: number,
  totalFrets: number,
  availableWidth: number
): number {
  return (fretNumber / totalFrets) * availableWidth;
}

/**
 * Calculate fret spacing for realistic (logarithmic) mode
 */
export function calculateRealisticFretPosition(
  fretNumber: number,
  scaleLengthMM: number,
  dpi: number,
  visibleLengthMM: number
): number {
  const positionMM = fretPositionMM(fretNumber, scaleLengthMM) * (visibleLengthMM / scaleLengthMM);
  return Math.round(positionMM * dpi);
}

