/**
 * Modal Meadow Test Page (v0.6).
 *
 * Mounts the ModalMeadow component full-screen with two HUDs:
 *   - bottom-left: current mode name + descriptor
 *   - centre-top: pointer-lock hint, swaps based on auto-walk vs takeover
 *
 * v0.6: the canvas now auto-walks by default; the centre-top hint reads
 * "Auto-walking · Click to take control" until the user clicks (pointer
 * lock) or presses a movement key, then it switches to the regular
 * "Click to enter · WASD to walk · ESC to release" prompt.
 *
 * The mode state lives here so the HUD can be plain React; the canvas
 * notifies us via the onModeChange callback.
 */

import React, { useState, useCallback } from 'react';
import { Container, Box, Typography, Paper } from '@mui/material';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';
import {
  ModalMeadow,
  IONIAN,
  type ModeConfig,
} from '../components/ModalMeadow';

const ModalMeadowTest: React.FC = () => {
  const [mode, setMode] = useState<ModeConfig>(IONIAN);
  const [locked, setLocked] = useState<boolean>(false);
  // v0.6: track whether the user has taken control. Auto-walk is on until
  // this flips true (pointer-lock click or movement-key press).
  const [tookOver, setTookOver] = useState<boolean>(false);

  // Callbacks are useCallback'd because ModalMeadow's effect depends on them;
  // a fresh function each render would re-tear-down the whole scene.
  const handleModeChange = useCallback((m: ModeConfig) => setMode(m), []);
  const handleLockChange = useCallback((l: boolean) => setLocked(l), []);
  const handleUserTakeover = useCallback(() => setTookOver(true), []);

  return (
    <DemoErrorBoundary demoName="Modal Meadow">
      <Container
        maxWidth={false}
        disableGutters
        sx={{ height: '100vh', overflow: 'hidden', position: 'relative' }}
      >
        <ModalMeadow
          onModeChange={handleModeChange}
          onLockChange={handleLockChange}
          onUserTakeover={handleUserTakeover}
        />

        {/* Centre-top pointer-lock hint — only when not locked. v0.6 swaps
            the text based on whether the camera is auto-walking. */}
        {!locked && (
          <Paper
            elevation={3}
            sx={{
              position: 'absolute',
              top: 24,
              left: '50%',
              transform: 'translateX(-50%)',
              px: 3,
              py: 1.5,
              backgroundColor: 'rgba(0,0,0,0.65)',
              color: '#cdeac0',
              fontFamily: 'monospace',
              fontSize: 13,
              pointerEvents: 'none',
              letterSpacing: 1,
            }}
          >
            {tookOver
              ? 'Click to enter · WASD to walk · ESC to release'
              : 'Auto-walking · Click to take control'}
          </Paper>
        )}

        {/* Bottom-left mode panel. */}
        <Paper
          elevation={3}
          sx={{
            position: 'absolute',
            bottom: 24,
            left: 24,
            px: 2.5,
            py: 1.5,
            minWidth: 280,
            backgroundColor: 'rgba(8, 14, 8, 0.78)',
            color: '#cdeac0',
            fontFamily: 'monospace',
            pointerEvents: 'none',
            borderLeft: '3px solid #9be38a',
          }}
        >
          <Typography
            variant="caption"
            sx={{ color: '#7da876', display: 'block', letterSpacing: 1 }}
          >
            CURRENT MODE
          </Typography>
          <Typography
            variant="h6"
            sx={{ color: '#9be38a', fontFamily: 'monospace', mb: 0.5 }}
          >
            {mode.name}
          </Typography>
          <Typography variant="body2" sx={{ color: '#cdeac0', fontSize: 12 }}>
            {mode.descriptor}
          </Typography>
        </Paper>

        {/* Bottom-right title chip — small attribution for the demo. */}
        <Box
          sx={{
            position: 'absolute',
            bottom: 24,
            right: 24,
            px: 2,
            py: 1,
            backgroundColor: 'rgba(0,0,0,0.55)',
            color: '#9be38a',
            fontFamily: 'monospace',
            fontSize: 11,
            pointerEvents: 'none',
            letterSpacing: 1,
          }}
        >
          MODAL MEADOW · v1.0 · Ionian ↔ Phrygian · Cinematic (Bloom + Shadows + God Rays)
        </Box>
      </Container>
    </DemoErrorBoundary>
  );
};

export default ModalMeadowTest;
