/**
 * BSP DOOM Explorer Test Page
 * 
 * Test page for the DOOM-like BSP tree explorer component.
 * Navigate through the Binary Space Partitioning tree structure
 * in first-person view like exploring a DOOM level.
 */

import React, { useState } from 'react';
import {
  Container,
  Box,
  Typography,
  Paper,
  Stack,
  Button,
  Divider,
  Alert,
  Chip,
  Switch,
  FormControlLabel,
  Slider,
} from '@mui/material';
import { BSPDoomExplorer } from '../components/BSP';
import type { BSPRegion } from '../components/BSP/BSPApiService';

const BSPDoomExplorerTest: React.FC = () => {
  const [showHUD, setShowHUD] = useState(true);
  const [showMinimap, setShowMinimap] = useState(true);
  const [moveSpeed, setMoveSpeed] = useState(5.0);
  const [lookSpeed, setLookSpeed] = useState(0.002);
  const [currentRegion, setCurrentRegion] = useState<BSPRegion | null>(null);
  const [regionHistory, setRegionHistory] = useState<string[]>([]);

  const handleRegionChange = (region: BSPRegion) => {
    setCurrentRegion(region);
    setRegionHistory(prev => [...prev, region.name].slice(-10)); // Keep last 10
  };

  const handleReset = () => {
    setShowHUD(true);
    setShowMinimap(true);
    setMoveSpeed(5.0);
    setLookSpeed(0.002);
    setRegionHistory([]);
  };

  return (
    <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 48px)', overflow: 'hidden' }}>
      <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
        {/* Header */}
        <Paper
          elevation={3}
          sx={{
            p: 2,
            borderRadius: 0,
            backgroundColor: '#1a1a1a',
            color: '#0f0',
            fontFamily: 'monospace',
          }}
        >
          <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between">
            <Box>
              <Typography variant="h4" sx={{ fontFamily: 'monospace', color: '#0f0' }}>
                🎮 BSP DOOM EXPLORER
              </Typography>
              <Typography variant="caption" sx={{ color: '#888' }}>
                First-person navigation through Binary Space Partitioning tree structure
              </Typography>
            </Box>
            <Button
              variant="outlined"
              onClick={handleReset}
              sx={{
                color: '#0f0',
                borderColor: '#0f0',
                '&:hover': {
                  borderColor: '#0f0',
                  backgroundColor: 'rgba(0, 255, 0, 0.1)',
                },
              }}
            >
              Reset Settings
            </Button>
          </Stack>
        </Paper>

        {/* Main Content */}
        <Box sx={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
          {/* Explorer */}
          <Box sx={{ flex: 1, position: 'relative', backgroundColor: '#000' }}>
            <BSPDoomExplorer
              width={window.innerWidth - 400}
              height={window.innerHeight - 148}
              moveSpeed={moveSpeed}
              lookSpeed={lookSpeed}
              showHUD={showHUD}
              showMinimap={showMinimap}
              onRegionChange={handleRegionChange}
            />
          </Box>

          {/* Side Panel */}
          <Paper
            sx={{
              width: 400,
              p: 3,
              backgroundColor: '#1a1a1a',
              color: '#0f0',
              fontFamily: 'monospace',
              overflowY: 'auto',
              borderRadius: 0,
            }}
          >
            <Stack spacing={3}>
              {/* Info */}
              <Box>
                <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                  ℹ️ About
                </Typography>
                <Alert
                  severity="info"
                  sx={{
                    backgroundColor: 'rgba(0, 255, 0, 0.1)',
                    color: '#0f0',
                    '& .MuiAlert-icon': { color: '#0f0' },
                  }}
                >
                  Navigate through the BSP tree structure like exploring a DOOM level.
                  Each partition plane is a wall, and tonal regions are colored rooms.
                </Alert>
              </Box>

              <Divider sx={{ borderColor: '#0f0' }} />

              {/* Controls Info */}
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

              <Divider sx={{ borderColor: '#0f0' }} />

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
                          '& .MuiSwitch-switchBase.Mui-checked': {
                            color: '#0f0',
                          },
                          '& .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track': {
                            backgroundColor: '#0f0',
                          },
                        }}
                      />
                    }
                    label={<Typography sx={{ color: '#888' }}>Show HUD</Typography>}
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        checked={showMinimap}
                        onChange={(e) => setShowMinimap(e.target.checked)}
                        sx={{
                          '& .MuiSwitch-switchBase.Mui-checked': {
                            color: '#0f0',
                          },
                          '& .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track': {
                            backgroundColor: '#0f0',
                          },
                        }}
                      />
                    }
                    label={<Typography sx={{ color: '#888' }}>Show Minimap</Typography>}
                  />

                  <Box>
                    <Typography variant="body2" sx={{ color: '#888', mb: 1 }}>
                      Move Speed: {moveSpeed.toFixed(1)}
                    </Typography>
                    <Slider
                      value={moveSpeed}
                      onChange={(_, value) => setMoveSpeed(value as number)}
                      min={1}
                      max={20}
                      step={0.5}
                      sx={{
                        color: '#0f0',
                        '& .MuiSlider-thumb': {
                          backgroundColor: '#0f0',
                        },
                        '& .MuiSlider-track': {
                          backgroundColor: '#0f0',
                        },
                        '& .MuiSlider-rail': {
                          backgroundColor: '#333',
                        },
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
                        '& .MuiSlider-thumb': {
                          backgroundColor: '#0f0',
                        },
                        '& .MuiSlider-track': {
                          backgroundColor: '#0f0',
                        },
                        '& .MuiSlider-rail': {
                          backgroundColor: '#333',
                        },
                      }}
                    />
                  </Box>
                </Stack>
              </Box>

              <Divider sx={{ borderColor: '#0f0' }} />

              {/* Current Region */}
              {currentRegion && (
                <Box>
                  <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                    📍 Current Region
                  </Typography>
                  <Paper
                    sx={{
                      p: 2,
                      backgroundColor: 'rgba(0, 255, 0, 0.1)',
                      border: '1px solid #0f0',
                    }}
                  >
                    <Typography variant="h6" sx={{ color: '#0f0' }}>
                      {currentRegion.name}
                    </Typography>
                    <Typography variant="body2" sx={{ color: '#888', mt: 1 }}>
                      Type: {currentRegion.tonalityType}
                    </Typography>
                    <Typography variant="body2" sx={{ color: '#888' }}>
                      Center: {currentRegion.tonalCenter}
                    </Typography>
                  </Paper>
                </Box>
              )}

              {/* Region History */}
              {regionHistory.length > 0 && (
                <Box>
                  <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                    📜 Region History
                  </Typography>
                  <Stack spacing={0.5}>
                    {regionHistory.slice().reverse().map((region, index) => (
                      <Chip
                        key={index}
                        label={region}
                        size="small"
                        sx={{
                          backgroundColor: 'rgba(0, 255, 0, 0.2)',
                          color: '#0f0',
                          borderColor: '#0f0',
                          fontFamily: 'monospace',
                        }}
                        variant="outlined"
                      />
                    ))}
                  </Stack>
                </Box>
              )}

              <Divider sx={{ borderColor: '#0f0' }} />

              {/* Features */}
              <Box>
                <Typography variant="h6" sx={{ color: '#0f0', mb: 2 }}>
                  ✨ Features
                </Typography>
                <Stack spacing={1}>
                  <Chip label="First-Person Camera" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                  <Chip label="WASD Movement" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                  <Chip label="Mouse Look" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                  <Chip label="BSP Partition Walls" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                  <Chip label="Tonal Region Rooms" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                  <Chip label="WebGPU Rendering" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                  <Chip label="Real-time HUD" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                  <Chip label="Minimap" size="small" sx={{ backgroundColor: '#0f0', color: '#000' }} />
                </Stack>
              </Box>
            </Stack>
          </Paper>
        </Box>
      </Box>
    </Container>
  );
};

export default BSPDoomExplorerTest;

