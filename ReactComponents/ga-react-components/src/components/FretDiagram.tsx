import React from 'react';
import { Box, Typography } from '@mui/material';
import { computeFretWindow, rowOf, DEFAULT_FRETS_SHOWN } from './fretWindow';

/**
 * Order of the `frets` array.
 *
 * `low-to-high` is the standard chord-chart order (E A D G B e) — the one
 * `GA.Business.ML.Agents.FretDiagram` parses and the one printed in chat answers.
 *
 * `high-to-low` is the domain's own order: `Str` 1 is the *highest*-pitched string
 * (`Str.cs`), voicings are generated over `Str.Range(6)`, and `Voicing.Diagram` therefore
 * emits the high e first. Anything that comes straight off a `Voicing` — including
 * `GET /api/contextual-chords/voicings/{chord}` (`VoicingFilterService`) and the OPTK index
 * metadata — is in this order. Open C major arrives as `0-1-0-2-3-x`, not `x-3-2-0-1-0`.
 */
export type StringOrder = 'low-to-high' | 'high-to-low';

export interface FretDiagramProps {
  /** Chord name displayed above the diagram */
  chordName: string;
  /**
   * 6-element fret array.
   * -1 = muted (x), 0 = open, 1–12 = fret number.
   * Interpreted according to {@link stringOrder}.
   */
  frets: number[];
  /**
   * Which end of {@link frets} is the low E. Defaults to `low-to-high`, the chord-chart
   * convention. Pass `high-to-low` for arrays that come from a `Voicing` / the GA API.
   */
  stringOrder?: StringOrder;
}

const STRINGS = 6;
const STRING_SPACING = 20;
const FRET_SPACING = 22;
const MARGIN_LEFT = 22;
const MARGIN_TOP = 30;

/**
 * Compact SVG chord diagram — no external dependencies.
 * Shows fret grid, open/muted string markers, and dot positions.
 *
 * Every fretted note is drawn: the grid grows downward for a voicing whose span exceeds the
 * default five rows rather than dropping the notes that fall outside it. See
 * {@link computeFretWindow}.
 */
const FretDiagram: React.FC<FretDiagramProps> = ({ chordName, frets, stringOrder = 'low-to-high' }) => {
  const width = MARGIN_LEFT + (STRINGS - 1) * STRING_SPACING + 24;

  // Draw low E on the left regardless of how the caller ordered the array.
  const lowToHigh = stringOrder === 'high-to-low' ? [...frets].reverse() : frets;

  const window = computeFretWindow(lowToHigh, DEFAULT_FRETS_SHOWN);
  const { baseFret, fretsShown } = window;

  const svgHeight = MARGIN_TOP + fretsShown * FRET_SPACING + 10;

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
        {Array.from({ length: fretsShown + 1 }, (_, i) => (
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
            x2={sx(s)} y2={fy(fretsShown)}
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
        {lowToHigh.map((fret, s) => {
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
          // Fretted dot — computeFretWindow guarantees this row is inside the grid.
          const cy = fy(rowOf(fret, window) - 1) + FRET_SPACING / 2;
          return (
            <circle key={`d-${s}`} cx={cx} cy={cy} r={7} fill="#333" />
          );
        })}
      </svg>
    </Box>
  );
};

export default FretDiagram;
