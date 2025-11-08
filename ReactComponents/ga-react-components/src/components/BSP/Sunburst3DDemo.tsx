/**
 * Sunburst3DDemo - Demo page for 3D Sunburst Visualization
 * 
 * Demonstrates the 3D sunburst with musical hierarchy data
 */

import React, { useState } from 'react';
import { Box, Typography, Paper, Stack, Slider, FormControlLabel, Switch } from '@mui/material';
import { Sunburst3D, SunburstNode } from './Sunburst3D';

// ==================
// Sample Data
// ==================

/**
 * Musical hierarchy data for demonstration
 */
const musicalHierarchyData: SunburstNode = {
  name: 'Music Theory',
  children: [
    {
      name: 'Pitch Class Sets',
      color: 0x00ffff,
      children: [
        {
          name: 'Trichords',
          value: 12,
          children: [
            { name: '3-1', value: 1 },
            { name: '3-2', value: 1 },
            { name: '3-3', value: 1 },
            { name: '3-4', value: 1 },
            { name: '3-5', value: 1 },
            { name: '3-6', value: 1 },
            { name: '3-7', value: 1 },
            { name: '3-8', value: 1 },
            { name: '3-9', value: 1 },
            { name: '3-10', value: 1 },
            { name: '3-11', value: 1 },
            { name: '3-12', value: 1 },
          ],
        },
        {
          name: 'Tetrachords',
          value: 29,
          children: [
            { name: '4-1', value: 1 },
            { name: '4-2', value: 1 },
            { name: '4-3', value: 1 },
            { name: '4-4', value: 1 },
            { name: '4-5', value: 1 },
            { name: '4-Z15', value: 1 },
            { name: '4-Z29', value: 1 },
            // ... more tetrachords
          ],
        },
        {
          name: 'Pentachords',
          value: 38,
        },
        {
          name: 'Hexachords',
          value: 50,
        },
      ],
    },
    {
      name: 'Chords',
      color: 0xffff00,
      children: [
        {
          name: 'Triads',
          children: [
            {
              name: 'Major',
              value: 12,
              children: [
                { name: 'C Major', value: 1 },
                { name: 'C# Major', value: 1 },
                { name: 'D Major', value: 1 },
                { name: 'Eb Major', value: 1 },
                { name: 'E Major', value: 1 },
                { name: 'F Major', value: 1 },
                { name: 'F# Major', value: 1 },
                { name: 'G Major', value: 1 },
                { name: 'Ab Major', value: 1 },
                { name: 'A Major', value: 1 },
                { name: 'Bb Major', value: 1 },
                { name: 'B Major', value: 1 },
              ],
            },
            {
              name: 'Minor',
              value: 12,
            },
            {
              name: 'Diminished',
              value: 12,
            },
            {
              name: 'Augmented',
              value: 4,
            },
          ],
        },
        {
          name: 'Seventh Chords',
          children: [
            { name: 'Major 7th', value: 12 },
            { name: 'Minor 7th', value: 12 },
            { name: 'Dominant 7th', value: 12 },
            { name: 'Half-Diminished', value: 12 },
            { name: 'Diminished 7th', value: 3 },
          ],
        },
        {
          name: 'Extended Chords',
          children: [
            { name: '9th Chords', value: 24 },
            { name: '11th Chords', value: 24 },
            { name: '13th Chords', value: 24 },
          ],
        },
      ],
    },
    {
      name: 'Voicings',
      color: 0xff8800,
      children: [
        {
          name: 'Jazz Voicings',
          color: 0x00ff00,
          children: [
            {
              name: 'Drop 2',
              children: [
                { name: 'Maj7', value: 48 },
                { name: 'Min7', value: 48 },
                { name: 'Dom7', value: 48 },
                { name: 'Min7b5', value: 48 },
                { name: 'Dim7', value: 12 },
              ],
            },
            {
              name: 'Drop 3',
              children: [
                { name: 'Maj7', value: 48 },
                { name: 'Min7', value: 48 },
                { name: 'Dom7', value: 48 },
                { name: 'Min7b5', value: 48 },
              ],
            },
            {
              name: 'Drop 2+4',
              children: [
                { name: 'Maj7', value: 48 },
                { name: 'Min7', value: 48 },
                { name: 'Dom7', value: 48 },
              ],
            },
            {
              name: 'Rootless',
              children: [
                { name: 'Type A', value: 24 },
                { name: 'Type B', value: 24 },
              ],
            },
            {
              name: 'Shell Voicings',
              children: [
                { name: 'Root-3-7', value: 24 },
                { name: 'Root-7-3', value: 24 },
              ],
            },
            {
              name: 'Quartal',
              children: [
                { name: '4ths', value: 36 },
                { name: 'Sus4', value: 24 },
                { name: 'Add11', value: 24 },
              ],
            },
          ],
        },
        {
          name: 'Classical Voicings',
          color: 0x0088ff,
          children: [
            { name: 'Close Position', value: 100 },
            { name: 'Open Position', value: 100 },
            { name: 'Four-Part', value: 80 },
            { name: 'SATB', value: 80 },
          ],
        },
        {
          name: 'Rock Voicings',
          color: 0xff0088,
          children: [
            { name: 'Power Chords', value: 60 },
            { name: 'Barre Chords', value: 120 },
            { name: 'Open Chords', value: 80 },
            { name: 'Triads', value: 100 },
          ],
        },
        {
          name: 'CAGED System',
          color: 0xffff00,
          children: [
            { name: 'C Shape', value: 50 },
            { name: 'A Shape', value: 50 },
            { name: 'G Shape', value: 50 },
            { name: 'E Shape', value: 50 },
            { name: 'D Shape', value: 50 },
          ],
        },
      ],
    },
    {
      name: 'Scales',
      color: 0xff00ff,
      children: [
        {
          name: 'Major Scales',
          children: [
            { name: 'Ionian', value: 12 },
            { name: 'Dorian', value: 12 },
            { name: 'Phrygian', value: 12 },
            { name: 'Lydian', value: 12 },
            { name: 'Mixolydian', value: 12 },
            { name: 'Aeolian', value: 12 },
            { name: 'Locrian', value: 12 },
          ],
        },
        {
          name: 'Melodic Minor',
          value: 84,
        },
        {
          name: 'Harmonic Minor',
          value: 84,
        },
        {
          name: 'Pentatonic',
          children: [
            { name: 'Major Pentatonic', value: 12 },
            { name: 'Minor Pentatonic', value: 12 },
          ],
        },
        {
          name: 'Blues Scales',
          value: 12,
        },
      ],
    },
  ],
};

// ==================
// Demo Component
// ==================

export const Sunburst3DDemo: React.FC = () => {
  const [maxDepth, setMaxDepth] = useState<number>(4);
  const [slopeAngle, setSlopeAngle] = useState<number>(30);
  const [autoRotate, setAutoRotate] = useState<boolean>(true);
  const [selectedNode, setSelectedNode] = useState<SunburstNode | null>(null);
  const [selectedPath, setSelectedPath] = useState<string[]>([]);

  const handleNodeClick = (node: SunburstNode, path: string[]) => {
    setSelectedNode(node);
    setSelectedPath(path);
    console.log('Clicked node:', node.name, 'Path:', path.join(' → '));
  };

  return (
    <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
      <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
        {/* Main Visualization - Full screen */}
        <Box sx={{ flex: 1, display: 'flex', position: 'relative' }}>
          <Sunburst3D
            data={musicalHierarchyData}
            width={window.innerWidth - 320}
            height={window.innerHeight - 48}
            maxDepth={maxDepth}
            slopeAngle={slopeAngle}
            onNodeClick={handleNodeClick}
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
            3D Sunburst Controls
          </Typography>

          {/* Max Depth Slider */}
          <Box sx={{ mb: 3 }}>
            <Typography sx={{ color: '#0f0', fontSize: '14px', mb: 1 }}>
              Max Depth (LOD): {maxDepth}
            </Typography>
            <Slider
              value={maxDepth}
              onChange={(_, value) => setMaxDepth(value as number)}
              min={1}
              max={6}
              step={1}
              marks
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': { backgroundColor: '#0f0' },
                '& .MuiSlider-track': { backgroundColor: '#0f0' },
                '& .MuiSlider-rail': { backgroundColor: '#333' },
              }}
            />
            <Typography sx={{ color: '#888', fontSize: '11px', mt: 1 }}>
              Controls how many levels are rendered (Level of Detail)
            </Typography>
          </Box>

          {/* Slope Angle Slider */}
          <Box sx={{ mb: 3 }}>
            <Typography sx={{ color: '#0f0', fontSize: '14px', mb: 1 }}>
              Slope Angle: {slopeAngle}°
            </Typography>
            <Slider
              value={slopeAngle}
              onChange={(_, value) => setSlopeAngle(value as number)}
              min={0}
              max={60}
              step={5}
              marks
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': { backgroundColor: '#0f0' },
                '& .MuiSlider-track': { backgroundColor: '#0f0' },
                '& .MuiSlider-rail': { backgroundColor: '#333' },
              }}
            />
            <Typography sx={{ color: '#888', fontSize: '11px', mt: 1 }}>
              Controls the elevation slope (0° = flat, 60° = steep)
            </Typography>
          </Box>

          {/* Auto Rotate Toggle */}
          <FormControlLabel
            control={
              <Switch
                checked={autoRotate}
                onChange={(e) => setAutoRotate(e.target.checked)}
                sx={{
                  '& .MuiSwitch-switchBase.Mui-checked': { color: '#0f0' },
                  '& .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track': { backgroundColor: '#0f0' },
                }}
              />
            }
            label={
              <Typography sx={{ color: '#0f0', fontSize: '14px' }}>
                Auto Rotate
              </Typography>
            }
            sx={{ mb: 3 }}
          />

          {/* Selected Node Info */}
          {selectedNode && (
            <Box sx={{ mt: 3, pt: 3, borderTop: '1px solid #0f0' }}>
              <Typography sx={{ color: '#0ff', fontSize: '16px', fontWeight: 'bold', mb: 1 }}>
                Selected: {selectedNode.name}
              </Typography>
              <Typography sx={{ color: '#888', fontSize: '12px', mb: 1 }}>
                Path: {selectedPath.join(' → ')}
              </Typography>
              {selectedNode.value && (
                <Typography sx={{ color: '#0f0', fontSize: '12px' }}>
                  Value: {selectedNode.value}
                </Typography>
              )}
              {selectedNode.children && (
                <Typography sx={{ color: '#0f0', fontSize: '12px' }}>
                  Children: {selectedNode.children.length}
                </Typography>
              )}
            </Box>
          )}

          {/* Instructions */}
          <Box sx={{ mt: 3, pt: 3, borderTop: '1px solid #0f0' }}>
            <Typography sx={{ color: '#888', fontSize: '12px', mb: 1 }}>
              <strong style={{ color: '#0f0' }}>Instructions:</strong>
            </Typography>
            <Typography sx={{ color: '#888', fontSize: '11px', mb: 0.5 }}>
              • Hover over segments to highlight
            </Typography>
            <Typography sx={{ color: '#888', fontSize: '11px', mb: 0.5 }}>
              • Click segments to zoom in
            </Typography>
            <Typography sx={{ color: '#888', fontSize: '11px', mb: 0.5 }}>
              • Adjust Max Depth to control LOD
            </Typography>
            <Typography sx={{ color: '#888', fontSize: '11px' }}>
              • Adjust Slope Angle for elevation effect
            </Typography>
          </Box>
        </Paper>
      </Stack>
    </Box>
  );
};

export default Sunburst3DDemo;

