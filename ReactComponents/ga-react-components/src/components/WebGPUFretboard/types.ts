/**
 * WebGPU Fretboard Types
 * Following Pixi.js v8 WebGPU best practices
 */

export interface FretboardPosition {
  string: number;
  fret: number;
  label?: string;
  color?: string;
  emphasized?: boolean;
}

export interface WebGPUFretboardConfig {
  // Physical dimensions (in mm)
  scaleLengthMM?: number; // e.g., 650mm classical, 648mm Fender
  nutWidthMM?: number;    // e.g., 52mm classical, 43mm electric
  
  // Display
  fretCount?: number;
  stringCount?: number;
  tuning?: string[];
  
  // Features
  capoFret?: number;
  leftHanded?: boolean;
  showFretNumbers?: boolean;
  showStringLabels?: boolean;
  showInlays?: boolean;
  
  // Visual
  guitarModel?: string;
  
  // Viewport (in pixels)
  viewportWidth?: number;
  viewportHeight?: number;
}

export interface WebGPUFretboardProps {
  title?: string;
  positions?: FretboardPosition[];
  config?: WebGPUFretboardConfig;
  onPositionClick?: (string: number, fret: number) => void;
}

export const DEFAULT_CONFIG: Required<WebGPUFretboardConfig> = {
  scaleLengthMM: 650,
  nutWidthMM: 52,
  fretCount: 22,
  stringCount: 6,
  tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
  capoFret: 0,
  leftHanded: false,
  showFretNumbers: true,
  showStringLabels: true,
  showInlays: true,
  guitarModel: 'classical',
  viewportWidth: 1400,
  viewportHeight: 250,
};

