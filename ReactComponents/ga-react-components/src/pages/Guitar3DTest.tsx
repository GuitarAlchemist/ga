/**
 * Guitar3D Test Page
 *
 * Comprehensive demo page for the Guitar3D component
 * Showcases 3D guitar model loading with various controls and options
 */

import React, { useState } from 'react';
import { Box, Typography, Paper, Switch, FormControlLabel, TextField, Button, Alert, IconButton } from '@mui/material';
import { ChevronLeft, ChevronRight } from '@mui/icons-material';
import Guitar3D from '../components/Guitar3D/Guitar3D';

const Guitar3DTest: React.FC = () => {
  const [autoRotate, setAutoRotate] = useState(true);
  const [showGrid, setShowGrid] = useState(false);
  const [currentGuitarIndex, setCurrentGuitarIndex] = useState(0);

  // State for each guitar model
  const [modelInfo1, setModelInfo1] = useState<string | null>(null);
  const [modelInfo2, setModelInfo2] = useState<string | null>(null);
  const [modelInfo3, setModelInfo3] = useState<string | null>(null);

  // Helper function to create model load handler
  const createModelLoadHandler = (setModelInfo: (info: string) => void) => (gltf: any) => {
    console.log('Model loaded:', gltf);

    // Extract model information
    let meshCount = 0;
    let vertexCount = 0;
    let triangleCount = 0;

    gltf.scene.traverse((child: any) => {
      if (child.isMesh) {
        meshCount++;
        if (child.geometry) {
          const positions = child.geometry.attributes.position;
          if (positions) {
            vertexCount += positions.count;
          }
          if (child.geometry.index) {
            triangleCount += child.geometry.index.count / 3;
          }
        }
      }
    });

    setModelInfo(
      `Meshes: ${meshCount} | Vertices: ${vertexCount.toLocaleString()} | Triangles: ${triangleCount.toLocaleString()}`
    );
  };

  // Guitar models for carousel
  const guitarModels = [
    {
      name: 'Classical Guitar',
      modelPath: '/models/guitar.glb',
      color: '#4CAF50',
      description: 'Classical guitar (guitar.glb)',
      cameraPosition: [2, 1.5, 3] as [number, number, number],
      backgroundColor: '#1a1a1a',
      onLoad: createModelLoadHandler(setModelInfo1),
      modelInfo: modelInfo1,
    },
    {
      name: 'Fender Telecaster',
      modelPath: '/models/guitar2.glb',
      color: '#ff9800',
      description: 'Electric Guitar - Fender Telecaster by Sladegeorg (Sketchfab)',
      cameraPosition: [3, 2, 4] as [number, number, number],
      backgroundColor: '#2a2a2a',
      onLoad: createModelLoadHandler(setModelInfo2),
      modelInfo: modelInfo2,
      onError: (error: Error) => {
        console.warn('Failed to load Telecaster model:', error);
        setModelInfo2('⚠️ Model not found. Download Telecaster model and place at /models/guitar2.glb');
      },
    },
    {
      name: 'Electric Guitar',
      modelPath: '/models/guitar3.glb',
      color: '#2196f3',
      description: '⚠️ Needs electric guitar model (add to public/models/guitar3.glb)',
      cameraPosition: [2, 1.5, 3] as [number, number, number],
      backgroundColor: '#1a1a1a',
      onLoad: createModelLoadHandler(setModelInfo3),
      modelInfo: modelInfo3,
    },
  ];

  const currentGuitar = guitarModels[currentGuitarIndex];

  const handlePrevGuitar = () => {
    setCurrentGuitarIndex((prev) => (prev === 0 ? guitarModels.length - 1 : prev - 1));
  };

  const handleNextGuitar = () => {
    setCurrentGuitarIndex((prev) => (prev === guitarModels.length - 1 ? 0 : prev + 1));
  };

  return (
    <Box sx={{
      width: '100%',
      minHeight: '100vh',
      bgcolor: '#0a0a0a',
      color: '#ffffff !important',
      p: 0,
      '& *': { color: 'inherit' } // Ensure all children inherit white color
    }}>
      {/* Header */}
      <Box sx={{ bgcolor: '#1a1a1a', borderBottom: '1px solid #333', p: 3 }}>
        <Typography variant="h3" sx={{ fontWeight: 700, mb: 1, color: '#ffffff !important' }}>
          🎸 Guitar 3D Viewer
        </Typography>
        <Typography variant="subtitle1" sx={{ color: '#aaaaaa !important', mb: 2 }}>
          Interactive 3D guitar model viewer with GLTF/GLB support
        </Typography>

        {/* Setup Instructions Alert */}
        <Alert severity="info" sx={{ mb: 2 }}>
          <strong>ℹ️ Guitar Model Gallery:</strong> All available guitar models are displayed below side-by-side
          <div style={{ marginTop: '8px' }}>
            <strong>📥 Currently Loaded:</strong>
          </div>
          <ul style={{ marginTop: '8px', marginBottom: 0, paddingLeft: '20px' }}>
            <li><strong>Guitar Model 1:</strong> ✅ Classical Guitar (9,125 vertices) - Currently using guitar.glb</li>
            <li><strong>Guitar Model 2:</strong> ⚠️ Acoustic Guitar - Needs proper model (currently using guitar.glb as placeholder)</li>
            <li><strong>Guitar Model 3:</strong> ⚠️ Electric Guitar - Needs proper model (add to <code>public/models/guitar3.glb</code>)</li>
          </ul>
          <div style={{ marginTop: '8px' }}>
            <strong>🎮 Controls:</strong> Use "Auto Rotate All" and "Show Grid" toggles above to control all models simultaneously
          </div>
        </Alert>

        {/* Technical Specifications */}
        <Paper sx={{ bgcolor: '#252525', p: 2, mb: 2 }}>
          <Typography variant="h6" sx={{ color: '#4CAF50', mb: 1 }}>
            📊 Technical Specifications
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 1 }}>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>Format:</strong> GLTF/GLB (GL Transmission Format)
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>Renderer:</strong> Three.js WebGPU (fallback to WebGL)
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>Antialiasing:</strong> 8x MSAA (WebGPU) / Standard (WebGL)
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>Tone Mapping:</strong> ACES Filmic
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>Shadows:</strong> PCF Soft Shadow Maps (WebGL only)
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>Controls:</strong> Orbit (drag to rotate, scroll to zoom)
            </Typography>
          </Box>
        </Paper>

        {/* Features */}
        <Paper sx={{ bgcolor: '#252525', p: 2, mb: 2 }}>
          <Typography variant="h6" sx={{ color: '#4CAF50', mb: 1 }}>
            ✨ Features
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 1 }}>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>✓ GLTF/GLB Loading:</strong> Industry-standard 3D format
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>✓ PBR Materials:</strong> Physically-based rendering
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>✓ Auto-Centering:</strong> Automatic model positioning
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>✓ Auto-Scaling:</strong> Fits model to viewport
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>✓ Shadow Casting:</strong> Realistic shadow rendering
            </Typography>
            <Typography variant="caption" sx={{ color: '#cccccc' }}>
              <strong>✓ Progress Indicator:</strong> Loading feedback
            </Typography>
          </Box>
        </Paper>

        {/* Controls */}
        <Paper sx={{ bgcolor: '#252525', p: 2 }}>
          <Typography variant="h6" sx={{ color: '#4CAF50 !important', mb: 1 }}>
            🎮 Global Controls
          </Typography>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
            <FormControlLabel
              control={
                <Switch
                  checked={autoRotate}
                  onChange={(e) => setAutoRotate(e.target.checked)}
                  sx={{ '& .MuiSwitch-switchBase.Mui-checked': { color: '#4CAF50' } }}
                />
              }
              label="Auto Rotate All"
              sx={{
                color: '#ffffff !important',
                '& .MuiFormControlLabel-label': { color: '#ffffff !important' }
              }}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={showGrid}
                  onChange={(e) => setShowGrid(e.target.checked)}
                  sx={{ '& .MuiSwitch-switchBase.Mui-checked': { color: '#4CAF50' } }}
                />
              }
              label="Show Grid"
              sx={{
                color: '#ffffff !important',
                '& .MuiFormControlLabel-label': { color: '#ffffff !important' }
              }}
            />
          </Box>
        </Paper>
      </Box>

      {/* 3D Viewport - Carousel */}
      <Box sx={{ p: 3, width: '100%', maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h5" sx={{ color: '#4CAF50 !important', mb: 3, textAlign: 'center' }}>
          🎸 Guitar Model Gallery
        </Typography>

        {/* Carousel Container */}
        <Box sx={{ position: 'relative', width: '100%' }}>
          {/* Navigation Buttons */}
          <IconButton
            onClick={handlePrevGuitar}
            sx={{
              position: 'absolute',
              left: -60,
              top: '50%',
              transform: 'translateY(-50%)',
              bgcolor: 'rgba(76, 175, 80, 0.2)',
              color: '#4CAF50',
              '&:hover': { bgcolor: 'rgba(76, 175, 80, 0.4)' },
              zIndex: 10,
            }}
          >
            <ChevronLeft fontSize="large" />
          </IconButton>

          <IconButton
            onClick={handleNextGuitar}
            sx={{
              position: 'absolute',
              right: -60,
              top: '50%',
              transform: 'translateY(-50%)',
              bgcolor: 'rgba(76, 175, 80, 0.2)',
              color: '#4CAF50',
              '&:hover': { bgcolor: 'rgba(76, 175, 80, 0.4)' },
              zIndex: 10,
            }}
          >
            <ChevronRight fontSize="large" />
          </IconButton>

          {/* Current Guitar Display */}
          <Paper sx={{ bgcolor: '#1a1a1a', p: 3, width: '100%' }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6" sx={{ color: `${currentGuitar.color} !important` }}>
                Guitar Model {currentGuitarIndex + 1} - {currentGuitar.name}
              </Typography>
              <Typography variant="caption" sx={{ color: '#888 !important' }}>
                {currentGuitarIndex + 1} / {guitarModels.length}
              </Typography>
            </Box>

            <Typography variant="caption" sx={{ color: `${currentGuitar.color} !important`, display: 'block', textAlign: 'center', mb: 3 }}>
              {currentGuitar.description}
            </Typography>

            <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', width: '100%' }}>
              <Guitar3D
                key={currentGuitar.modelPath}
                modelPath={currentGuitar.modelPath}
                width={800}
                height={600}
                autoRotate={autoRotate}
                showGrid={showGrid}
                backgroundColor={currentGuitar.backgroundColor}
                cameraPosition={currentGuitar.cameraPosition}
                onLoad={currentGuitar.onLoad}
                onError={currentGuitar.onError}
              />
            </Box>

            {currentGuitar.modelInfo && (
              <Alert
                severity={currentGuitar.modelInfo.includes('⚠️') ? 'warning' : 'success'}
                sx={{ mt: 3, fontSize: '14px' }}
              >
                {currentGuitar.modelInfo}
              </Alert>
            )}

            {currentGuitarIndex === 1 && (
              <Box sx={{ mt: 2, p: 2, bgcolor: '#2a2a2a', borderRadius: 1 }}>
                <Typography variant="body2" sx={{ color: '#aaa !important' }}>
                  <strong>Model Info:</strong> Fender Telecaster by Sladegeorg<br/>
                  <strong>Triangles:</strong> 25.4k | <strong>Vertices:</strong> 20.5k<br/>
                  <strong>Download:</strong> Run <code>.\scripts\download-telecaster-model.ps1</code>
                </Typography>
              </Box>
            )}

            {/* Carousel Indicators */}
            <Box sx={{ display: 'flex', justifyContent: 'center', gap: 1, mt: 3 }}>
              {guitarModels.map((_, index) => (
                <Box
                  key={index}
                  onClick={() => setCurrentGuitarIndex(index)}
                  sx={{
                    width: 12,
                    height: 12,
                    borderRadius: '50%',
                    bgcolor: index === currentGuitarIndex ? '#4CAF50' : '#555',
                    cursor: 'pointer',
                    transition: 'all 0.3s',
                    '&:hover': { bgcolor: index === currentGuitarIndex ? '#4CAF50' : '#777' },
                  }}
                />
              ))}
            </Box>
          </Paper>
        </Box>

        {/* Instructions */}
        <Paper sx={{ bgcolor: '#252525', p: 2 }}>
          <Typography variant="h6" sx={{ color: '#4CAF50', mb: 1 }}>
            📖 How to Use
          </Typography>
          <Typography variant="body2" sx={{ color: '#cccccc', mb: 1 }}>
            <strong>Mouse Controls:</strong>
          </Typography>
          <ul style={{ color: '#aaaaaa', fontSize: '14px', marginTop: 0 }}>
            <li><strong>Left Click + Drag:</strong> Rotate camera around model</li>
            <li><strong>Right Click + Drag:</strong> Pan camera</li>
            <li><strong>Scroll Wheel:</strong> Zoom in/out</li>
          </ul>
          <Typography variant="body2" sx={{ color: '#cccccc', mb: 1, mt: 2 }}>
            <strong>Loading Your Own Models:</strong>
          </Typography>
          <ul style={{ color: '#aaaaaa', fontSize: '14px', marginTop: 0 }}>
            <li>Place your <code>.glb</code> or <code>.gltf</code> file in <code>public/models/</code></li>
            <li>Enter the path (e.g., <code>/models/myguitar.glb</code>) in the input field above</li>
            <li>Click "Load" to display your model</li>
            <li>Or use a direct URL to a model hosted online</li>
          </ul>
          <Typography variant="body2" sx={{ color: '#cccccc', mb: 1, mt: 2 }}>
            <strong>Free Guitar Models:</strong>
          </Typography>
          <ul style={{ color: '#aaaaaa', fontSize: '14px', marginTop: 0 }}>
            <li>
              <strong>Sketchfab (Recommended):</strong>{' '}
              <a
                href="https://sketchfab.com/search?q=guitar&type=models&features=downloadable&sort_by=-likeCount"
                target="_blank"
                rel="noopener noreferrer"
                style={{ color: '#4CAF50', textDecoration: 'underline' }}
              >
                Search for "guitar"
              </a>
              {' '}with CC0 or CC Attribution license (requires free account)
            </li>
            <li><strong>BlenderKit:</strong> Free Blender addon with guitar models (export as GLB)</li>
            <li><strong>Poly Haven:</strong> Free 3D models (limited guitar selection)</li>
            <li><strong>Free3D:</strong> Various free guitar models (may need format conversion)</li>
          </ul>
          <Typography variant="body2" sx={{ color: '#cccccc', mb: 1, mt: 2 }}>
            <strong>⚠️ Note:</strong> Direct URLs to Sketchfab downloads don't work due to CORS restrictions.
            You must download the model file and place it in the <code>public/models/</code> directory.
          </Typography>
        </Paper>
      </Box>
    </Box>
  );
};

export default Guitar3DTest;

