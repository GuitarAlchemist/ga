import React from 'react';
import { Container, Box, Typography } from '@mui/material';
import WebGPUFretboard from '../components/WebGPUFretboard';
import { FretboardPosition } from '../components/WebGPUFretboard/types';

// C Major chord positions
const cMajorChord: FretboardPosition[] = [
  { string: 0, fret: 0, label: 'E', color: '#4dabf7' },
  { string: 1, fret: 1, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 2, fret: 0, label: 'G', color: '#51cf66' },
  { string: 3, fret: 2, label: 'E', color: '#4dabf7' },
  { string: 4, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 5, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
];

export const WebGPUFretboardTest: React.FC = () => {
  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" gutterBottom>
          WebGPUFretboard Test Page
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          This page is dedicated to testing the WebGPUFretboard component (Pixi.js v8 WebGPU).
        </Typography>
      </Box>

      <WebGPUFretboard
        title="WebGPU Fretboard (Pixi.js v8)"
        positions={cMajorChord}
        config={{
          fretCount: 22,
          stringCount: 6,
          guitarModel: 'classical',
          capoFret: 0,
          leftHanded: false,
          showFretNumbers: true,
          showStringLabels: true,
          showInlays: true,
          viewportWidth: 1400,
          viewportHeight: 250,
        }}
      />

      <Box sx={{ mt: 4 }}>
        <Typography variant="h5" gutterBottom>
          Features to Test:
        </Typography>
        <ul>
          <li>✅ Pixi.js v8 WebGPU rendering</li>
          <li>✅ Capo support</li>
          <li>✅ Left-handed mode</li>
          <li>✅ Position markers</li>
          <li>✅ Fret numbers</li>
          <li>✅ String labels</li>
          <li>✅ Inlays</li>
          <li>✅ Interactive (click positions)</li>
          <li>❌ Fullscreen (not implemented)</li>
          <li>❌ Realistic wood grain (not implemented)</li>
          <li>❌ Wound strings (not implemented)</li>
          <li>❌ Advanced materials (not implemented)</li>
        </ul>
      </Box>
    </Container>
  );
};

export default WebGPUFretboardTest;

