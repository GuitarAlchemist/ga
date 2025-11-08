/**
 * Physical spacing calculations (all in millimeters)
 * Following the 12-TET (12-tone equal temperament) formula
 */

/**
 * Calculate fret position in mm from nut
 * Formula: distance = scaleLength - (scaleLength / 2^(fret/12))
 */
export function fretXmm(fretNumber: number, scaleLengthMM: number): number {
  return scaleLengthMM - scaleLengthMM / Math.pow(2, fretNumber / 12);
}

/**
 * Calculate string Y position in mm from center
 * Strings are evenly spaced across the nut width
 */
export function stringYmm(
  stringIndex: number,
  stringCount: number,
  nutWidthMM: number
): number {
  const spacing = nutWidthMM / (stringCount + 1);
  return -nutWidthMM / 2 + (stringIndex + 1) * spacing;
}

/**
 * Create mm-to-pixel converter
 * @param viewportWidthPx - Viewport width in pixels
 * @param visibleRangeMM - Visible range in mm (e.g., nut to fret 22)
 */
export function makeMmToPx(
  viewportWidthPx: number,
  visibleRangeMM: number
): (mm: number) => number {
  const scale = viewportWidthPx / visibleRangeMM;
  return (mm: number) => mm * scale;
}

/**
 * String gauge in inches (standard light set)
 */
export const STRING_GAUGES_INCH = [
  0.046, // Low E
  0.036, // A
  0.026, // D
  0.017, // G
  0.013, // B
  0.010, // High E
];

/**
 * Convert gauge from inches to mm
 */
export function gaugeToMM(gaugeInch: number): number {
  return gaugeInch * 25.4;
}

/**
 * Calculate string thickness in pixels
 * @param gaugeInch - String gauge in inches
 * @param pxPerMM - Pixels per millimeter scale factor
 */
export function stringThicknessPx(gaugeInch: number, pxPerMM: number): number {
  const mm = gaugeToMM(gaugeInch);
  const dpr = Math.max(1, window.devicePixelRatio ?? 1);
  return Math.max(1, Math.round(mm * 0.5 * pxPerMM * dpr));
}

