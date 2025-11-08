/**
 * OceanDemo - Demo page for Ocean Component
 * 
 * Demonstrates realistic ocean water with interactive controls
 */

import React, { useState } from 'react';
import { Box, Typography, Paper, Stack, Slider } from '@mui/material';
import * as THREE from 'three';
import { Ocean } from './Ocean';

export const OceanDemo: React.FC = () => {
  return (
    <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
      <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
        {/* Main Visualization - Full screen */}
        <Box sx={{ flex: 1, display: 'flex', position: 'relative' }}>
          <Ocean
            width={window.innerWidth - 320}
            height={window.innerHeight - 48}
          />
        </Box>

        {/* Controls Panel */}
        <Paper
          sx={{
            width: 320,
            padding: 3,
            backgroundColor: 'rgba(0, 0, 0, 0.9)',
            border: '1px solid #0af',
            overflowY: 'auto',
          }}
        >
          <Typography variant="h5" sx={{ color: '#0af', fontFamily: 'monospace', mb: 3 }}>
            🌊 OCEAN SHADER
          </Typography>

          <Typography variant="subtitle2" sx={{ color: '#0af', fontFamily: 'monospace', mb: 1 }}>
            OCEAN SIMULATION
          </Typography>

          <Typography variant="body2" sx={{ color: '#0af', fontFamily: 'monospace', mb: 2, opacity: 0.7 }}>
            Realistic ocean with Gerstner waves, physical sky, and Fresnel reflections.
          </Typography>

          <Typography variant="caption" sx={{ color: '#0af', fontFamily: 'monospace', display: 'block', mt: 3 }}>
            Based on: threejs.org/examples
          </Typography>
          <Typography variant="caption" sx={{ color: '#0af', fontFamily: 'monospace', display: 'block' }}>
            Technique: Gerstner waves
          </Typography>
          <Typography variant="caption" sx={{ color: '#0af', fontFamily: 'monospace', display: 'block' }}>
            Features: Fresnel, Specular, Diffuse
          </Typography>
        </Paper>
      </Stack>
    </Box>
  );
};

export default OceanDemo;

