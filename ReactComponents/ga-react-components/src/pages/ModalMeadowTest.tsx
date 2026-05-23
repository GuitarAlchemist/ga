/**
 * Modal Meadow Test Page (v1.4).
 *
 * Mounts the ModalMeadow component full-screen with two HUDs:
 *   - bottom-left: current mode name + descriptor + position along the
 *     brightness curve (7 pips, Lydian→Locrian, current lit)
 *   - centre-top: pointer-lock hint. Swaps based on whether auto-walk is
 *     still active (v0.6 takeover-pattern, preserved) — until the user
 *     either click-locks the canvas or invokes the WASD-takeover handler,
 *     the hint reads "Auto-walking · Click to take control"; afterward it
 *     becomes "Click to enter · WASD to walk · ESC to release".
 *
 * v1.4 (mobile fallback): if `useIsMobile()` reports coarse pointer or a
 * narrow viewport, render <MobileFallback> instead of mounting the heavy
 * Three.js scene. Saves the 240k-blade GPU cost on devices where the
 * controls (WASD + pointer-lock) wouldn't work anyway.
 *
 * The mode state lives here so the HUD can be plain React; the canvas
 * notifies us via the onModeChange / onUserTakeover callbacks.
 */

import React, { useState, useCallback } from 'react';
import { Container, Box, Typography, Paper } from '@mui/material';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';
import { useIsMobile } from '../components/Common/ResponsiveDemoShell';
import {
  ModalMeadow,
  MobileFallback,
  LYDIAN,
  MODES,
  type ModeConfig,
} from '../components/ModalMeadow';

const ModalMeadowTest: React.FC = () => {
  const isMobile = useIsMobile();
  const [mode, setMode] = useState<ModeConfig>(LYDIAN);
  const [locked, setLocked] = useState<boolean>(false);
  // v0.6 pattern (kept): auto-walk is on until this flips true via
  // pointer-lock click or a movement-key press inside the canvas.
  const [tookOver, setTookOver] = useState<boolean>(false);

  // Callbacks are useCallback'd because ModalMeadow's effect depends on them;
  // a fresh function each render would re-tear-down the whole scene.
  // Hooks must always run unconditionally — even on mobile, where we'll
  // skip rendering ModalMeadow — so they live above the early return.
  const handleModeChange = useCallback((m: ModeConfig) => setMode(m), []);
  const handleLockChange = useCallback((l: boolean) => setLocked(l), []);
  const handleUserTakeover = useCallback(() => setTookOver(true), []);

  // v1.4: short-circuit before instantiating the Three.js scene on mobile.
  // Saves the 240k-blade GPU spin-up and avoids the pointer-lock /
  // keyboard / mouse controls that don't exist on touch devices.
  if (isMobile) {
    return (
      <DemoErrorBoundary demoName="Modal Meadow (mobile)">
        <MobileFallback />
      </DemoErrorBoundary>
    );
  }

  // Index of the current mode (0..6, Lydian..Locrian). Used to draw the
  // "you are here" pip in the brightness curve below the HUD.
  const modeIndex = MODES.findIndex((m) => m.name === mode.name);

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

        {/* Centre-top pointer-lock hint — only when not locked. v0.6 swap
            preserved: text differs depending on takeover state. */}
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
              : 'Auto-walking the brightness curve · Click to take control'}
          </Paper>
        )}

        {/* Bottom-left mode panel + brightness-curve pip strip. */}
        <Paper
          elevation={3}
          sx={{
            position: 'absolute',
            bottom: 24,
            left: 24,
            px: 2.5,
            py: 1.5,
            minWidth: 320,
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

          {/* Brightness-curve pip strip: 7 dots Lydian→Locrian, current lit. */}
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mt: 1.2 }}>
            <Typography sx={{ fontSize: 9, color: '#7da876', letterSpacing: 1, mr: 0.5 }}>
              BRIGHT
            </Typography>
            {MODES.map((m, i) => (
              <Box
                key={m.name}
                sx={{
                  width: i === modeIndex ? 12 : 8,
                  height: i === modeIndex ? 12 : 8,
                  borderRadius: '50%',
                  backgroundColor: i === modeIndex ? '#9be38a' : 'rgba(155,227,138,0.35)',
                  border: i === modeIndex ? '2px solid #cdeac0' : 'none',
                  transition: 'all 0.25s',
                }}
              />
            ))}
            <Typography sx={{ fontSize: 9, color: '#7da876', letterSpacing: 1, ml: 0.5 }}>
              DARK
            </Typography>
          </Box>
        </Paper>

        {/* Bottom-right title chip. */}
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
          MODAL MEADOW · v1.4 · 7 modes · ADSR · Reverb · Hills · Ponds · Descent · Ground Clamp · Sky · Sun · PCF Shadows · God Rays · Mobile Fallback
        </Box>
      </Container>
    </DemoErrorBoundary>
  );
};

export default ModalMeadowTest;
