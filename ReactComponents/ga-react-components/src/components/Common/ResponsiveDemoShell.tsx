/**
 * ResponsiveDemoShell — shared chrome for the cinematic 3D demos.
 *
 * Wraps a canvas-style viewport in:
 *
 *  - A side-by-side viewport + 320px controls panel on desktop (≥900px,
 *    fine pointer).
 *  - A full-bleed viewport + cog FAB → MUI Drawer on mobile / tablet
 *    (coarse pointer or narrow viewport).
 *
 * Always overlays a Chromecast pill in the top-right.
 *
 * Demos pass the rendered canvas component as `viewport` and the controls
 * JSX (sliders, switches, headings) as `controls`. The same controls JSX
 * is used in both the desktop sidebar and the mobile drawer so they can't
 * drift.
 */

import React, { useState } from 'react';
import {
  Box,
  Paper,
  Stack,
  Drawer,
  IconButton,
  useMediaQuery,
} from '@mui/material';
import SettingsIcon from '@mui/icons-material/Settings';
import CastButton from './CastButton';

export interface ResponsiveDemoShellProps {
  /** The 3D canvas / scene component. Should fill its parent (auto-size). */
  viewport: React.ReactNode;
  /** Controls JSX. Should include its own header + close button on mobile. */
  controls: React.ReactNode;
  /** Background color behind the canvas (visible during canvas init). */
  backgroundColor?: string;
  /** Border tint for the controls panel. */
  panelBorderColor?: string;
  /** Background for the panel (semi-transparent so canvas shows through faintly). */
  panelBackgroundColor?: string;
  /** Cog button accent color. */
  cogColor?: string;
}

/** True when the viewport prefers a coarse pointer (touch) or is narrow. */
export const useIsMobile = (): boolean => {
  const coarsePointer = useMediaQuery('(pointer: coarse)');
  const narrowVp      = useMediaQuery('(max-width: 900px)');
  return coarsePointer || narrowVp;
};

const ResponsiveDemoShell: React.FC<ResponsiveDemoShellProps> = ({
  viewport,
  controls,
  backgroundColor = '#000',
  panelBorderColor = '#444',
  panelBackgroundColor = 'rgba(8, 12, 16, 0.94)',
  cogColor = '#ffffff',
}) => {
  const isMobile = useIsMobile();
  const [drawerOpen, setDrawerOpen] = useState<boolean>(false);

  if (isMobile) {
    return (
      <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor, overflow: 'hidden', position: 'relative' }}>
        <Box sx={{ width: '100%', height: '100%', position: 'absolute', inset: 0 }}>
          {viewport}
        </Box>
        <CastButton />

        <IconButton
          aria-label="Open settings"
          onClick={() => setDrawerOpen(true)}
          sx={{
            position: 'absolute',
            bottom: 24,
            right: 16,
            zIndex: 10,
            backgroundColor: 'rgba(0, 0, 0, 0.65)',
            backdropFilter: 'blur(6px)',
            color: cogColor,
            border: `1px solid ${cogColor}`,
            '&:hover': { backgroundColor: 'rgba(0, 0, 0, 0.85)' },
          }}
        >
          <SettingsIcon />
        </IconButton>

        <Drawer
          anchor="right"
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          PaperProps={{
            sx: {
              width: '85vw',
              maxWidth: 360,
              padding: 2,
              backgroundColor: panelBackgroundColor,
              borderLeft: `1px solid ${panelBorderColor}`,
            },
          }}
        >
          {/* Drawer-specific consumer responsibility: the controls JSX
              should render its own close button (we forward state via
              context-free pattern: pass `setDrawerOpen` if you need it).
              By default the user can swipe or tap-outside the drawer. */}
          {controls}
        </Drawer>
      </Box>
    );
  }

  return (
    <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor, overflow: 'hidden' }}>
      <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
        <Box sx={{ flex: 1, display: 'flex', position: 'relative', minWidth: 0 }}>
          {viewport}
          <CastButton />
        </Box>
        <Paper
          sx={{
            width: 320,
            padding: 3,
            backgroundColor: panelBackgroundColor,
            border: `1px solid ${panelBorderColor}`,
            overflowY: 'auto',
          }}
        >
          {controls}
        </Paper>
      </Stack>
    </Box>
  );
};

export default ResponsiveDemoShell;
