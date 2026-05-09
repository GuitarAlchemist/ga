/**
 * Cheese Avalanche test page — apéricube + babybel physics demo.
 *
 * Full-bleed simulation viewport with a compact controls panel on the
 * right (matching FluffyGrass / SandDunes style) and a Chromecast overlay
 * button in the top-right corner.
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
} from '@mui/material';
import { CheeseAvalanche } from '../components/CheeseAvalanche';
import CastButton from '../components/Common/CastButton';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

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

const CheeseAvalancheTest: React.FC = () => {
  const [bodyCount, setBodyCount] = useState<number>(360);
  const [babybelFraction, setBabybelFraction] = useState<number>(0.35);
  const [mountainHeight, setMountainHeight] = useState<number>(28);
  const [gravity, setGravity] = useState<number>(28);

  const [autoCycle, setAutoCycle] = useState<boolean>(true);
  const [dayLengthSeconds, setDayLengthSeconds] = useState<number>(90);
  const [fixedTimeOfDay, setFixedTimeOfDay] = useState<number>(0.22);
  const [autoRotate, setAutoRotate] = useState<boolean>(true);

  const sceneKey = `${bodyCount}-${babybelFraction.toFixed(2)}-${mountainHeight}-${gravity}-${autoCycle ? 'cycle' : 'fixed'}-${dayLengthSeconds}-${fixedTimeOfDay.toFixed(2)}-${autoRotate}`;

  const sliderSx = {
    color: '#ffd25b',
    '& .MuiSlider-thumb': { backgroundColor: '#ffd25b' },
    '& .MuiSlider-track': { backgroundColor: '#ffd25b' },
    '& .MuiSlider-rail':  { backgroundColor: '#ffd25b', opacity: 0.3 },
  };
  const labelSx = { color: '#fff5d8', fontFamily: 'monospace', mb: 1 };
  const headSx  = { color: '#ffd25b', fontFamily: 'monospace', mb: 1, mt: 2 };

  return (
    <DemoErrorBoundary demoName="Cheese Avalanche">
      <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
        <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
          <Box sx={{ flex: 1, display: 'flex', position: 'relative' }}>
            <CheeseAvalanche
              key={sceneKey}
              bodyCount={bodyCount}
              babybelFraction={babybelFraction}
              mountainHeight={mountainHeight}
              gravity={gravity}
              dayLengthSeconds={autoCycle ? dayLengthSeconds : 0}
              fixedTimeOfDay={fixedTimeOfDay}
              autoRotate={autoRotate}
            />
            <CastButton />
          </Box>

          <Paper
            sx={{
              width: 320,
              padding: 3,
              backgroundColor: 'rgba(20, 12, 6, 0.94)',
              border: '1px solid #6e4c1f',
              overflowY: 'auto',
            }}
          >
            <Typography variant="h5" sx={{ color: '#ffd25b', fontFamily: 'monospace', mb: 1 }}>
              🧀 CHEESE AVALANCHE
            </Typography>
            <Typography variant="caption" sx={{ color: '#c8a47a', fontFamily: 'monospace', display: 'block', mb: 2 }}>
              apéricubes + babybels · heightfield physics
            </Typography>

            <Typography variant="subtitle2" sx={headSx}>SIMULATION</Typography>
            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" sx={labelSx}>Total bodies: {bodyCount}</Typography>
              <Slider value={bodyCount} onChange={(_, v) => setBodyCount(v as number)} min={60} max={800} step={20} sx={sliderSx} />
            </Box>
            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" sx={labelSx}>Babybel fraction: {Math.round(babybelFraction * 100)}%</Typography>
              <Slider value={babybelFraction} onChange={(_, v) => setBabybelFraction(v as number)} min={0} max={1} step={0.05} sx={sliderSx} />
            </Box>
            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" sx={labelSx}>Mountain height: {mountainHeight}m</Typography>
              <Slider value={mountainHeight} onChange={(_, v) => setMountainHeight(v as number)} min={12} max={50} step={2} sx={sliderSx} />
            </Box>
            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" sx={labelSx}>Gravity: {gravity} m/s²</Typography>
              <Slider value={gravity} onChange={(_, v) => setGravity(v as number)} min={5} max={60} step={1} sx={sliderSx} />
            </Box>

            <Divider sx={{ my: 2, borderColor: '#6e4c1f' }} />

            <Typography variant="subtitle2" sx={headSx}>ATMOSPHERE</Typography>
            <FormControlLabel
              control={<Switch checked={autoCycle} onChange={(_, v) => setAutoCycle(v)} />}
              label={<span style={{ color: '#fff5d8', fontFamily: 'monospace', fontSize: 13 }}>Day/Night Cycle</span>}
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
              label={<span style={{ color: '#fff5d8', fontFamily: 'monospace', fontSize: 13 }}>Auto-rotate camera</span>}
            />

            <Typography variant="caption" sx={{ color: '#c8a47a', fontFamily: 'monospace', display: 'block', mt: 3 }}>
              Drag to look · scroll to zoom
            </Typography>
          </Paper>
        </Stack>
      </Box>
    </DemoErrorBoundary>
  );
};

export default CheeseAvalancheTest;
