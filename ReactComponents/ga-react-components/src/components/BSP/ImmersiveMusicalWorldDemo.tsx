/**
 * Immersive Musical World Demo
 * 
 * Demo page for the Immersive Musical World component with sample musical hierarchy data.
 */

import React, { useState } from 'react';
import { Container, Box, Typography, Paper, Stack, Slider, FormControlLabel, Switch } from '@mui/material';
import { ImmersiveMusicalWorld, MusicalNode } from './ImmersiveMusicalWorld';

// Sample musical hierarchy data
const musicalWorldData: MusicalNode = {
  name: 'Musical Universe',
  children: [
    {
      name: 'Harmony',
      color: 0x4488ff,
      children: [
        {
          name: 'Chords',
          color: 0x4488ff,
          children: [
            { name: 'Major', value: 12, color: 0x44ff88 },
            { name: 'Minor', value: 12, color: 0x8844ff },
            { name: 'Diminished', value: 12, color: 0xff4488 },
            { name: 'Augmented', value: 12, color: 0xffaa44 },
          ],
        },
        {
          name: 'Voicings',
          color: 0x6688ff,
          children: [
            {
              name: 'Jazz',
              color: 0x44ff44,
              children: [
                { name: 'Drop 2', value: 8, color: 0x44ff44 },
                { name: 'Drop 3', value: 8, color: 0x66ff44 },
                { name: 'Drop 2+4', value: 8, color: 0x88ff44 },
                { name: 'Rootless', value: 8, color: 0xaaff44 },
              ],
            },
            {
              name: 'Classical',
              color: 0x4444ff,
              children: [
                { name: 'Close Position', value: 6, color: 0x4444ff },
                { name: 'Open Position', value: 6, color: 0x6644ff },
                { name: 'SATB', value: 6, color: 0x8844ff },
              ],
            },
            {
              name: 'Rock',
              color: 0xff4444,
              children: [
                { name: 'Power Chords', value: 5, color: 0xff4444 },
                { name: 'Barre Chords', value: 5, color: 0xff6644 },
                { name: 'Open Chords', value: 5, color: 0xff8844 },
              ],
            },
          ],
        },
        {
          name: 'Progressions',
          color: 0x88ff44,
          children: [
            { name: 'I-IV-V', value: 10, color: 0x88ff44 },
            { name: 'ii-V-I', value: 10, color: 0xaaff44 },
            { name: 'I-vi-IV-V', value: 10, color: 0xccff44 },
            { name: 'Circle of Fifths', value: 10, color: 0xeeff44 },
          ],
        },
      ],
    },
    {
      name: 'Melody',
      color: 0xff8844,
      children: [
        {
          name: 'Scales',
          color: 0xff8844,
          children: [
            { name: 'Major', value: 7, color: 0xff8844 },
            { name: 'Minor', value: 7, color: 0xffaa44 },
            { name: 'Pentatonic', value: 5, color: 0xffcc44 },
            { name: 'Blues', value: 6, color: 0xffee44 },
          ],
        },
        {
          name: 'Modes',
          color: 0xffaa44,
          children: [
            { name: 'Ionian', value: 7, color: 0xff4444 },
            { name: 'Dorian', value: 7, color: 0xff6644 },
            { name: 'Phrygian', value: 7, color: 0xff8844 },
            { name: 'Lydian', value: 7, color: 0xffaa44 },
            { name: 'Mixolydian', value: 7, color: 0xffcc44 },
            { name: 'Aeolian', value: 7, color: 0xffee44 },
            { name: 'Locrian', value: 7, color: 0xffff44 },
          ],
        },
        {
          name: 'Arpeggios',
          color: 0xffcc44,
          children: [
            { name: 'Major Triad', value: 3, color: 0x44ff88 },
            { name: 'Minor Triad', value: 3, color: 0x8844ff },
            { name: 'Seventh', value: 4, color: 0xff4488 },
            { name: 'Extended', value: 5, color: 0xffaa44 },
          ],
        },
      ],
    },
    {
      name: 'Rhythm',
      color: 0xff44ff,
      children: [
        {
          name: 'Time Signatures',
          color: 0xff44ff,
          children: [
            { name: '4/4', value: 16, color: 0xff44ff },
            { name: '3/4', value: 12, color: 0xff66ff },
            { name: '6/8', value: 12, color: 0xff88ff },
            { name: '5/4', value: 10, color: 0xffaaff },
            { name: '7/8', value: 14, color: 0xffccff },
          ],
        },
        {
          name: 'Patterns',
          color: 0xff66ff,
          children: [
            { name: 'Straight', value: 8, color: 0xff44ff },
            { name: 'Swing', value: 8, color: 0xff66ff },
            { name: 'Shuffle', value: 8, color: 0xff88ff },
            { name: 'Syncopated', value: 8, color: 0xffaaff },
          ],
        },
      ],
    },
    {
      name: 'Techniques',
      color: 0x44ffff,
      children: [
        {
          name: 'Guitar',
          color: 0x44ffff,
          children: [
            { name: 'Bending', value: 5, color: 0x44ffff },
            { name: 'Hammer-On', value: 5, color: 0x66ffff },
            { name: 'Pull-Off', value: 5, color: 0x88ffff },
            { name: 'Slide', value: 5, color: 0xaaffff },
            { name: 'Vibrato', value: 5, color: 0xccffff },
          ],
        },
        {
          name: 'Piano',
          color: 0x66ffff,
          children: [
            { name: 'Legato', value: 6, color: 0x44ffff },
            { name: 'Staccato', value: 6, color: 0x66ffff },
            { name: 'Pedaling', value: 6, color: 0x88ffff },
            { name: 'Trills', value: 6, color: 0xaaffff },
          ],
        },
      ],
    },
  ],
};

export const ImmersiveMusicalWorldDemo: React.FC = () => {
  const [moveSpeed, setMoveSpeed] = useState(10.0);
  const [lookSpeed, setLookSpeed] = useState(0.002);
  const [showHUD, setShowHUD] = useState(true);

  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
      <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', bgcolor: '#000' }}>
        {/* Header */}
        <Paper
          elevation={3}
          sx={{
            p: 2,
            borderRadius: 0,
            backgroundColor: '#1a1a1a',
            color: '#0f0',
            fontFamily: 'monospace',
            borderBottom: '2px solid #0f0',
          }}
        >
          <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between">
            <Box>
              <Typography variant="h4" sx={{ fontFamily: 'monospace', color: '#0f0' }}>
                🌍 IMMERSIVE MUSICAL WORLD
              </Typography>
              <Typography variant="caption" sx={{ color: '#888' }}>
                Explore the musical hierarchy in a full 3D immersive environment
              </Typography>
            </Box>
          </Stack>
        </Paper>

        {/* Main Content */}
        <Box sx={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
          {/* 3D World */}
          <Box sx={{ flex: 1, position: 'relative', backgroundColor: '#000' }}>
            <ImmersiveMusicalWorld
              data={musicalWorldData}
              width={window.innerWidth - 350}
              height={window.innerHeight - 128}
              moveSpeed={moveSpeed}
              lookSpeed={lookSpeed}
              showHUD={showHUD}
            />
          </Box>

          {/* Side Panel */}
          <Paper
            sx={{
              width: 350,
              p: 3,
              backgroundColor: '#1a1a1a',
              color: '#0f0',
              fontFamily: 'monospace',
              overflowY: 'auto',
              borderRadius: 0,
              borderLeft: '2px solid #0f0',
            }}
          >
            <Stack spacing={3}>
              {/* Info */}
              <Box>
                <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                  ℹ️ About
                </Typography>
                <Typography variant="body2" sx={{ color: '#888', mb: 2 }}>
                  A fully immersive 3D world where the musical hierarchy becomes a physical landscape.
                  Each ring of the sunburst is now a floating platform at different elevations.
                </Typography>
                <Typography variant="body2" sx={{ color: '#888' }}>
                  Walk through the world in first-person view and explore the connections between
                  musical concepts.
                </Typography>
              </Box>

              {/* Controls */}
              <Box>
                <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                  🎮 Controls
                </Typography>
                <Stack spacing={1}>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    <strong style={{ color: '#0f0' }}>WASD</strong> - Move forward/back/left/right
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    <strong style={{ color: '#0f0' }}>Mouse</strong> - Look around (click to lock)
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    <strong style={{ color: '#0f0' }}>Space</strong> - Move up
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    <strong style={{ color: '#0f0' }}>Shift</strong> - Move down
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    <strong style={{ color: '#0f0' }}>ESC</strong> - Release pointer lock
                  </Typography>
                </Stack>
              </Box>

              {/* Settings */}
              <Box>
                <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                  ⚙️ Settings
                </Typography>
                <Stack spacing={2}>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={showHUD}
                        onChange={(e) => setShowHUD(e.target.checked)}
                        sx={{
                          '& .MuiSwitch-switchBase.Mui-checked': { color: '#0f0' },
                          '& .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track': { backgroundColor: '#0f0' },
                        }}
                      />
                    }
                    label={<Typography sx={{ color: '#888' }}>Show HUD</Typography>}
                  />

                  <Box>
                    <Typography variant="body2" sx={{ color: '#888', mb: 1 }}>
                      Move Speed: {moveSpeed.toFixed(1)}
                    </Typography>
                    <Slider
                      value={moveSpeed}
                      onChange={(_, value) => setMoveSpeed(value as number)}
                      min={1}
                      max={30}
                      step={1}
                      sx={{
                        color: '#0f0',
                        '& .MuiSlider-thumb': { backgroundColor: '#0f0' },
                        '& .MuiSlider-track': { backgroundColor: '#0f0' },
                        '& .MuiSlider-rail': { backgroundColor: '#333' },
                      }}
                    />
                  </Box>

                  <Box>
                    <Typography variant="body2" sx={{ color: '#888', mb: 1 }}>
                      Look Speed: {(lookSpeed * 1000).toFixed(1)}
                    </Typography>
                    <Slider
                      value={lookSpeed * 1000}
                      onChange={(_, value) => setLookSpeed((value as number) / 1000)}
                      min={0.5}
                      max={5}
                      step={0.1}
                      sx={{
                        color: '#0f0',
                        '& .MuiSlider-thumb': { backgroundColor: '#0f0' },
                        '& .MuiSlider-track': { backgroundColor: '#0f0' },
                        '& .MuiSlider-rail': { backgroundColor: '#333' },
                      }}
                    />
                  </Box>
                </Stack>
              </Box>

              {/* Features */}
              <Box>
                <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                  ✨ Features
                </Typography>
                <Stack spacing={1}>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ First-person camera
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ Floating platforms (hierarchy levels)
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ Gradient skybox
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ Dynamic lighting
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ Particle atmosphere
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ Shadows and reflections
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ Edge glow effects
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    ✅ Real-time FPS counter
                  </Typography>
                </Stack>
              </Box>

              {/* Tips */}
              <Box>
                <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                  💡 Tips
                </Typography>
                <Stack spacing={1}>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    • Click the canvas to lock your mouse pointer
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    • Use Space/Shift to fly up and down
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    • Inner platforms are higher (deeper in hierarchy)
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    • Each color represents a different musical category
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#888' }}>
                    • Platforms glow with their category color
                  </Typography>
                </Stack>
              </Box>
            </Stack>
          </Paper>
        </Box>
      </Box>
    </Container>
  );
};

export default ImmersiveMusicalWorldDemo;

