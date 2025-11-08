import React from 'react';
import { Container, Box, Typography } from '@mui/material';
import { ThreeFretboard, ThreeFretboardPosition } from '../components/ThreeFretboard';

type FretboardPosition = ThreeFretboardPosition;

// C Major chord positions
const cMajorChord: FretboardPosition[] = [
  { string: 0, fret: 0, label: 'E', color: '#4dabf7' },
  { string: 1, fret: 1, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 2, fret: 0, label: 'G', color: '#51cf66' },
  { string: 3, fret: 2, label: 'E', color: '#4dabf7' },
  { string: 4, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 5, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
];

export const ThreeFretboardTest: React.FC = () => {
  return (
    <Box sx={{ width: '100vw', minHeight: '100vh', p: 2 }}>
      <Box sx={{ mb: 3, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h3" gutterBottom>
          ThreeFretboard Test Page
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          This page is dedicated to testing the ThreeFretboard component (Three.js 3D WebGPU).
        </Typography>
      </Box>

      <ThreeFretboard
        title="3D Fretboard (Three.js + WebGPU)"
        positions={cMajorChord}
        config={{
          fretCount: 22,
          stringCount: 6,
          guitarModel: 'electric_fender_strat',
          capoFret: 0,
          leftHanded: false,
          enableOrbitControls: true,
        }}
      />

      <Box sx={{ mt: 4, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h5" gutterBottom>
          Features to Test:
        </Typography>
        <ul>
          <li>✅ WebGPU/WebGL rendering</li>
          <li>✅ Fullscreen button</li>
          <li>✅ Capo position selector</li>
          <li>✅ Left-handed toggle</li>
          <li>✅ Guitar type selector</li>
          <li>✅ Orbit controls (drag to rotate, scroll to zoom)</li>
          <li>✅ C Major chord positions</li>
          <li>✅ Realistic materials (PBR)</li>
          <li>✅ Wound strings with normal mapping</li>
          <li>✅ Metallic frets</li>
          <li>✅ Nut with slots on top</li>
          <li>✅ Bridge and strum zone</li>
        </ul>
      </Box>
    </Box>
  );
};

export default ThreeFretboardTest;

