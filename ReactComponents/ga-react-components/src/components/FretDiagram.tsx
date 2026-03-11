import React from 'react';
import { Box, Typography } from '@mui/material';

export interface FretDiagramProps {
  /** Chord name displayed above the diagram */
  chordName: string;
  /**
   * 6-element fret array (low-E to high-e).
   * -1 = muted (x), 0 = open, 1–12 = fret number.
   */
  frets: number[];
}

const STRINGS = 6;
const FRETS_SHOWN = 5;
const STRING_SPACING = 20;
const FRET_SPACING = 22;
const MARGIN_LEFT = 22;
const MARGIN_TOP = 30;

/**
 * Compact SVG chord diagram — no external dependencies.
 * Shows fret grid, open/muted string markers, and dot positions.
 */
const FretDiagram: React.FC<FretDiagramProps> = ({ chordName, frets }) => {
  const width = MARGIN_LEFT + (STRINGS - 1) * STRING_SPACING + 24;

  // Determine the fret window (base fret for the grid)
  const pressed = frets.filter(f => f > 0);
  const minFret = pressed.length > 0 ? Math.min(...pressed) : 1;
  const baseFret = minFret <= 2 ? 1 : minFret;

  const svgHeight = MARGIN_TOP + FRETS_SHOWN * FRET_SPACING + 10;

  // String x positions (low E on left)
  const sx = (s: number) => MARGIN_LEFT + s * STRING_SPACING;
  // Fret y positions
  const fy = (f: number) => MARGIN_TOP + f * FRET_SPACING;

  return (
    <Box sx={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}>
      <Typography variant="caption" sx={{ fontWeight: 700, fontFamily: 'monospace' }}>
        {chordName}
      </Typography>
      <svg width={width} height={svgHeight} aria-label={`${chordName} chord diagram`}>
        {/* Nut (thick top line when base fret = 1) */}
        {baseFret === 1 && (
          <line
            x1={sx(0)} y1={fy(0)}
            x2={sx(STRINGS - 1)} y2={fy(0)}
            stroke="#333" strokeWidth={4}
          />
        )}

        {/* Fret wires */}
        {Array.from({ length: FRETS_SHOWN + 1 }, (_, i) => (
          <line
            key={`fret-${i}`}
            x1={sx(0)} y1={fy(i)}
            x2={sx(STRINGS - 1)} y2={fy(i)}
            stroke="#999" strokeWidth={1}
          />
        ))}

        {/* String lines */}
        {Array.from({ length: STRINGS }, (_, s) => (
          <line
            key={`str-${s}`}
            x1={sx(s)} y1={fy(0)}
            x2={sx(s)} y2={fy(FRETS_SHOWN)}
            stroke="#999" strokeWidth={1}
          />
        ))}

        {/* Base fret label (when not at nut) */}
        {baseFret > 1 && (
          <text
            x={sx(STRINGS - 1) + 6} y={fy(1) + 4}
            fontSize={9} fill="#555" fontFamily="monospace"
          >
            {baseFret}fr
          </text>
        )}

        {/* Open/muted markers above nut, and dots on frets */}
        {frets.map((fret, s) => {
          const cx = sx(s);
          if (fret === -1) {
            // Muted: ×
            return (
              <text key={`m-${s}`} x={cx - 4} y={fy(0) - 8}
                fontSize={11} fill="#e53935" fontWeight="bold" fontFamily="monospace">×</text>
            );
          }
          if (fret === 0) {
            // Open: ○
            return (
              <circle key={`o-${s}`} cx={cx} cy={fy(0) - 10} r={5}
                fill="none" stroke="#444" strokeWidth={1.5} />
            );
          }
          // Fretted dot
          const row = fret - baseFret + 1;
          if (row < 1 || row > FRETS_SHOWN) return null;
          const cy = fy(row - 1) + FRET_SPACING / 2;
          return (
            <circle key={`d-${s}`} cx={cx} cy={cy} r={7} fill="#333" />
          );
        })}
      </svg>
    </Box>
  );
};

export default FretDiagram;
