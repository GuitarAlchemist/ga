/**
 * FluffyGrassDemo — phone-friendly responsive shell.
 *
 * Desktop (≥900px wide): viewport on the left, fixed 320px controls panel
 * on the right (the previous layout).
 *
 * Mobile / narrow (<900px or no hover): full-bleed canvas + a settings FAB
 * that opens a Drawer with the same controls. Defaults are dialled down
 * so the simulation actually runs at 60fps on a phone GPU — fewer chunks,
 * lower density, and the FluffyGrass component caps its pixel ratio at
 * 1.5 instead of 2.
 */

import React, { useState } from 'react';
import {
  Box,
  Typography,
  Paper,
  Stack,
  Slider,
  Switch,
  FormControlLabel,
  Divider,
  Drawer,
  IconButton,
  useMediaQuery,
} from '@mui/material';
import SettingsIcon from '@mui/icons-material/Settings';
import CloseIcon from '@mui/icons-material/Close';
import { FluffyGrass } from './FluffyGrass';
import CastButton from '../Common/CastButton';

const labelForTod = (t: number): string => {
  if (t < 0.05 || t > 0.95) return 'Sunrise';
  if (t < 0.20) return 'Morning';
  if (t < 0.30) return 'Noon';
  if (t < 0.45) return 'Afternoon';
  if (t < 0.55) return 'Sunset';
  if (t < 0.70) return 'Dusk';
  if (t < 0.85) return 'Night';
  return 'Pre-dawn';
};

export const FluffyGrassDemo: React.FC = () => {
  // Coarse-pointer / narrow viewport → mobile layout. The first matches
  // phones in any orientation; the second covers tablets in portrait too.
  const coarsePointer = useMediaQuery('(pointer: coarse)');
  const narrowVp      = useMediaQuery('(max-width: 900px)');
  const isMobile      = coarsePointer || narrowVp;

  // Mobile-aware defaults — the FluffyGrass scene is GPU-heavy at the
  // desktop defaults; phones need lower density / fewer chunks to stay at
  // 60fps. The user can still crank these up via the controls panel if
  // they want.
  const [grassDensity, setGrassDensity] = useState<number>(isMobile ? 320 : 800);
  const [chunkSize, setChunkSize] = useState<number>(10);
  const [chunkCount, setChunkCount] = useState<number>(isMobile ? 6 : 8);
  const [grassHeight, setGrassHeight] = useState<number>(1.0);
  const [grassWidth, setGrassWidth] = useState<number>(0.1);
  const [windSpeed, setWindSpeed] = useState<number>(1.0);
  const [windStrength, setWindStrength] = useState<number>(0.35);

  const [autoCycle, setAutoCycle] = useState<boolean>(true);
  const [dayLengthSeconds, setDayLengthSeconds] = useState<number>(90);
  const [fixedTimeOfDay, setFixedTimeOfDay] = useState<number>(0.18);
  const [autoRotate, setAutoRotate] = useState<boolean>(true);
  const [fireflies, setFireflies] = useState<boolean>(true);
  const [flowers, setFlowers] = useState<boolean>(true);

  const [drawerOpen, setDrawerOpen] = useState<boolean>(false);

  // Re-mount on big topology changes; live-update wind/colors via uniforms.
  const sceneKey = `${grassDensity}-${chunkSize}-${chunkCount}-${grassHeight}-${grassWidth}-${autoCycle ? 'cycle' : 'fixed'}-${dayLengthSeconds}-${fixedTimeOfDay.toFixed(2)}-${autoRotate}-${fireflies}-${flowers}`;

  const sliderSx = {
    color: '#9be38a',
    '& .MuiSlider-thumb': { backgroundColor: '#9be38a' },
    '& .MuiSlider-track': { backgroundColor: '#9be38a' },
    '& .MuiSlider-rail': { backgroundColor: '#9be38a', opacity: 0.3 },
  };
  const labelSx = { color: '#cdeac0', fontFamily: 'monospace', mb: 1 };
  const headSx = { color: '#9be38a', fontFamily: 'monospace', mb: 1, mt: 2 };

  // Controls panel content — used by both desktop sidebar and mobile drawer
  // so they stay in lockstep.
  const controlsContent = (
    <>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
        <Typography variant="h5" sx={{ color: '#9be38a', fontFamily: 'monospace' }}>
          🌾 FLUFFY GRASS
        </Typography>
        {isMobile && (
          <IconButton aria-label="Close settings" onClick={() => setDrawerOpen(false)} sx={{ color: '#9be38a' }}>
            <CloseIcon />
          </IconButton>
        )}
      </Stack>
      <Typography variant="caption" sx={{ color: '#7da876', fontFamily: 'monospace', display: 'block', mb: 2 }}>
        cinematic v2 — bezier blades · day/night · gusts · fireflies
      </Typography>

      <Typography variant="subtitle2" sx={headSx}>ATMOSPHERE</Typography>
      <FormControlLabel
        control={<Switch checked={autoCycle} onChange={(_, v) => setAutoCycle(v)} />}
        label={<span style={{ color: '#cdeac0', fontFamily: 'monospace', fontSize: 13 }}>Day/Night Cycle</span>}
      />
      {autoCycle ? (
        <Box sx={{ mb: 2, mt: 1 }}>
          <Typography variant="body2" sx={labelSx}>Day Length: {dayLengthSeconds}s</Typography>
          <Slider value={dayLengthSeconds} onChange={(_, v) => setDayLengthSeconds(v as number)} min={20} max={240} step={10} sx={sliderSx} />
        </Box>
      ) : (
        <Box sx={{ mb: 2, mt: 1 }}>
          <Typography variant="body2" sx={labelSx}>Time of Day: {labelForTod(fixedTimeOfDay)}</Typography>
          <Slider value={fixedTimeOfDay} onChange={(_, v) => setFixedTimeOfDay(v as number)} min={0} max={0.999} step={0.01} sx={sliderSx} />
        </Box>
      )}

      <FormControlLabel
        control={<Switch checked={autoRotate} onChange={(_, v) => setAutoRotate(v)} />}
        label={<span style={{ color: '#cdeac0', fontFamily: 'monospace', fontSize: 13 }}>Auto-rotate camera</span>}
      />
      <FormControlLabel
        control={<Switch checked={fireflies} onChange={(_, v) => setFireflies(v)} />}
        label={<span style={{ color: '#cdeac0', fontFamily: 'monospace', fontSize: 13 }}>Fireflies</span>}
      />
      <FormControlLabel
        control={<Switch checked={flowers} onChange={(_, v) => setFlowers(v)} />}
        label={<span style={{ color: '#cdeac0', fontFamily: 'monospace', fontSize: 13 }}>Wildflowers</span>}
      />

      <Divider sx={{ my: 2, borderColor: '#2d5a2d' }} />

      <Typography variant="subtitle2" sx={headSx}>WIND</Typography>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Wind Speed: {windSpeed.toFixed(1)}</Typography>
        <Slider value={windSpeed} onChange={(_, v) => setWindSpeed(v as number)} min={0} max={4} step={0.1} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Wind Strength: {windStrength.toFixed(2)}</Typography>
        <Slider value={windStrength} onChange={(_, v) => setWindStrength(v as number)} min={0} max={1} step={0.05} sx={sliderSx} />
      </Box>

      <Divider sx={{ my: 2, borderColor: '#2d5a2d' }} />

      <Typography variant="subtitle2" sx={headSx}>FIELD</Typography>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Grass Density: {grassDensity}</Typography>
        <Slider value={grassDensity} onChange={(_, v) => setGrassDensity(v as number)} min={100} max={2400} step={100} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Chunk Size: {chunkSize}</Typography>
        <Slider value={chunkSize} onChange={(_, v) => setChunkSize(v as number)} min={5} max={20} step={1} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Chunk Count: {chunkCount}×{chunkCount}</Typography>
        <Slider value={chunkCount} onChange={(_, v) => setChunkCount(v as number)} min={2} max={14} step={2} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Blade Height: {grassHeight.toFixed(1)}</Typography>
        <Slider value={grassHeight} onChange={(_, v) => setGrassHeight(v as number)} min={0.4} max={2.5} step={0.1} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Blade Width: {grassWidth.toFixed(2)}</Typography>
        <Slider value={grassWidth} onChange={(_, v) => setGrassWidth(v as number)} min={0.04} max={0.25} step={0.01} sx={sliderSx} />
      </Box>

      <Typography variant="caption" sx={{ color: '#7da876', fontFamily: 'monospace', display: 'block', mt: 3 }}>
        {isMobile ? 'Pinch to zoom · drag to look' : 'Drag to look · scroll to zoom'}
      </Typography>
    </>
  );

  // The FluffyGrass component is wrapped in a flex Box with no explicit
  // pixel size — the component reads its container's clientWidth/Height
  // via ResizeObserver. This way we never recompute window-based sizes on
  // the JSX side and the canvas tracks the layout cleanly.
  const grass = (
    <FluffyGrass
      key={sceneKey}
      grassDensity={grassDensity}
      chunkSize={chunkSize}
      chunkCount={chunkCount}
      grassHeight={grassHeight}
      grassWidth={grassWidth}
      windSpeed={windSpeed}
      windStrength={windStrength}
      dayLengthSeconds={autoCycle ? dayLengthSeconds : 0}
      fixedTimeOfDay={fixedTimeOfDay}
      autoRotate={autoRotate}
      fireflies={fireflies}
      flowers={flowers}
    />
  );

  if (isMobile) {
    return (
      <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden', position: 'relative' }}>
        <Box sx={{ width: '100%', height: '100%', position: 'absolute', inset: 0 }}>
          {grass}
        </Box>

        {/* Top-right Cast pill */}
        <CastButton />

        {/* Bottom-right Settings FAB — opens the drawer with the controls. */}
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
            color: '#9be38a',
            border: '1px solid #9be38a',
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
              backgroundColor: 'rgba(8, 14, 8, 0.96)',
              borderLeft: '1px solid #2d5a2d',
            },
          }}
        >
          {controlsContent}
        </Drawer>
      </Box>
    );
  }

  // Desktop: side-by-side viewport + 320px panel.
  return (
    <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
      <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
        <Box sx={{ flex: 1, display: 'flex', position: 'relative', minWidth: 0 }}>
          {grass}
          <CastButton />
        </Box>

        <Paper
          sx={{
            width: 320,
            padding: 3,
            backgroundColor: 'rgba(8, 14, 8, 0.92)',
            border: '1px solid #2d5a2d',
            overflowY: 'auto',
          }}
        >
          {controlsContent}
        </Paper>
      </Stack>
    </Box>
  );
};

export default FluffyGrassDemo;
