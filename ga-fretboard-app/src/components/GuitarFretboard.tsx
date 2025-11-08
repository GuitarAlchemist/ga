import React from 'react';
import { Box, Typography } from '@mui/material';
import {
  GuitarFretboardProps,
  DEFAULT_FRETBOARD_CONFIG,
  FretboardPosition,
} from '../types/fretboard.types';

/**
 * GuitarFretboard Component
 * 
 * A reusable guitar fretboard visualization component that displays
 * positions for chords, scales, modes, and arpeggios.
 * 
 * All music theory calculations should be done by the backend API.
 * This component only handles the visual rendering of position data.
 */
const GuitarFretboard: React.FC<GuitarFretboardProps> = ({
  config = {},
  positions = [],
  displayMode = 'chord',
  title,
  onPositionClick,
}) => {
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
  } = fretboardConfig;

  // Constants for rendering
  const MARGIN = 30;
  const LABEL_MARGIN = showStringLabels ? 40 : 0;
  const playableWidth = width - (2 * MARGIN) - LABEL_MARGIN;
  const playableHeight = height - (2 * MARGIN);

  /**
   * Calculate fret position using the 12th root of 2 (real guitar proportions)
   * This creates the logarithmic spacing that matches actual guitar frets
   */
  const getFretPosition = (fretNumber: number): number => {
    return MARGIN + LABEL_MARGIN + (playableWidth * (1 - Math.pow(2, -fretNumber / 12)));
  };

  /**
   * Calculate string position (evenly spaced)
   */
  const getStringPosition = (stringNumber: number): number => {
    const stringSpacing = playableHeight / (stringCount - 1);
    return MARGIN + (stringNumber * stringSpacing);
  };

  /**
   * Get the center position between two frets
   */
  const getFretCenter = (fretNumber: number): number => {
    if (fretNumber === 0) {
      // For open strings, position near the nut
      return MARGIN + LABEL_MARGIN + 15;
    }
    return (getFretPosition(fretNumber) + getFretPosition(fretNumber - 1)) / 2;
  };

  // Standard fret markers (dots)
  const singleDotFrets = [3, 5, 7, 9, 15, 17, 19, 21];
  const doubleDotFrets = [12, 24];

  /**
   * Render a position marker on the fretboard
   */
  const renderPosition = (position: FretboardPosition, index: number) => {
    const { string, fret, label, color = '#2196F3', emphasized = false } = position;
    const x = getFretCenter(fret);
    const y = getStringPosition(string);
    const radius = emphasized ? 12 : 10;

    return (
      <g
        key={`position-${index}`}
        onClick={() => onPositionClick?.(position)}
        style={{ cursor: onPositionClick ? 'pointer' : 'default' }}
      >
        {/* Position marker circle */}
        <circle
          cx={x}
          cy={y}
          r={radius}
          fill={color}
          stroke={emphasized ? '#FFD700' : '#fff'}
          strokeWidth={emphasized ? 3 : 2}
          opacity={0.9}
        />
        
        {/* Label text */}
        {label && (
          <text
            x={x}
            y={y + 4}
            textAnchor="middle"
            fontSize={emphasized ? 13 : 11}
            fontWeight="bold"
            fill="#fff"
          >
            {label}
          </text>
        )}
      </g>
    );
  };

  return (
    <Box sx={{ p: 2 }}>
      {title && (
        <Typography variant="h6" gutterBottom>
          {title}
        </Typography>
      )}
      
      <svg width={width} height={height}>
        {/* Fretboard background */}
        <rect
          x={MARGIN + LABEL_MARGIN}
          y={MARGIN}
          width={playableWidth}
          height={playableHeight}
          fill="#2b1810"
          stroke="black"
          strokeWidth={2}
        />

        {/* Frets */}
        {Array.from({ length: fretCount + 1 }).map((_, i) => (
          <line
            key={`fret-${i}`}
            x1={getFretPosition(i)}
            y1={MARGIN}
            x2={getFretPosition(i)}
            y2={height - MARGIN}
            stroke={i === 0 ? "#ECD08C" : "silver"}
            strokeWidth={i === 0 ? 8 : 2}
          />
        ))}

        {/* Strings */}
        {Array.from({ length: stringCount }).map((_, i) => (
          <line
            key={`string-${i}`}
            x1={MARGIN + LABEL_MARGIN}
            y1={getStringPosition(i)}
            x2={width - MARGIN}
            y2={getStringPosition(i)}
            stroke="#DDD"
            strokeWidth={3 - (i * 0.4)}
          />
        ))}

        {/* String labels (tuning) */}
        {showStringLabels && tuning && (
          <>
            {Array.from({ length: stringCount }).map((_, i) => (
              <text
                key={`string-label-${i}`}
                x={MARGIN + 10}
                y={getStringPosition(i) + 5}
                textAnchor="middle"
                fontSize={14}
                fontWeight="bold"
                fill="#666"
              >
                {tuning[i]}
              </text>
            ))}
          </>
        )}

        {/* Fret markers (single dots) */}
        {singleDotFrets.map(fret => (
          <circle
            key={`dot-${fret}`}
            cx={getFretCenter(fret)}
            cy={height / 2}
            r={8}
            fill="#888"
            opacity={0.5}
          />
        ))}

        {/* Fret markers (double dots at 12th and 24th frets) */}
        {doubleDotFrets.map(fret => (
          <g key={`double-dot-${fret}`}>
            <circle
              cx={getFretCenter(fret)}
              cy={height / 2 - 30}
              r={8}
              fill="#888"
              opacity={0.5}
            />
            <circle
              cx={getFretCenter(fret)}
              cy={height / 2 + 30}
              r={8}
              fill="#888"
              opacity={0.5}
            />
          </g>
        ))}

        {/* Fret numbers */}
        {showFretNumbers && (
          <>
            {Array.from({ length: fretCount + 1 }).map((_, i) => {
              if (i === 0 || i % 3 !== 0) return null;
              return (
                <text
                  key={`fret-number-${i}`}
                  x={getFretCenter(i)}
                  y={height - 10}
                  textAnchor="middle"
                  fontSize={12}
                  fill="#666"
                >
                  {i}
                </text>
              );
            })}
          </>
        )}

        {/* Render positions */}
        {positions.map((position, index) => renderPosition(position, index))}
      </svg>

      {/* Display mode indicator */}
      <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
        Mode: {displayMode.charAt(0).toUpperCase() + displayMode.slice(1)}
      </Typography>
    </Box>
  );
};

export default GuitarFretboard;

