/**
 * BSP DOOM Explorer test page.
 *
 * Walks a guitarist through a curated 8-key world. Each room = a real
 * key+mode; each partition = a real harmonic boundary; the HUD's
 * "modulate to →" chips teleport between neighbouring keys.
 *
 * Earlier this page wore a retro DOOM-green theme with a 400px panel
 * full of duplicated controls. That's been replaced with the GA dark
 * palette (`#0d1117` / `#161b22` / `#30363d` / `#e6edf3`) and a single
 * collapsible settings drawer; on mobile we serve a clean "best on
 * desktop" landing card with a 2D map preview instead of forcing FPS
 * controls onto a touch screen.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Stack,
  Typography,
  Drawer,
  IconButton,
  Slider,
  Switch,
  FormControlLabel,
  Divider,
  Chip,
  useMediaQuery,
  Tooltip,
} from '@mui/material';
import SettingsIcon from '@mui/icons-material/Settings';
import CloseIcon from '@mui/icons-material/Close';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import DesktopMacIcon from '@mui/icons-material/DesktopMac';
import { BSPDoomExplorer } from '../components/BSP';
import type { BSPRegion } from '../components/BSP/BSPApiService';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';
import { getAllKeys } from '../components/BSP/v2/musicalTree';

const GA_BG = '#0d1117';
const GA_PANEL = '#161b22';
const GA_BORDER = '#30363d';
const GA_TEXT = '#e6edf3';
const GA_MUTED = '#8b949e';
const GA_ACCENT = '#79c0ff';

function MobileMapPreview() {
  const keys = useMemo(() => getAllKeys(), []);
  return (
    <Box
      sx={{
        width: '100%',
        height: '100%',
        background: GA_BG,
        color: GA_TEXT,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'flex-start',
        p: 3,
        gap: 2,
        overflowY: 'auto',
      }}
    >
      <DesktopMacIcon sx={{ fontSize: 48, color: GA_ACCENT }} />
      <Typography variant="h6" sx={{ color: GA_TEXT, fontFamily: 'ui-monospace, monospace' }}>
        Best on desktop
      </Typography>
      <Typography variant="body2" sx={{ color: GA_MUTED, textAlign: 'center', maxWidth: 360 }}>
        The BSP DOOM Explorer is a first-person walk through eight musical
        keys — it needs a mouse for look and WASD for movement. Here's a flat
        preview of the eight rooms; open this page on a desktop for the full
        experience.
      </Typography>
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr)',
          gap: 1,
          mt: 2,
          width: '100%',
          maxWidth: 360,
        }}
      >
        {keys.map((k) => (
          <Box
            key={k.key}
            sx={{
              background: GA_PANEL,
              border: `1px solid ${GA_BORDER}`,
              borderRadius: 1,
              p: 1,
              textAlign: 'center',
              fontFamily: 'ui-monospace, monospace',
            }}
          >
            <Typography sx={{ color: GA_ACCENT, fontSize: 11, fontWeight: 700 }}>
              {k.shortName}
            </Typography>
            <Typography sx={{ color: GA_MUTED, fontSize: 9 }}>
              {k.mode}
            </Typography>
          </Box>
        ))}
      </Box>
    </Box>
  );
}

const BSPDoomExplorerTest: React.FC = () => {
  const [showHUD, setShowHUD] = useState(true);
  const [showMinimap, setShowMinimap] = useState(true);
  const [moveSpeed, setMoveSpeed] = useState(5.0);
  const [lookSpeed, setLookSpeed] = useState(0.002);
  const [currentRegion, setCurrentRegion] = useState<BSPRegion | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [viewport, setViewport] = useState({ w: 1200, h: 800 });

  const isMobile = useMediaQuery('(max-width: 767px)');

  useEffect(() => {
    function update() {
      // Layout: full-bleed canvas, header takes ~48px.
      setViewport({ w: window.innerWidth, h: Math.max(400, window.innerHeight - 96) });
    }
    update();
    window.addEventListener('resize', update);
    return () => window.removeEventListener('resize', update);
  }, []);

  const handleRegionChange = useCallback((region: BSPRegion) => {
    setCurrentRegion(region);
  }, []);

  const handleReset = useCallback(() => {
    setShowHUD(true);
    setShowMinimap(true);
    setMoveSpeed(5.0);
    setLookSpeed(0.002);
  }, []);

  return (
    <DemoErrorBoundary demoName="BSP DOOM Explorer">
      <Box
        sx={{
          height: 'calc(100vh - 48px)',
          display: 'flex',
          flexDirection: 'column',
          background: GA_BG,
          color: GA_TEXT,
          overflow: 'hidden',
        }}
      >
        {/* Toolbar — matches the EcosystemRoadmap toolbar pattern. */}
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 2,
            px: 2,
            py: 1,
            background: GA_PANEL,
            borderBottom: `1px solid ${GA_BORDER}`,
            minHeight: 48,
          }}
        >
          <Typography
            variant="subtitle1"
            sx={{ color: GA_TEXT, fontFamily: 'ui-monospace, monospace', fontWeight: 600 }}
          >
            BSP DOOM Explorer
          </Typography>
          <Chip
            label="Binary Space Partitioning of tonal regions"
            size="small"
            sx={{
              background: 'transparent',
              border: `1px solid ${GA_BORDER}`,
              color: GA_MUTED,
              fontFamily: 'ui-monospace, monospace',
              fontSize: 11,
            }}
          />
          {currentRegion && (
            <Chip
              label={currentRegion.name}
              size="small"
              sx={{
                background: GA_PANEL,
                border: `1px solid ${GA_ACCENT}`,
                color: GA_ACCENT,
                fontFamily: 'ui-monospace, monospace',
                fontSize: 11,
              }}
            />
          )}
          <Box sx={{ flexGrow: 1 }} />
          <Tooltip title="Reset settings">
            <IconButton
              onClick={handleReset}
              size="small"
              sx={{ color: GA_MUTED, '&:hover': { color: GA_TEXT, background: '#21262d' } }}
            >
              <RestartAltIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Settings">
            <IconButton
              onClick={() => setSettingsOpen(true)}
              size="small"
              sx={{ color: GA_MUTED, '&:hover': { color: GA_TEXT, background: '#21262d' } }}
            >
              <SettingsIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>

        {/* Canvas / mobile fallback. */}
        <Box sx={{ flex: 1, position: 'relative', background: GA_BG }}>
          {isMobile ? (
            <MobileMapPreview />
          ) : (
            <BSPDoomExplorer
              width={viewport.w}
              height={viewport.h}
              moveSpeed={moveSpeed}
              lookSpeed={lookSpeed}
              showHUD={showHUD}
              showMinimap={showMinimap}
              onRegionChange={handleRegionChange}
            />
          )}
        </Box>

        {/* Settings drawer. */}
        <Drawer
          anchor="right"
          open={settingsOpen}
          onClose={() => setSettingsOpen(false)}
          PaperProps={{
            sx: {
              width: 320,
              background: GA_PANEL,
              color: GA_TEXT,
              borderLeft: `1px solid ${GA_BORDER}`,
              fontFamily: 'ui-monospace, monospace',
            },
          }}
        >
          <Box sx={{ display: 'flex', alignItems: 'center', p: 2, borderBottom: `1px solid ${GA_BORDER}` }}>
            <Typography variant="subtitle1" sx={{ color: GA_TEXT, fontWeight: 600, flexGrow: 1 }}>
              Settings
            </Typography>
            <IconButton size="small" onClick={() => setSettingsOpen(false)} sx={{ color: GA_MUTED }}>
              <CloseIcon fontSize="small" />
            </IconButton>
          </Box>
          <Stack spacing={2.5} sx={{ p: 2 }}>
            <FormControlLabel
              control={
                <Switch
                  checked={showHUD}
                  onChange={(e) => setShowHUD(e.target.checked)}
                  size="small"
                />
              }
              label={<Typography sx={{ color: GA_TEXT, fontSize: 13 }}>HUD</Typography>}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={showMinimap}
                  onChange={(e) => setShowMinimap(e.target.checked)}
                  size="small"
                />
              }
              label={<Typography sx={{ color: GA_TEXT, fontSize: 13 }}>Minimap</Typography>}
            />

            <Box>
              <Typography variant="caption" sx={{ color: GA_MUTED }}>
                Move speed: {moveSpeed.toFixed(1)}
              </Typography>
              <Slider
                value={moveSpeed}
                onChange={(_, v) => setMoveSpeed(v as number)}
                min={1}
                max={20}
                step={0.5}
                size="small"
                sx={{ color: GA_ACCENT }}
              />
            </Box>

            <Box>
              <Typography variant="caption" sx={{ color: GA_MUTED }}>
                Look sensitivity: {(lookSpeed * 1000).toFixed(1)}
              </Typography>
              <Slider
                value={lookSpeed * 1000}
                onChange={(_, v) => setLookSpeed((v as number) / 1000)}
                min={0.5}
                max={5}
                step={0.1}
                size="small"
                sx={{ color: GA_ACCENT }}
              />
            </Box>

            <Divider sx={{ borderColor: GA_BORDER }} />

            <Box>
              <Typography variant="caption" sx={{ color: GA_MUTED, display: 'block', mb: 1 }}>
                Controls
              </Typography>
              <Typography sx={{ color: GA_TEXT, fontSize: 11.5, lineHeight: 1.7 }}>
                <b>click canvas</b> · lock pointer<br />
                <b>WASD</b> / arrows · move<br />
                <b>space</b> / <b>shift</b> · up / down<br />
                <b>esc</b> · release pointer<br />
                click a <b>modulate to</b> chip · teleport
              </Typography>
            </Box>

            <Box>
              <Typography variant="caption" sx={{ color: GA_MUTED, display: 'block', mb: 1 }}>
                What you'll see
              </Typography>
              <Typography sx={{ color: GA_MUTED, fontSize: 11.5, lineHeight: 1.6 }}>
                Eight rooms — each one a key+mode. Floor colour encodes
                circle-of-fifths position. Walls are BSP partition planes
                (harmonic boundaries). The HUD names the key you're in,
                its scale, its diatonic chords with Roman numerals, and
                the keys you can modulate to from here. Try the
                <b style={{ color: GA_ACCENT }}> ii–V–I tour</b> at the
                bottom of the canvas.
              </Typography>
            </Box>
          </Stack>
        </Drawer>
      </Box>
    </DemoErrorBoundary>
  );
};

export default BSPDoomExplorerTest;
