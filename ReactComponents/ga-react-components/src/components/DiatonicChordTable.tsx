import React, { useState } from 'react';
import { Box, Typography } from '@mui/material';
import type { ChordInContext } from '../types/agent-state';

// Quality colors driven by ChordInContext.function (backend value) — no string parsing.
const FUNCTION_COLORS: Record<string, { bg: string; text: string; border: string }> = {
  Tonic:        { bg: '#fff8e1', text: '#e65100',  border: '#ffb300' },
  Subdominant:  { bg: '#e3f2fd', text: '#1565c0',  border: '#1976d2' },
  Dominant:     { bg: '#fce4ec', text: '#880e4f',  border: '#c2185b' },
  LeadingTone:  { bg: '#f3e5f5', text: '#6a1b9a',  border: '#9c27b0' },
};

const FALLBACK_COLORS = { bg: '#f5f5f5', text: '#333', border: '#bbb' };

function colorsFor(fn: string) {
  return FUNCTION_COLORS[fn] ?? FALLBACK_COLORS;
}

export interface DiatonicChordTableProps {
  /** Domain chords — quality and roman numeral come from the backend. */
  chords: readonly ChordInContext[];
  onChordClick?: (chord: ChordInContext) => void;
}

const DiatonicChordTable: React.FC<DiatonicChordTableProps> = ({ chords, onChordClick }) => {
  const [selectedIdx, setSelectedIdx] = useState<number | null>(null);

  if (chords.length === 0) return null;

  return (
    <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
      {chords.map((chord, i) => {
        const colors     = colorsFor(chord.function);
        const isSelected = selectedIdx === i;
        const roman      = chord.romanNumeral ?? chord.scaleDegree?.toString() ?? '—';

        return (
          <Box
            key={chord.templateName + i}
            onClick={() => {
              setSelectedIdx(i);
              onChordClick?.(chord);
            }}
            sx={{
              cursor:        'pointer',
              display:       'flex',
              flexDirection: 'column',
              alignItems:    'center',
              px: 1.5,
              py: 1,
              borderRadius:  1.5,
              bgcolor:       colors.bg,
              border:        `2px solid ${isSelected ? colors.border : 'transparent'}`,
              boxShadow:     isSelected
                ? `0 0 0 2px ${colors.border}40`
                : '0 1px 3px rgba(0,0,0,0.10)',
              transition: 'all 0.15s ease',
              minWidth:   52,
              '&:hover': {
                boxShadow: '0 2px 8px rgba(0,0,0,0.18)',
                transform: 'translateY(-2px)',
              },
            }}
          >
            <Typography
              variant="caption"
              sx={{
                color:       colors.border,
                fontWeight:  700,
                fontFamily:  'Georgia, serif',
                fontSize:    '0.6rem',
                lineHeight:  1,
                letterSpacing: 0.5,
              }}
            >
              {roman}
            </Typography>
            <Typography
              variant="body2"
              sx={{
                color:      colors.text,
                fontWeight: 700,
                mt:         0.5,
                fontSize:   '0.95rem',
                fontFamily: 'monospace',
              }}
            >
              {chord.contextualName}
            </Typography>
            {chord.functionalDescription && (
              <Typography
                variant="caption"
                sx={{ color: colors.border, fontSize: '0.55rem', mt: 0.25, opacity: 0.8 }}
              >
                {chord.functionalDescription}
              </Typography>
            )}
          </Box>
        );
      })}
    </Box>
  );
};

export default DiatonicChordTable;
