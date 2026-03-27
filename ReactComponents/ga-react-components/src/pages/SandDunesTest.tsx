import React from 'react';
import { Box, Typography, Paper } from '@mui/material';
import { SandDunes } from '../components/SandDunes';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

/**
 * Sand Dunes Test Page
 * 
 * Demonstrates advanced procedural sand dune terrain generation with:
 * - Ridged multifractal noise for realistic dune shapes
 * - Micro ripples perpendicular to wind direction
 * - Parallax Occlusion Mapping (POM) for depth
 * - Slope-based shading and color variation
 * - Physical sky (Hosek-Wilkie atmospheric model)
 * - Self-shadowing in ripple troughs
 * - Interactive camera controls
 */
const SandDunesTest: React.FC = () => {
  return (
    <DemoErrorBoundary demoName="Sand Dunes">
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
          🏜️ PROCEDURAL SAND DUNES TERRAIN
        </Typography>
        <Typography variant="body2" sx={{ color: '#ffffff', mb: 1 }}>
          Advanced desert landscape with procedural noise, POM, and physical sky
        </Typography>
        
        {/* Features */}
        <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap', mt: 2 }}>
          <Box>
            <Typography variant="caption" sx={{ color: '#00ff00', fontWeight: 'bold' }}>
              🌊 RIDGED MULTIFRACTAL
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              Sharp dune crests with realistic slip faces
            </Typography>
          </Box>
          
          <Box>
            <Typography variant="caption" sx={{ color: '#ffaa00', fontWeight: 'bold' }}>
              〰️ MICRO RIPPLES + POM
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              Parallax occlusion mapping for depth at close range
            </Typography>
          </Box>
          
          <Box>
            <Typography variant="caption" sx={{ color: '#ff00ff', fontWeight: 'bold' }}>
              🎨 ADVANCED SHADING
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              Self-shadowing, sparkle, sheen effects
            </Typography>
          </Box>
          
          <Box>
            <Typography variant="caption" sx={{ color: '#00aaff', fontWeight: 'bold' }}>
              ☀️ PHYSICAL SKY
            </Typography>
            <Typography variant="caption" display="block" sx={{ color: '#cccccc' }}>
              Hosek-Wilkie atmospheric scattering model
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
              • <strong>Resolution:</strong> 512×512 vertices
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Terrain Size:</strong> 3000×3000 meters
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Height Range:</strong> 0-38 meters
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Noise Octaves:</strong> 5 (ridged) + 4 (soft)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>POM Steps:</strong> 18 (near camera)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>POM Range:</strong> 0-180 meters
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Ripple Scale:</strong> 0.22 (spatial frequency)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Wind Direction:</strong> (1.0, 0.25) normalized
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Sky Model:</strong> Hosek-Wilkie (Three.js Sky)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Tone Mapping:</strong> ACES Filmic
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
              <strong>Scroll:</strong> Zoom in/out (10-2000m)
            </Typography>
          </Box>
        </Box>

        {/* Algorithm Description */}
        <Box sx={{ mt: 2, p: 1.5, bgcolor: 'rgba(0, 255, 0, 0.05)', borderRadius: 1, border: '1px solid #00ff00' }}>
          <Typography variant="caption" sx={{ color: '#00ff00', fontWeight: 'bold', display: 'block', mb: 1 }}>
            🧮 ADVANCED PROCEDURAL GENERATION
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block', mb: 0.5 }}>
            <strong>1. Macro Dunes:</strong> Ridged multifractal (5 octaves) + soft fBm (4 octaves) → sharp crests
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block', mb: 0.5 }}>
            <strong>2. Slip Faces:</strong> Sharpening function biases crests upward → realistic dune profiles
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block', mb: 0.5 }}>
            <strong>3. Micro Ripples:</strong> Saw wave + noise perpendicular to wind → ripple patterns
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block', mb: 0.5 }}>
            <strong>4. Parallax Occlusion:</strong> 18-step raymarch for depth when near camera (0-180m)
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block', mb: 0.5 }}>
            <strong>5. Self-Shadowing:</strong> Darken ripple troughs based on view angle → depth perception
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block', mb: 0.5 }}>
            <strong>6. Slope Masking:</strong> Fade ripples on steep slopes (&gt;0.7) → natural appearance
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block', mb: 0.5 }}>
            <strong>7. Shading:</strong> Height-based tint + slope darkening + sparkle/sheen
          </Typography>
          <Typography variant="caption" sx={{ color: '#cccccc', display: 'block' }}>
            <strong>8. Physical Sky:</strong> Hosek-Wilkie model with turbidity, Rayleigh, Mie scattering
          </Typography>
        </Box>

        {/* Sky Parameters */}
        <Box sx={{ mt: 2, p: 1.5, bgcolor: 'rgba(135, 206, 235, 0.1)', borderRadius: 1, border: '1px solid #87ceeb' }}>
          <Typography variant="caption" sx={{ color: '#87ceeb', fontWeight: 'bold', display: 'block', mb: 1 }}>
            ☀️ PHYSICAL SKY PARAMETERS
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 1 }}>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Turbidity:</strong> 2.2 (dust/haze)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Rayleigh:</strong> 2.8 (molecular scattering)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Mie Coefficient:</strong> 0.006 (aerosol density)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Mie Directional G:</strong> 0.8 (forward scatter)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Sun Elevation:</strong> 85° (near zenith)
            </Typography>
            <Typography variant="caption" sx={{ color: '#ffffff' }}>
              • <strong>Sun Azimuth:</strong> 25° (east-northeast)
            </Typography>
          </Box>
        </Box>
      </Paper>

      {/* 3D Viewport */}
      <Box sx={{ flex: 1, position: 'relative', overflow: 'hidden' }}>
        <SandDunes />
        
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
            🌍 CAMERA: Interactive (Orbit Controls)
          </Typography>
          <Typography variant="caption" sx={{ color: '#ffffff', display: 'block' }}>
            ☀️ SUN: 85° elevation, 25° azimuth
          </Typography>
          <Typography variant="caption" sx={{ color: '#ffffff', display: 'block' }}>
            🌫️ FOG: Exponential (density 0.00032)
          </Typography>
          <Typography variant="caption" sx={{ color: '#ffffff', display: 'block' }}>
            🎨 TONE MAPPING: ACES Filmic (exposure 1.35)
          </Typography>
        </Box>
      </Box>
    </Box>
    </DemoErrorBoundary>
  );
};

export default SandDunesTest;

