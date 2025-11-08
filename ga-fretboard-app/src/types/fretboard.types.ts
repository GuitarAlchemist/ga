/**
 * Represents a position on the fretboard
 */
export interface FretboardPosition {
  /** String number (0-5 for standard 6-string guitar, 0 = high E) */
  string: number;
  /** Fret number (0 = open string) */
  fret: number;
  /** Optional label to display on the position (e.g., note name, interval, finger number) */
  label?: string;
  /** Optional color for the position marker */
  color?: string;
  /** Whether this position is emphasized (e.g., root note) */
  emphasized?: boolean;
}

/**
 * Display mode for the fretboard visualization
 */
export type DisplayMode = 'chord' | 'scale' | 'mode' | 'arpeggio';

/**
 * Configuration for the fretboard display
 */
export interface FretboardConfig {
  /** Number of frets to display (default: 24) */
  fretCount?: number;
  /** Number of strings (default: 6) */
  stringCount?: number;
  /** Starting fret to display (default: 0) */
  startFret?: number;
  /** Tuning labels for each string (e.g., ['E', 'A', 'D', 'G', 'B', 'E']) */
  tuning?: string[];
  /** Whether to show fret numbers */
  showFretNumbers?: boolean;
  /** Whether to show string labels */
  showStringLabels?: boolean;
  /** Width of the fretboard in pixels */
  width?: number;
  /** Height of the fretboard in pixels */
  height?: number;
}

/**
 * Props for the GuitarFretboard component
 */
export interface GuitarFretboardProps {
  /** Configuration for the fretboard display */
  config?: FretboardConfig;
  /** Array of positions to display on the fretboard */
  positions?: FretboardPosition[];
  /** Current display mode */
  displayMode?: DisplayMode;
  /** Optional title for the visualization */
  title?: string;
  /** Optional callback when a position is clicked */
  onPositionClick?: (position: FretboardPosition) => void;
}

/**
 * Default fretboard configuration
 */
export const DEFAULT_FRETBOARD_CONFIG: Required<FretboardConfig> = {
  fretCount: 24,
  stringCount: 6,
  startFret: 0,
  tuning: ['E', 'B', 'G', 'D', 'A', 'E'], // Standard tuning (high to low)
  showFretNumbers: true,
  showStringLabels: true,
  width: 1200,
  height: 200,
};

