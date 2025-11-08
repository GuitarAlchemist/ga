// @ts-nocheck
import React, { useState } from 'react';
import { Box, Typography, Paper } from '@mui/material';

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
 * Fret spacing mode
 */
export type FretSpacingMode = 'schematic' | 'realistic';

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
  /** Fret spacing mode: 'schematic' (linear) or 'realistic' (logarithmic) */
  spacingMode?: FretSpacingMode;
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
  /** Optional callback when a position is hovered */
  onPositionHover?: (position: FretboardPosition | null) => void;
}

/**
 * Default fretboard configuration
 */
const DEFAULT_FRETBOARD_CONFIG: Required<FretboardConfig> = {
  fretCount: 24,
  stringCount: 6,
  startFret: 0,
  tuning: ['E', 'B', 'G', 'D', 'A', 'E'], // Standard tuning (high to low)
  showFretNumbers: true,
  showStringLabels: true,
  width: 1200,
  height: 200,
  spacingMode: 'schematic',
};

/**
 * GuitarFretboard Component
 *
 * A reusable guitar fretboard visualization component that displays
 * positions for chords, scales, modes, and arpeggios.
 *
 * Inspired by the legacy Delphi implementation, this component provides
 * a clean, interactive fretboard visualization.
 */
const GuitarFretboard: React.FC<GuitarFretboardProps> = ({
  config = {},
  positions = [],
  displayMode = 'chord',
  title,
  onPositionClick,
  onPositionHover,
}) => {
  const [hoveredPosition, setHoveredPosition] = useState<FretboardPosition | null>(null);

  // Merge provided config with defaults
  const fretboardConfig = { ...DEFAULT_FRETBOARD_CONFIG, ...config };
  const {
    fretCount,
    stringCount,
    tuning,
    showFretNumbers,
    showStringLabels,
    width,
    height,
    spacingMode,
  } = fretboardConfig;

  // Calculate dimensions
  const stringSpacing = height / (stringCount + 1);
  const labelWidth = 40;
  const dotRadius = 4;
  const positionRadius = 12;

  // Fret markers (standard guitar fretboard markers)
  const fretMarkers = new Set([3, 5, 7, 9, 12, 15, 17, 19, 21, 24]);

  /**
   * Calculate fret position based on spacing mode
   * Schematic: linear spacing (equal distance between frets)
   * Realistic: logarithmic spacing (based on 12th root of 2, like real guitars)
   */
  const calculateFretPosition = (fretNumber: number): number => {
    const playableWidth = width - labelWidth - 20;

    if (spacingMode === 'realistic') {
      // Logarithmic spacing using 12th root of 2 (0.5^(1/12))
      // This matches the Delphi implementation
      const guitarNeckLength = playableWidth / (1 - Math.pow(0.5, fretCount / 12));
      return labelWidth + guitarNeckLength * (Math.pow(0.5, fretNumber / 12) - Math.pow(0.5, fretCount / 12));
    } else {
      // Schematic: linear spacing
      const fretSpacing = playableWidth / fretCount;
      return labelWidth + fretNumber * fretSpacing;
    }
  };

  // Create a map of positions for quick lookup
  const positionMap = new Map<string, FretboardPosition>();
  positions.forEach(pos => {
    positionMap.set(`${pos.string}-${pos.fret}`, pos);
  });

  const handlePositionClick = (string: number, fret: number) => {
    const position: FretboardPosition = { string, fret };
    onPositionClick?.(position);
  };

  return (
    <Paper elevation={2} sx={{ p: 2, bgcolor: '#f5f5f5' }}>
      {title && (
        <Typography variant="h6" sx={{ mb: 2, fontWeight: 'bold' }}>
          {title}
        </Typography>
      )}

      <Box sx={{ overflowX: 'auto' }}>
        <svg
          width={width + labelWidth}
          height={height + 40}
          style={{ border: '1px solid #ddd', backgroundColor: '#faf8f3' }}
        >
          {/* Fretboard background */}
          <rect
            x={labelWidth}
            y={20}
            width={width}
            height={height}
            fill="#d4a574"
            stroke="#8b4513"
            strokeWidth={2}
          />

          {/* Frets */}
          {Array.from({ length: fretCount + 1 }).map((_, i) => {
            const x = calculateFretPosition(i);
            const isNut = i === 0;
            return (
              <line
                key={`fret-${i}`}
                x1={x}
                y1={20}
                x2={x}
                y2={height + 20}
                stroke="#8b4513"
                strokeWidth={isNut ? 4 : 1}
              />
            );
          })}

          {/* Strings */}
          {Array.from({ length: stringCount }).map((_, i) => {
            const y = 20 + (i + 1) * stringSpacing;
            return (
              <line
                key={`string-${i}`}
                x1={labelWidth}
                y1={y}
                x2={labelWidth + width}
                y2={y}
                stroke="#333"
                strokeWidth={2}
              />
            );
          })}

          {/* Fret markers (dots) */}
          {Array.from({ length: fretCount }).map((_, i) => {
            const fret = i + 1;
            if (fretMarkers.has(fret)) {
              const x1 = calculateFretPosition(fret - 1);
              const x2 = calculateFretPosition(fret);
              const x = (x1 + x2) / 2; // Center between frets
              const y = 20 + height / 2;
              return (
                <circle
                  key={`marker-${fret}`}
                  cx={x}
                  cy={y}
                  r={dotRadius}
                  fill="#8b4513"
                  opacity={0.5}
                />
              );
            }
            return null;
          })}

          {/* Fret numbers */}
          {showFretNumbers &&
            Array.from({ length: fretCount + 1 }).map((_, i) => {
              const x = calculateFretPosition(i);
              return (
                <text
                  key={`fret-num-${i}`}
                  x={x}
                  y={height + 35}
                  textAnchor="middle"
                  fontSize={12}
                  fill="#666"
                >
                  {i}
                </text>
              );
            })}

          {/* String labels (tuning) */}
          {showStringLabels &&
            tuning.map((note, i) => {
              const y = 20 + (i + 1) * stringSpacing;
              return (
                <text
                  key={`tuning-${i}`}
                  x={labelWidth - 10}
                  y={y + 5}
                  textAnchor="end"
                  fontSize={12}
                  fontWeight="bold"
                  fill="#333"
                >
                  {note}
                </text>
              );
            })}

          {/* Position markers */}
          {positions.map((pos, idx) => {
            const x1 = calculateFretPosition(pos.fret);
            const x2 = calculateFretPosition(pos.fret + 1);
            const x = (x1 + x2) / 2; // Center between frets
            const y = 20 + (pos.string + 1) * stringSpacing;
            const isHovered = hoveredPosition?.string === pos.string && hoveredPosition?.fret === pos.fret;
            const color = pos.color || (pos.emphasized ? '#ff6b6b' : '#4dabf7');

            return (
              <g key={`position-${idx}`}>
                <circle
                  cx={x}
                  cy={y}
                  r={positionRadius}
                  fill={color}
                  opacity={isHovered ? 1 : 0.8}
                  stroke={pos.emphasized ? '#ff0000' : '#333'}
                  strokeWidth={pos.emphasized ? 2 : 1}
                  style={{ cursor: 'pointer', transition: 'all 0.2s' }}
                  onMouseEnter={() => {
                    setHoveredPosition(pos);
                    onPositionHover?.(pos);
                  }}
                  onMouseLeave={() => {
                    setHoveredPosition(null);
                    onPositionHover?.(null);
                  }}
                  onClick={() => handlePositionClick(pos.string, pos.fret)}
                />
                {pos.label && (
                  <text
                    x={x}
                    y={y + 5}
                    textAnchor="middle"
                    fontSize={11}
                    fontWeight="bold"
                    fill="#fff"
                    pointerEvents="none"
                  >
                    {pos.label}
                  </text>
                )}
              </g>
            );
          })}
        </svg>
      </Box>

      {/* Display mode indicator */}
      <Typography variant="caption" sx={{ mt: 1, display: 'block', color: '#666' }}>
        Mode: {displayMode} | Spacing: {spacingMode === 'realistic' ? 'Realistic (Logarithmic)' : 'Schematic (Linear)'}
      </Typography>
    </Paper>
  );
};

export default GuitarFretboard;
