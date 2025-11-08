import React from 'react';
import { Container, Box, Typography } from '@mui/material';
import GuitarFretboard, { FretboardPosition } from '../components/GuitarFretboard';

// C Major chord positions
const cMajorChord: FretboardPosition[] = [
  { string: 0, fret: 0, label: 'E', color: '#4dabf7' },
  { string: 1, fret: 1, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 2, fret: 0, label: 'G', color: '#51cf66' },
  { string: 3, fret: 2, label: 'E', color: '#4dabf7' },
  { string: 4, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 5, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
];

export const GuitarFretboardTest: React.FC = () => {
  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" gutterBottom>
          GuitarFretboard Test Page
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          This page is dedicated to testing the GuitarFretboard component (SVG Legacy).
        </Typography>
      </Box>

      <GuitarFretboard
        title="Guitar Fretboard (SVG)"
        positions={cMajorChord}
        config={{
          fretCount: 22,
          stringCount: 6,
          showFretNumbers: true,
          showStringLabels: true,
          width: 1200,
          height: 200,
        }}
      />

      <Box sx={{ mt: 4 }}>
        <Typography variant="h5" gutterBottom>
          Features to Test:
        </Typography>
        <ul>
          <li>✅ SVG rendering</li>
          <li>✅ Position markers</li>
          <li>✅ Fret numbers</li>
          <li>✅ String labels</li>
          <li>✅ Interactive (click positions)</li>
          <li>❌ Capo support (not implemented)</li>
          <li>❌ Left-handed mode (not implemented)</li>
          <li>❌ Guitar model selection (not implemented)</li>
          <li>❌ Realistic rendering (not implemented)</li>
        </ul>
      </Box>
    </Container>
  );
};

export default GuitarFretboardTest;

