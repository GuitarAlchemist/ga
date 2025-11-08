/**
 * Instrument type definitions
 */

export interface Tuning {
  name: string;
  tuning: string;
}

export interface Instrument {
  name: string;
  icon?: string;
  tunings: Tuning[];
}

export interface InstrumentIconProps {
  /** SVG icon string */
  icon?: string;
  /** Size in pixels (default: 24) */
  size?: number;
  /** Color (default: currentColor) */
  color?: string;
  /** Additional CSS class */
  className?: string;
}

