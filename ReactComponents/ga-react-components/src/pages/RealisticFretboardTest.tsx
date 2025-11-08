import React from 'react';
import { Container, Box, Typography } from '@mui/material';
import RealisticFretboard, { FretboardPosition } from '../components/RealisticFretboard';

// C Major chord positions
const cMajorChord: FretboardPosition[] = [
  { string: 0, fret: 0, label: 'E', color: '#4dabf7' },
  { string: 1, fret: 1, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 2, fret: 0, label: 'G', color: '#51cf66' },
  { string: 3, fret: 2, label: 'E', color: '#4dabf7' },
  { string: 4, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 5, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
];

export const RealisticFretboardTest: React.FC = () => {
  return (
    <Box sx={{ width: '100vw', minHeight: '100vh', p: 2 }}>
      <Box sx={{ mb: 3, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h3" gutterBottom>
          RealisticFretboard Test Page
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          This page is dedicated to testing the RealisticFretboard component (Pixi.js with WebGPU).
        </Typography>
      </Box>

      <Box sx={{ width: '100%', overflowX: 'auto' }}>
        <RealisticFretboard
          title="Realistic Fretboard (Pixi.js)"
          positions={cMajorChord}
          config={{
            fretCount: 22,
            stringCount: 6,
            guitarModel: 'electric_fender_strat',
            capoFret: 0,
            leftHanded: false,
            spacingMode: 'realistic',
            width: Math.min(typeof window !== 'undefined' ? window.innerWidth - 50 : 1900, 2400),
            height: 300,
          }}
        />
      </Box>

      <Box sx={{ mt: 4, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h5" gutterBottom>
          Features to Test:
        </Typography>
        <ul>
          <li>✅ Pixi.js WebGPU rendering</li>
          <li>✅ Capo position selector</li>
          <li>✅ Left-handed toggle</li>
          <li>✅ Guitar type selector</li>
          <li>✅ C Major chord positions</li>
          <li>✅ Realistic wood grain texture</li>
          <li>✅ Wound strings visualization</li>
          <li>✅ Metallic frets with highlights</li>
          <li>✅ Nut with string slots</li>
          <li>✅ Bridge rendering</li>
          <li>✅ Headstock</li>
          <li>✅ Body/strum zone</li>
          <li>✅ Neck profile</li>
          <li>✅ Inlays (position markers)</li>
        </ul>
      </Box>
    </Box>
  );
};

export default RealisticFretboardTest;

