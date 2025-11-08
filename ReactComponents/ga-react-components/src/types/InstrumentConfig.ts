/**
 * Generic Stringed Instrument Configuration
 * 
 * This type system supports ANY stringed instrument (guitar, bass, ukulele, banjo, etc.)
 * by defining common properties that all fretted instruments share.
 */

/**
 * Body style categories for visual rendering
 */
export type InstrumentBodyStyle =
  | 'classical'        // Classical guitar with wide neck, nylon strings
  | 'acoustic'         // Steel-string acoustic guitar
  | 'electric'         // Electric guitar (Stratocaster, Les Paul, etc.)
  | 'bass'             // Bass guitar (4, 5, or 6 strings)
  | 'ukulele'          // Ukulele (soprano, concert, tenor, baritone)
  | 'banjo'            // Banjo (4 or 5 strings)
  | 'mandolin'         // Mandolin family (mandolin, mandola, mandocello)
  | 'lute'             // Historical lutes
  | 'generic';         // Generic fretboard (no body)

/**
 * Rendering mode for the fretboard
 */
export type RenderMode =
  | '2d-svg'           // SVG-based 2D rendering (GuitarFretboard)
  | '2d-canvas'        // Canvas-based 2D rendering (RealisticFretboard)
  | '3d-webgl'         // Three.js WebGL rendering
  | '3d-webgpu';       // Three.js WebGPU rendering (ThreeFretboard)

/**
 * Spacing mode for fret positioning
 */
export type SpacingMode =
  | 'realistic'        // Logarithmic spacing (like real instruments)
  | 'schematic';       // Linear spacing (easier to read)

/**
 * Core instrument configuration
 * This is the minimal information needed to render any stringed instrument
 */
export interface InstrumentConfig {
  // Identity
  family: string;              // "Guitar", "BassGuitar", "Ukulele", "Banjo", etc.
  variant: string;             // "Standard", "Drop D", "Baritone", "Bluegrass5Strings", etc.
  displayName: string;         // Human-readable name: "Standard Guitar"
  fullName?: string;           // Optional detailed name: "5 strings Bluegrass"
  
  // Tuning (from YAML)
  tuning: string[];            // Array of pitch names: ["E2", "A2", "D3", "G3", "B3", "E4"]
  
  // Physical dimensions (in millimeters)
  scaleLength: number;         // Distance from nut to bridge (e.g., 650mm for classical guitar)
  nutWidth: number;            // Width at the nut (e.g., 52mm for classical, 43mm for electric)
  bridgeWidth: number;         // Width at the bridge (usually wider than nut)
  fretCount: number;           // Number of frets (12-24 typically)
  
  // Visual properties
  bodyStyle: InstrumentBodyStyle;
  woodColor?: number;          // Hex color for wood (default: 0x8B4513 - saddle brown)
  
  // Optional features
  hasRosette?: boolean;        // Sound hole decoration (acoustic instruments)
  hasPickguard?: boolean;      // Pickguard (acoustic/electric guitars)
  hasDroneString?: boolean;    // Drone string (banjo, sitar, etc.)
  droneStringPosition?: number; // Position of drone string (0-1, 0 = nut side)
}

/**
 * Position marker on the fretboard
 */
export interface FretboardPosition {
  string: number;              // String index (0 = highest pitch, n-1 = lowest pitch)
  fret: number;                // Fret number (0 = open string)
  label?: string;              // Optional label (note name, finger number, etc.)
  color?: string;              // Optional color (hex or CSS color)
  emphasized?: boolean;        // Highlight this position (e.g., root note)
}

/**
 * Rendering options for the fretboard
 */
export interface FretboardRenderOptions {
  // Dimensions
  width?: number;              // Canvas/SVG width in pixels
  height?: number;             // Canvas/SVG height in pixels
  
  // Display options
  showFretNumbers?: boolean;   // Show fret numbers
  showStringLabels?: boolean;  // Show string tuning labels
  showInlays?: boolean;        // Show position marker inlays (dots)
  spacingMode?: SpacingMode;   // Fret spacing mode
  
  // 3D-specific options
  enableOrbitControls?: boolean; // Allow camera rotation (3D only)
  cameraAngle?: number;        // Initial camera angle (3D only)
  
  // Interaction
  interactive?: boolean;       // Enable click/touch interaction
  highlightOnHover?: boolean;  // Highlight positions on hover
}

/**
 * Complete props for the generic fretboard component
 */
export interface StringedInstrumentFretboardProps {
  // Core configuration
  instrument: InstrumentConfig;
  renderMode: RenderMode;
  
  // Position markers
  positions?: FretboardPosition[];
  
  // Common features
  capoFret?: number;           // Capo position (0 = no capo)
  leftHanded?: boolean;        // Flip for left-handed players
  
  // Rendering options
  options?: FretboardRenderOptions;
  
  // Callbacks
  onPositionClick?: (string: number, fret: number) => void;
  onPositionHover?: (string: number | null, fret: number | null) => void;
  onCapoChange?: (fret: number) => void;
  onLeftHandedChange?: (leftHanded: boolean) => void;
  
  // UI customization
  title?: string;              // Optional title
  showControls?: boolean;      // Show capo/left-handed controls
}

/**
 * Instrument database entry (parsed from YAML)
 */
export interface InstrumentDatabaseEntry {
  family: string;
  displayName: string;
  variants: {
    [variantName: string]: {
      displayName: string;
      fullName?: string;
      tuning: string;          // Space-separated pitch names
    };
  };
}

/**
 * Default physical properties for common instrument families
 */
export const INSTRUMENT_DEFAULTS: Record<string, Partial<InstrumentConfig>> = {
  Guitar: {
    scaleLength: 650,          // Classical guitar scale
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 19,
    bodyStyle: 'classical',
    woodColor: 0x8B4513,
  },
  
  BassGuitar: {
    scaleLength: 860,          // 34" bass scale
    nutWidth: 45,
    bridgeWidth: 60,
    fretCount: 24,
    bodyStyle: 'bass',
    woodColor: 0x654321,
  },
  
  Ukulele: {
    scaleLength: 330,          // Soprano ukulele
    nutWidth: 35,
    bridgeWidth: 40,
    fretCount: 12,
    bodyStyle: 'ukulele',
    woodColor: 0xDEB887,
    hasRosette: true,
  },
  
  Banjo: {
    scaleLength: 660,          // Standard banjo scale
    nutWidth: 32,
    bridgeWidth: 35,
    fretCount: 22,
    bodyStyle: 'banjo',
    woodColor: 0x8B7355,
    hasDroneString: true,
    droneStringPosition: 0.2,
  },
  
  Mandolin: {
    scaleLength: 350,          // Standard mandolin scale
    nutWidth: 28,
    bridgeWidth: 32,
    fretCount: 20,
    bodyStyle: 'mandolin',
    woodColor: 0xA0522D,
  },
  
  BaritoneGuitar: {
    scaleLength: 686,          // 27" baritone scale
    nutWidth: 48,
    bridgeWidth: 68,
    fretCount: 22,
    bodyStyle: 'electric',
    woodColor: 0x654321,
  },
  
  OctaveGuitar: {
    scaleLength: 508,          // 20" octave guitar scale
    nutWidth: 45,
    bridgeWidth: 60,
    fretCount: 19,
    bodyStyle: 'acoustic',
    woodColor: 0x8B4513,
  },
};

/**
 * Helper function to create an instrument config from YAML data
 */
export function createInstrumentConfig(
  family: string,
  variant: string,
  tuningString: string,
  displayName: string,
  fullName?: string
): InstrumentConfig {
  const defaults = INSTRUMENT_DEFAULTS[family] || INSTRUMENT_DEFAULTS.Guitar;
  const tuning = tuningString.split(/\s+/).filter(s => s.length > 0);
  
  return {
    family,
    variant,
    displayName,
    fullName,
    tuning,
    scaleLength: defaults.scaleLength!,
    nutWidth: defaults.nutWidth!,
    bridgeWidth: defaults.bridgeWidth!,
    fretCount: defaults.fretCount!,
    bodyStyle: defaults.bodyStyle!,
    woodColor: defaults.woodColor,
    hasRosette: defaults.hasRosette,
    hasPickguard: defaults.hasPickguard,
    hasDroneString: defaults.hasDroneString,
    droneStringPosition: defaults.droneStringPosition,
  };
}

/**
 * Helper function to get string count from tuning
 */
export function getStringCount(instrument: InstrumentConfig): number {
  return instrument.tuning.length;
}

/**
 * Helper function to check if instrument has a specific feature
 */
export function hasFeature(
  instrument: InstrumentConfig,
  feature: 'rosette' | 'pickguard' | 'droneString'
): boolean {
  switch (feature) {
    case 'rosette':
      return instrument.hasRosette ?? false;
    case 'pickguard':
      return instrument.hasPickguard ?? false;
    case 'droneString':
      return instrument.hasDroneString ?? false;
    default:
      return false;
  }
}

