/**
 * Ocean Test Page
 *
 * Advanced ocean simulation with Gerstner waves, physical sky, and realistic water optics.
 */

import React from 'react';
import { Box, Typography, Paper } from '@mui/material';
import { Ocean } from '../components/Ocean';

const OceanTest: React.FC = () => {
  return (
    <Box sx={{ width: '100%', height: '100vh', display: 'flex', flexDirection: 'column', bgcolor: '#1a1a1a' }}>
      {/* Header */}
      <Paper
        elevation={3}
        sx={{
          p: 2,
          bgcolor: 'rgba(0, 0, 0, 0.8)',
          color: '#00ffff',
          borderRadius: 0,
          borderBottom: '2px solid #00ffff',
        }}
      >
        <Typography variant="h4" gutterBottom sx={{ fontFamily: 'monospace', textShadow: '0 0 10px #00ffff' }}>
          🌊 ADVANCED OCEAN SIMULATION
        </Typography>
        <Typography variant="body2" sx={{ color: '#ffffff', mb: 1 }}>
          Gerstner waves with physical sky, Fresnel reflections, and Beer's law absorption
        </Typography>

        {/* Features */}
        <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap', mt: 2 }}>
          <Box>
            <Typography variant="caption" sx={{ color: '#00ff00', fontWeight: 'bold' }}>
              🌊 GERSTNER WAVES
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              3-wave set: swell + mid + chop
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" sx={{ color: '#ffaa00', fontWeight: 'bold' }}>
              💎 FRESNEL REFLECTIONS
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              Schlick approximation (F0 = 0.02)
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" sx={{ color: '#ff00ff', fontWeight: 'bold' }}>
              🍺 BEER'S LAW
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              Depth-based color absorption
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" sx={{ color: '#00aaff', fontWeight: 'bold' }}>
              ☀️ PHYSICAL SKY
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              Hosek-Wilkie atmospheric scattering
            </Typography>
          </Box>
        </Box>

        {/* Technical Details */}
        <Box sx={{ mt: 2, p: 1.5, bgcolor: 'rgba(0, 255, 255, 0.1)', borderRadius: 1, border: '1px solid #00ffff' }}>
          <Typography variant="caption" sx={{ color: '#00ffff', fontWeight: 'bold', display: 'block', mb: 1 }}>
            📐 TECHNICAL SPECIFICATIONS
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 1 }}>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Ocean Size:</strong> 4000×4000 meters
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Resolution:</strong> 400×400 vertices
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Wave Lengths:</strong> 45m, 18m, 9m
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Wave Amplitudes:</strong> 0.70m, 0.35m, 0.22m
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Steepness:</strong> 0.90, 0.80, 0.70
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Foam Strength:</strong> 0.15
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Base Roughness:</strong> 0.02
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Max Roughness:</strong> 0.18
            </Typography>
          </Box>
        </Box>

        {/* Controls */}
        <Box sx={{ mt: 2, p: 1.5, bgcolor: 'rgba(255, 255, 255, 0.05)', borderRadius: 1 }}>
          <Typography variant="caption" sx={{ color: '#ffff00', fontWeight: 'bold', display: 'block', mb: 1 }}>
            🎮 CONTROLS
          </Typography>
          <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              <strong>Left Mouse:</strong> Rotate camera
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              <strong>Right Mouse:</strong> Pan view
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              <strong>Scroll:</strong> Zoom in/out (5-1000m)
            </Typography>
          </Box>
        </Box>
      </Paper>

      {/* 3D Viewport */}
      <Box sx={{ flex: 1, position: 'relative', overflow: 'hidden' }}>
        <Ocean />

        {/* Overlay Info */}
        <Box
          sx={{
            position: 'absolute',
            bottom: 16,
            left: 16,
            bgcolor: 'rgba(0, 0, 0, 0.7)',
            color: '#00ffff',
            p: 1.5,
            borderRadius: 1,
            border: '1px solid #00ffff',
            fontFamily: 'monospace',
            fontSize: '0.75rem',
          }}
        >
          <Typography variant="caption" sx={{ color: '#00ffff', display: 'block' }}>
            🌊 GERSTNER WAVES: 3-wave set (swell + mid + chop)
          </Typography>
          <Typography variant="caption" sx={{ color: '#ffffff', display: 'block' }}>
            💎 FRESNEL: Schlick approximation
          </Typography>
          <Typography variant="caption" sx={{ color: '#ffffff', display: 'block' }}>
            🍺 BEER'S LAW: σ = [0.12, 0.06, 0.02] m⁻¹
          </Typography>
          <Typography variant="caption" sx={{ color: '#ffffff', display: 'block' }}>
            ☀️ SUN: 78° elevation, 30° azimuth
          </Typography>
        </Box>
      </Box>
    </Box>
  );
};

export default OceanTest;

