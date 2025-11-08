import React, { useState } from 'react';
import { Container, Typography, Box, Paper, Alert } from '@mui/material';
import { MusicTheorySelector, MusicTheoryContext } from '../components/MusicTheorySelector';
import { MinimalThreeInstrument } from '../components/MinimalThree';
import { InstrumentConfig, FretboardPosition } from '../types/InstrumentConfig';

/**
 * Test page for MusicTheorySelector integration with fretboard
 * 
 * This demonstrates:
 * 1. Selecting a key/mode from the backend API
 * 2. Highlighting scale tones on the fretboard
 * 3. Real-time updates when music theory context changes
 */
const MusicTheoryTest: React.FC = () => {
  const [musicTheoryContext, setMusicTheoryContext] = useState<MusicTheoryContext>({
    tonality: 'atonal'
  });

  const [positions, setPositions] = useState<FretboardPosition[]>([]);

  // Standard guitar configuration
  const guitarConfig: InstrumentConfig = {
    family: 'Guitar',
    variant: 'Standard',
    displayName: 'Standard Guitar',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 19,
    bodyStyle: 'electric'
  };

  // Handle music theory context changes
  const handleContextChange = (context: MusicTheoryContext) => {
    console.log('Music theory context changed:', context);
    setMusicTheoryContext(context);

    // Generate fretboard positions for the selected key/mode
    if (context.tonality === 'tonal' && context.notes && context.notes.length > 0) {
      const newPositions = generatePositionsForNotes(context.notes, guitarConfig.tuning);
      setPositions(newPositions);
    } else {
      setPositions([]);
    }
  };

  return (
    <Box sx={{ width: '100vw', minHeight: '100vh', p: 2 }}>
      <Box sx={{ mb: 3, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h3" gutterBottom>
          🎵 Music Theory Selector Test
        </Typography>

        <Typography variant="body1" paragraph>
          This page demonstrates the integration of the MusicTheorySelector component with the fretboard.
          Select a key and mode to see the scale tones highlighted on the fretboard.
        </Typography>

        <Alert severity="info" sx={{ mb: 3 }}>
          <strong>Backend Required:</strong> Make sure the GaApi backend is running on{' '}
          <code>http://localhost:7001</code>. Run <code>.\Scripts\start-all.ps1 -Dashboard</code> to start all services.
        </Alert>
      </Box>

      {/* Music Theory Selector */}
      <MusicTheorySelector
        context={musicTheoryContext}
        onContextChange={handleContextChange}
        apiBaseUrl="http://localhost:7001"
      />

      {/* Current Context Display */}
      <Paper elevation={2} sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Current Context
        </Typography>
        <Box component="pre" sx={{ fontSize: '0.875rem', overflow: 'auto' }}>
          {JSON.stringify(musicTheoryContext, null, 2)}
        </Box>
      </Paper>

      {/* Fretboard Visualization */}
      <Paper elevation={2} sx={{ p: 2, width: '100%' }}>
        <Box sx={{ maxWidth: '1200px', mx: 'auto', mb: 2 }}>
          <Typography variant="h6" gutterBottom>
            Fretboard Visualization
          </Typography>
          <Typography variant="body2" color="text.secondary" paragraph>
            {positions.length > 0
              ? `Showing ${positions.length} positions for ${musicTheoryContext.key}`
              : 'Select a key to see scale tones on the fretboard'}
          </Typography>
        </Box>

        <Box sx={{ width: '100%', height: 600, bgcolor: '#1a1a1a', borderRadius: 1, display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
          <MinimalThreeInstrument
            instrument={guitarConfig}
            positions={positions}
            renderMode="3d-webgl"
            viewMode="fretboard"
            capoFret={0}
            leftHanded={false}
            showLabels={true}
            showInlays={true}
            enableOrbitControls={true}
            width={Math.min(window.innerWidth - 100, 2000)}
            height={600}
            onPositionClick={(string, fret) => {
              console.log(`Clicked: String ${string}, Fret ${fret}`);
            }}
            onPositionHover={(string, fret) => {
              if (string !== null && fret !== null) {
                console.log(`Hovering: String ${string}, Fret ${fret}`);
              }
            }}
          />
        </Box>
      </Paper>

      {/* Instructions */}
      <Box sx={{ maxWidth: '1200px', mx: 'auto' }}>
        <Paper elevation={2} sx={{ p: 3, mt: 3 }}>
          <Typography variant="h6" gutterBottom>
            How to Use
          </Typography>
          <ol>
            <li>
              <strong>Start the backend:</strong> Run <code>.\Scripts\start-all.ps1 -Dashboard</code> from the repository root
            </li>
            <li>
              <strong>Select Tonality:</strong> Choose "Tonal" to enable key/mode selection
            </li>
            <li>
              <strong>Select Key:</strong> Choose a musical key (e.g., "Key of C", "Key of Am")
            </li>
            <li>
              <strong>Select Mode:</strong> Choose a mode (e.g., "Ionian", "Dorian", "Phrygian")
            </li>
            <li>
              <strong>View Scale Tones:</strong> The fretboard will highlight all notes in the selected key/mode
            </li>
            <li>
              <strong>Interact:</strong> Hover over positions to see string/fret information
            </li>
          </ol>
        </Paper>
      </Box>
    </Box>
  );
};

/**
 * Generate fretboard positions for a set of notes
 * 
 * This is a simplified implementation that finds all occurrences of the notes
 * on the fretboard up to the 12th fret.
 */
function generatePositionsForNotes(
  notes: string[],
  tuning: string[]
): FretboardPosition[] {
  const positions: FretboardPosition[] = [];

  // Note names without octave numbers
  const noteNames = notes.map(note => note.replace(/\d+$/, ''));

  // Chromatic scale
  const chromaticScale = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];

  // For each string
  tuning.forEach((stringNote, stringIndex) => {
    // Get the root note of the string (without octave)
    const rootNote = stringNote.replace(/\d+$/, '');
    const rootIndex = chromaticScale.indexOf(rootNote);

    if (rootIndex === -1) return;

    // Check each fret (0-12)
    for (let fret = 0; fret <= 12; fret++) {
      const noteIndex = (rootIndex + fret) % 12;
      const noteName = chromaticScale[noteIndex];

      // Check if this note is in our scale
      if (noteNames.includes(noteName)) {
        // Determine if this is the root note
        const isRoot = noteName === noteNames[0];

        positions.push({
          string: stringIndex,
          fret: fret,
          label: noteName,
          color: isRoot ? '#ff6b6b' : '#4dabf7', // Red for root, blue for other scale tones
          emphasized: isRoot
        });
      }
    }
  });

  return positions;
}

export default MusicTheoryTest;

