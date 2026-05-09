/**
 * FluffyGrassDemo — uses the shared ResponsiveDemoShell.
 *
 * Desktop: viewport + 320px controls panel. Mobile: full-bleed canvas
 * + cog FAB → Drawer. Cast pill anchored top-right on both.
 */

import React, { useState } from 'react';
import {
  Box,
  Typography,
  Stack,
  Slider,
  Switch,
  FormControlLabel,
  Divider,
} from '@mui/material';
import { FluffyGrass } from './FluffyGrass';
import ResponsiveDemoShell, { useIsMobile } from '../Common/ResponsiveDemoShell';

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
  const isMobile = useIsMobile();

  // Mobile-aware defaults — phone GPUs need lower density / fewer chunks.
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

  const sceneKey = `${grassDensity}-${chunkSize}-${chunkCount}-${grassHeight}-${grassWidth}-${autoCycle ? 'cycle' : 'fixed'}-${dayLengthSeconds}-${fixedTimeOfDay.toFixed(2)}-${autoRotate}-${fireflies}-${flowers}`;

  const sliderSx = {
    color: '#9be38a',
    '& .MuiSlider-thumb': { backgroundColor: '#9be38a' },
    '& .MuiSlider-track': { backgroundColor: '#9be38a' },
    '& .MuiSlider-rail': { backgroundColor: '#9be38a', opacity: 0.3 },
  };
  const labelSx = { color: '#cdeac0', fontFamily: 'monospace', mb: 1 };
  const headSx = { color: '#9be38a', fontFamily: 'monospace', mb: 1, mt: 2 };

  const controls = (
    <>
      <Typography variant="h5" sx={{ color: '#9be38a', fontFamily: 'monospace', mb: 1 }}>
        🌾 FLUFFY GRASS
      </Typography>
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

  const viewport = (
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

  return (
    <ResponsiveDemoShell
      viewport={viewport}
      controls={
        <Box>
          {/* Wrapper Box keeps the same vertical-stack flow. */}
          <Stack>{controls}</Stack>
        </Box>
      }
      panelBackgroundColor="rgba(8, 14, 8, 0.92)"
      panelBorderColor="#2d5a2d"
      cogColor="#9be38a"
    />
  );
};

export default FluffyGrassDemo;
