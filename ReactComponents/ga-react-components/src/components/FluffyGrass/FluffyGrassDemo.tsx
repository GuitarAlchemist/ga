/**
 * FluffyGrassDemo - Demo page for Fluffy Grass Component
 * 
 * Demonstrates the fluffy grass with interactive controls
 */

import React, { useState } from 'react';
import { Box, Typography, Paper, Stack, Slider } from '@mui/material';
import * as THREE from 'three';
import { FluffyGrass } from './FluffyGrass';

export const FluffyGrassDemo: React.FC = () => {
  const [grassDensity, setGrassDensity] = useState<number>(500);
  const [chunkSize, setChunkSize] = useState<number>(10);
  const [chunkCount, setChunkCount] = useState<number>(8);
  const [grassHeight, setGrassHeight] = useState<number>(1.0);
  const [grassWidth, setGrassWidth] = useState<number>(0.1);
  const [windSpeed, setWindSpeed] = useState<number>(0.0); // Start with no wind
  const [windStrength, setWindStrength] = useState<number>(0.3);

  return (
    <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
      <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
        {/* Main Visualization - Full screen */}
        <Box sx={{ flex: 1, display: 'flex', position: 'relative' }}>
          <FluffyGrass
            width={window.innerWidth - 320}
            height={window.innerHeight - 48}
            grassDensity={grassDensity}
            chunkSize={chunkSize}
            chunkCount={chunkCount}
            grassHeight={grassHeight}
            grassWidth={grassWidth}
            windSpeed={windSpeed}
            windStrength={windStrength}
          />
        </Box>

        {/* Controls Panel */}
        <Paper
          sx={{
            width: 320,
            padding: 3,
            backgroundColor: 'rgba(0, 0, 0, 0.9)',
            border: '1px solid #0f0',
            overflowY: 'auto',
          }}
        >
          <Typography variant="h5" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 3 }}>
            🌾 FLUFFY GRASS
          </Typography>

          <Typography variant="subtitle2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
            CONTROLS
          </Typography>

          {/* Grass Density */}
          <Box sx={{ mb: 3 }}>
            <Typography variant="body2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
              Grass Density: {grassDensity}
            </Typography>
            <Slider
              value={grassDensity}
              onChange={(_, value) => setGrassDensity(value as number)}
              min={50}
              max={2000}
              step={50}
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-track': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-rail': {
                  backgroundColor: '#0f0',
                  opacity: 0.3,
                },
              }}
            />
          </Box>

          {/* Chunk Size */}
          <Box sx={{ mb: 3 }}>
            <Typography variant="body2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
              Chunk Size: {chunkSize}
            </Typography>
            <Slider
              value={chunkSize}
              onChange={(_, value) => setChunkSize(value as number)}
              min={5}
              max={20}
              step={1}
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-track': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-rail': {
                  backgroundColor: '#0f0',
                  opacity: 0.3,
                },
              }}
            />
          </Box>

          {/* Chunk Count */}
          <Box sx={{ mb: 3 }}>
            <Typography variant="body2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
              Chunk Count: {chunkCount}x{chunkCount}
            </Typography>
            <Slider
              value={chunkCount}
              onChange={(_, value) => setChunkCount(value as number)}
              min={2}
              max={16}
              step={2}
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-track': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-rail': {
                  backgroundColor: '#0f0',
                  opacity: 0.3,
                },
              }}
            />
          </Box>

          {/* Grass Height */}
          <Box sx={{ mb: 3 }}>
            <Typography variant="body2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
              Grass Height: {grassHeight.toFixed(1)}
            </Typography>
            <Slider
              value={grassHeight}
              onChange={(_, value) => setGrassHeight(value as number)}
              min={0.5}
              max={3.0}
              step={0.1}
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-track': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-rail': {
                  backgroundColor: '#0f0',
                  opacity: 0.3,
                },
              }}
            />
          </Box>

          {/* Grass Width */}
          <Box sx={{ mb: 3 }}>
            <Typography variant="body2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
              Grass Width: {grassWidth.toFixed(2)}
            </Typography>
            <Slider
              value={grassWidth}
              onChange={(_, value) => setGrassWidth(value as number)}
              min={0.05}
              max={0.3}
              step={0.01}
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-track': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-rail': {
                  backgroundColor: '#0f0',
                  opacity: 0.3,
                },
              }}
            />
          </Box>

          {/* Wind Speed */}
          <Box sx={{ mb: 3 }}>
            <Typography variant="body2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
              Wind Speed: {windSpeed.toFixed(1)}
            </Typography>
            <Slider
              value={windSpeed}
              onChange={(_, value) => setWindSpeed(value as number)}
              min={0}
              max={5}
              step={0.1}
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-track': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-rail': {
                  backgroundColor: '#0f0',
                  opacity: 0.3,
                },
              }}
            />
          </Box>

          {/* Wind Strength */}
          <Box sx={{ mb: 3 }}>
            <Typography variant="body2" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 1 }}>
              Wind Strength: {windStrength.toFixed(2)}
            </Typography>
            <Slider
              value={windStrength}
              onChange={(_, value) => setWindStrength(value as number)}
              min={0}
              max={1}
              step={0.05}
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-track': {
                  backgroundColor: '#0f0',
                },
                '& .MuiSlider-rail': {
                  backgroundColor: '#0f0',
                  opacity: 0.3,
                },
              }}
            />
          </Box>

          <Typography variant="caption" sx={{ color: '#0f0', fontFamily: 'monospace', display: 'block', mt: 3 }}>
            Based on: tympanus.net/codrops
          </Typography>
          <Typography variant="caption" sx={{ color: '#0f0', fontFamily: 'monospace', display: 'block' }}>
            Technique: Billboard grass with instancing
          </Typography>
        </Paper>
      </Stack>
    </Box>
  );
};

export default FluffyGrassDemo;

