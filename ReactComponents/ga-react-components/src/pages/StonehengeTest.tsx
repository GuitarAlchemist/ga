/**
 * Stonehenge test page — restored monument scene.
 *
 * Phone-friendly from the start: full-bleed canvas, settings cog FAB on
 * mobile opening a Drawer with the controls, side-by-side viewport +
 * 320px panel on desktop. Cast pill anchored top-right on both layouts.
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
  Drawer,
  IconButton,
  useMediaQuery,
} from '@mui/material';
import SettingsIcon from '@mui/icons-material/Settings';
import CloseIcon from '@mui/icons-material/Close';
import { Stonehenge } from '../components/Stonehenge';
import CastButton from '../components/Common/CastButton';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

const labelForTod = (t: number): string => {
  if (t < 0.05 || t > 0.95) return 'Solstice Sunrise';
  if (t < 0.20) return 'Morning';
  if (t < 0.30) return 'Noon';
  if (t < 0.45) return 'Afternoon';
  if (t < 0.55) return 'Sunset';
  if (t < 0.70) return 'Dusk';
  if (t < 0.85) return 'Night';
  return 'Pre-dawn';
};

const StonehengeTest: React.FC = () => {
  const coarsePointer = useMediaQuery('(pointer: coarse)');
  const narrowVp      = useMediaQuery('(max-width: 900px)');
  const isMobile      = coarsePointer || narrowVp;

  const [autoCycle, setAutoCycle] = useState<boolean>(true);
  const [dayLengthSeconds, setDayLengthSeconds] = useState<number>(120);
  const [fixedTimeOfDay, setFixedTimeOfDay] = useState<number>(0.05); // solstice sunrise
  const [autoRotate, setAutoRotate] = useState<boolean>(true);
  const [ravens, setRavens] = useState<boolean>(true);

  const [drawerOpen, setDrawerOpen] = useState<boolean>(false);

  const sceneKey = `${autoCycle ? 'cycle' : 'fixed'}-${dayLengthSeconds}-${fixedTimeOfDay.toFixed(2)}-${autoRotate}-${ravens}`;

  const sliderSx = {
    color: '#d4c5a0',
    '& .MuiSlider-thumb': { backgroundColor: '#d4c5a0' },
    '& .MuiSlider-track': { backgroundColor: '#d4c5a0' },
    '& .MuiSlider-rail':  { backgroundColor: '#d4c5a0', opacity: 0.3 },
  };
  const labelSx = { color: '#e8dfc8', fontFamily: 'monospace', mb: 1 };
  const headSx  = { color: '#d4c5a0', fontFamily: 'monospace', mb: 1, mt: 2 };

  const controlsContent = (
    <>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
        <Typography variant="h5" sx={{ color: '#d4c5a0', fontFamily: 'monospace' }}>
          🪨 STONEHENGE
        </Typography>
        {isMobile && (
          <IconButton aria-label="Close settings" onClick={() => setDrawerOpen(false)} sx={{ color: '#d4c5a0' }}>
            <CloseIcon />
          </IconButton>
        )}
      </Stack>
      <Typography variant="caption" sx={{ color: '#a89878', fontFamily: 'monospace', display: 'block', mb: 2 }}>
        restored to its original form, c. 2500 BCE
      </Typography>

      <Typography variant="subtitle2" sx={headSx}>ATMOSPHERE</Typography>
      <FormControlLabel
        control={<Switch checked={autoCycle} onChange={(_, v) => setAutoCycle(v)} />}
        label={<span style={{ color: '#e8dfc8', fontFamily: 'monospace', fontSize: 13 }}>Day/Night Cycle</span>}
      />
      {autoCycle ? (
        <Box sx={{ mb: 2, mt: 1 }}>
          <Typography variant="body2" sx={labelSx}>Day Length: {dayLengthSeconds}s</Typography>
          <Slider value={dayLengthSeconds} onChange={(_, v) => setDayLengthSeconds(v as number)} min={30} max={300} step={10} sx={sliderSx} />
        </Box>
      ) : (
        <Box sx={{ mb: 2, mt: 1 }}>
          <Typography variant="body2" sx={labelSx}>Time of Day: {labelForTod(fixedTimeOfDay)}</Typography>
          <Slider value={fixedTimeOfDay} onChange={(_, v) => setFixedTimeOfDay(v as number)} min={0} max={0.999} step={0.01} sx={sliderSx} />
        </Box>
      )}

      <FormControlLabel
        control={<Switch checked={autoRotate} onChange={(_, v) => setAutoRotate(v)} />}
        label={<span style={{ color: '#e8dfc8', fontFamily: 'monospace', fontSize: 13 }}>Auto-rotate camera</span>}
      />
      <FormControlLabel
        control={<Switch checked={ravens} onChange={(_, v) => setRavens(v)} />}
        label={<span style={{ color: '#e8dfc8', fontFamily: 'monospace', fontSize: 13 }}>Ravens at dusk</span>}
      />

      <Typography variant="caption" sx={{ color: '#a89878', fontFamily: 'monospace', display: 'block', mt: 3 }}>
        {isMobile ? 'Pinch to zoom · drag to look' : 'Drag to look · scroll to zoom'}
      </Typography>
      <Typography variant="caption" sx={{ color: '#a89878', fontFamily: 'monospace', display: 'block', mt: 1 }}>
        Heel stone marks the solstice-sunrise alignment.
      </Typography>
    </>
  );

  const henge = (
    <Stonehenge
      key={sceneKey}
      dayLengthSeconds={autoCycle ? dayLengthSeconds : 0}
      fixedTimeOfDay={fixedTimeOfDay}
      autoRotate={autoRotate}
      ravens={ravens}
    />
  );

  if (isMobile) {
    return (
      <DemoErrorBoundary demoName="Stonehenge">
        <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden', position: 'relative' }}>
          <Box sx={{ width: '100%', height: '100%', position: 'absolute', inset: 0 }}>
            {henge}
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
              color: '#d4c5a0',
              border: '1px solid #d4c5a0',
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
                backgroundColor: 'rgba(20, 16, 8, 0.96)',
                borderLeft: '1px solid #6e5a3a',
              },
            }}
          >
            {controlsContent}
          </Drawer>
        </Box>
      </DemoErrorBoundary>
    );
  }

  return (
    <DemoErrorBoundary demoName="Stonehenge">
      <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
        <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
          <Box sx={{ flex: 1, display: 'flex', position: 'relative', minWidth: 0 }}>
            {henge}
            <CastButton />
          </Box>
          <Paper
            sx={{
              width: 320,
              padding: 3,
              backgroundColor: 'rgba(20, 16, 8, 0.94)',
              border: '1px solid #6e5a3a',
              overflowY: 'auto',
            }}
          >
            {controlsContent}
          </Paper>
        </Stack>
      </Box>
    </DemoErrorBoundary>
  );
};

export default StonehengeTest;
