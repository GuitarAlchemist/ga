/**
 * Minimal Three.js Instrument Test Page
 * 
 * Test page for the new MinimalThreeInstrument component that can render
 * any stringed instrument from the YAML database.
 */

import React from 'react';
import { Container, Typography, Box } from '@mui/material';
import { InstrumentShowcase } from '../components/MinimalThree';

const MinimalThreeTest: React.FC = () => {
  return (
    <Box sx={{ width: '100vw', minHeight: '100vh', p: 2 }}>
      <Box sx={{ mb: 3, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h3" gutterBottom>
          🎸 Minimal Three.js Instrument Test
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          This page demonstrates the new MinimalThreeInstrument component that can render
          ANY stringed instrument from the YAML database using a single ThreeJS + WebGPU component.
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          Features:
        </Typography>
        <ul>
          <li>Single component supports 60+ instrument families with 200+ variants</li>
          <li>Adaptive geometry based on string count, scale length, and body style</li>
          <li>WebGPU-first rendering with WebGL fallback</li>
          <li>Realistic materials for wood, strings, metal, and inlays</li>
          <li>Interactive 3D controls with orbit camera</li>
          <li>Automatic capo, left-handed, and position marker support</li>
        </ul>
      </Box>

      <InstrumentShowcase />
    </Box>
  );
};

export default MinimalThreeTest;
