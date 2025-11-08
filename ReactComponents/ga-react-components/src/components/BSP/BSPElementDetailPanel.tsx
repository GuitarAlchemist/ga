import React from 'react';
import { Box, Paper, Typography, Chip, Stack } from '@mui/material';
import BraceletNotation from '../Atonal/BraceletNotation';
import IntervalClassVector from '../IntervalClassVector';
import VexChordDiagram from '../Chords/VexChordDiagram';
import VexTabViewer from '../VexTabViewer';
import { ChordData } from '../Chords/ChordData';
import { ChordNote } from '../Chords/ChordNote';

export interface BSPElementData {
  floor: number;
  name: string;
  group?: string;
  
  // Floor 0-2: Pitch Class Set data
  pitchClassSet?: number; // Binary representation
  forteCode?: string;
  intervalClassVector?: number[];
  primeForm?: number[];
  
  // Floor 3-4: Chord data
  chordSymbol?: string;
  chordQuality?: string;
  rootNote?: string;
  inversion?: number;
  notes?: string[];
  
  // Floor 5: Voicing data
  voicing?: number[]; // Fret numbers for each string
  cagedShape?: string;
  fretRange?: { min: number; max: number };
  difficulty?: string;
  tabNotation?: string; // VexTab format
}

interface BSPElementDetailPanelProps {
  element: BSPElementData | null;
  position?: { x: number; y: number };
}

const BSPElementDetailPanel: React.FC<BSPElementDetailPanelProps> = ({ element, position }) => {
  if (!element) return null;

  const getFloorName = (floor: number): string => {
    const names = [
      'Pitch Class Sets (OPTIC/K)',
      'Forte Codes (OPTIC)',
      'Prime Forms (OPTC)',
      'Chords (OPC)',
      'Inversions (OP)',
      'Voicings (O)'
    ];
    return names[floor] || `Floor ${floor}`;
  };

  const getFloorColor = (floor: number): string => {
    // Atonal space (0-2): cyan/blue tones
    // Tonal space (3-5): yellow/gold tones
    const colors = ['#00ffff', '#00ccff', '#0099ff', '#ffff00', '#ffcc00', '#ff9900'];
    return colors[floor] || '#0f0';
  };

  // Calculate smart positioning to avoid going off-screen
  const calculatePosition = () => {
    if (!position) return { left: 20, top: 20 };

    const panelWidth = 350;
    const panelHeight = 400; // Approximate max height
    const padding = 20;

    let left = position.x + padding;
    let top = position.y + padding;

    // Check if panel would go off right edge
    if (left + panelWidth > window.innerWidth) {
      left = position.x - panelWidth - padding;
    }

    // Check if panel would go off bottom edge
    if (top + panelHeight > window.innerHeight) {
      top = window.innerHeight - panelHeight - padding;
    }

    // Ensure panel doesn't go off left edge
    if (left < padding) {
      left = padding;
    }

    // Ensure panel doesn't go off top edge
    if (top < padding) {
      top = padding;
    }

    return { left, top };
  };

  const smartPosition = calculatePosition();

  const renderFloor0to2Content = () => {
    // Floors 0-2: Pitch Class Sets, Forte Codes, Prime Forms
    const pitchClassSet = element.pitchClassSet || 0;
    const intervalClassVector = element.intervalClassVector || [];
    
    return (
      <Stack spacing={2}>
        {/* Bracelet Notation */}
        <Box sx={{ display: 'flex', justifyContent: 'center' }}>
          <BraceletNotation scale={pitchClassSet} size={180} />
        </Box>

        {/* Forte Code */}
        {element.forteCode && (
          <Box>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Forte Code:
            </Typography>
            <Typography sx={{ color: '#0ff', fontFamily: 'monospace', fontSize: '16px', fontWeight: 'bold' }}>
              {element.forteCode}
            </Typography>
          </Box>
        )}

        {/* Prime Form */}
        {element.primeForm && element.primeForm.length > 0 && (
          <Box>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Prime Form:
            </Typography>
            <Typography sx={{ color: '#0ff', fontFamily: 'monospace', fontSize: '14px' }}>
              [{element.primeForm.join(', ')}]
            </Typography>
          </Box>
        )}

        {/* Interval Class Vector */}
        {intervalClassVector.length > 0 && (
          <Box>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Interval Class Vector:
            </Typography>
            <Typography sx={{ color: '#0ff', fontFamily: 'monospace', fontSize: '14px' }}>
              &lt;{intervalClassVector.join(', ')}&gt;
            </Typography>
          </Box>
        )}
      </Stack>
    );
  };

  const renderFloor3to4Content = () => {
    // Floors 3-4: Chords and Inversions
    const voicing = element.voicing || [0, 2, 2, 1, 0, 0];
    const chordNotes: ChordNote[] = voicing.map((fret, index) => [index + 1, fret] as ChordNote);
    const chordData: ChordData = {
      chordNotes,
      position: 1,
      barres: []
    };

    return (
      <Stack spacing={2}>
        {/* Chord Symbol */}
        {element.chordSymbol && (
          <Box sx={{ textAlign: 'center' }}>
            <Typography sx={{ color: '#ff0', fontSize: '24px', fontWeight: 'bold', fontFamily: 'serif' }}>
              {element.chordSymbol}
            </Typography>
          </Box>
        )}

        {/* Chord Quality */}
        {element.chordQuality && (
          <Box sx={{ display: 'flex', justifyContent: 'center' }}>
            <Chip
              label={element.chordQuality}
              size="small"
              sx={{
                backgroundColor: '#ff0',
                color: '#000',
                fontWeight: 'bold'
              }}
            />
          </Box>
        )}

        {/* Chord Diagram */}
        <Box sx={{ display: 'flex', justifyContent: 'center', bgcolor: '#fff', borderRadius: 1, p: 1 }}>
          <VexChordDiagram chord={chordData} width={100} height={120} />
        </Box>

        {/* Inversion Info */}
        {element.inversion !== undefined && (
          <Box>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Inversion:
            </Typography>
            <Typography sx={{ color: '#ff0', fontFamily: 'monospace' }}>
              {element.inversion === 0 ? 'Root Position' : `${element.inversion}${element.inversion === 1 ? 'st' : element.inversion === 2 ? 'nd' : 'rd'} Inversion`}
            </Typography>
          </Box>
        )}

        {/* Notes */}
        {element.notes && element.notes.length > 0 && (
          <Box>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Notes:
            </Typography>
            <Typography sx={{ color: '#ff0', fontFamily: 'monospace', fontSize: '14px' }}>
              {element.notes.join(' - ')}
            </Typography>
          </Box>
        )}
      </Stack>
    );
  };

  const renderFloor5Content = () => {
    // Floor 5: Voicings
    const voicing = element.voicing || [0, 2, 2, 1, 0, 0];
    const chordNotes: ChordNote[] = voicing.map((fret, index) => [index + 1, fret] as ChordNote);
    const chordData: ChordData = {
      chordNotes,
      position: element.fretRange?.min || 1,
      barres: []
    };

    return (
      <Stack spacing={2}>
        {/* Voicing Name */}
        <Box sx={{ textAlign: 'center' }}>
          <Typography sx={{ color: '#f90', fontSize: '18px', fontWeight: 'bold' }}>
            {element.name}
          </Typography>
        </Box>

        {/* CAGED Shape */}
        {element.cagedShape && (
          <Box sx={{ display: 'flex', justifyContent: 'center', gap: 1 }}>
            <Chip
              label={`CAGED: ${element.cagedShape}`}
              size="small"
              sx={{
                backgroundColor: '#f90',
                color: '#000',
                fontWeight: 'bold'
              }}
            />
            {element.difficulty && (
              <Chip
                label={element.difficulty}
                size="small"
                sx={{
                  backgroundColor: element.difficulty === 'Easy' ? '#0f0' : element.difficulty === 'Intermediate' ? '#ff0' : '#f00',
                  color: '#000',
                  fontWeight: 'bold'
                }}
              />
            )}
          </Box>
        )}

        {/* Chord Diagram */}
        <Box sx={{ display: 'flex', justifyContent: 'center', bgcolor: '#fff', borderRadius: 1, p: 1 }}>
          <VexChordDiagram chord={chordData} width={100} height={120} />
        </Box>

        {/* Fret Range */}
        {element.fretRange && (
          <Box>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Fret Range:
            </Typography>
            <Typography sx={{ color: '#f90', fontFamily: 'monospace' }}>
              {element.fretRange.min} - {element.fretRange.max}
            </Typography>
          </Box>
        )}

        {/* Tablature */}
        {element.tabNotation && (
          <Box sx={{ bgcolor: '#fff', borderRadius: 1, p: 1 }}>
            <VexTabViewer notation={element.tabNotation} showStandardNotation={false} />
          </Box>
        )}

        {/* Voicing Array */}
        {element.voicing && (
          <Box>
            <Typography variant="caption" sx={{ color: '#888' }}>
              Voicing:
            </Typography>
            <Typography sx={{ color: '#f90', fontFamily: 'monospace', fontSize: '12px' }}>
              [{element.voicing.map(f => f === -1 ? 'x' : f).join(', ')}]
            </Typography>
          </Box>
        )}
      </Stack>
    );
  };

  const renderContent = () => {
    if (element.floor <= 2) {
      return renderFloor0to2Content();
    } else if (element.floor <= 4) {
      return renderFloor3to4Content();
    } else {
      return renderFloor5Content();
    }
  };

  return (
    <Paper
      sx={{
        position: 'fixed',
        left: smartPosition.left,
        top: smartPosition.top,
        p: 2,
        backgroundColor: 'rgba(0, 0, 0, 0.95)',
        color: '#0f0',
        fontFamily: 'monospace',
        minWidth: 250,
        maxWidth: 350,
        border: `2px solid ${getFloorColor(element.floor)}`,
        boxShadow: `0 0 20px ${getFloorColor(element.floor)}`,
        zIndex: 10000,
        pointerEvents: 'none', // Don't block mouse events
        transition: 'left 0.1s ease-out, top 0.1s ease-out', // Smooth movement
      }}
    >
      <Stack spacing={2}>
        {/* Header */}
        <Box>
          <Typography variant="h6" sx={{ color: getFloorColor(element.floor), fontWeight: 'bold', fontSize: '14px' }}>
            {getFloorName(element.floor)}
          </Typography>
          <Typography sx={{ color: '#0f0', fontSize: '18px', fontWeight: 'bold' }}>
            {element.name}
          </Typography>
          {element.group && (
            <Typography variant="caption" sx={{ color: '#888' }}>
              Group: {element.group}
            </Typography>
          )}
        </Box>

        {/* Content based on floor */}
        {renderContent()}
      </Stack>
    </Paper>
  );
};

export default BSPElementDetailPanel;

