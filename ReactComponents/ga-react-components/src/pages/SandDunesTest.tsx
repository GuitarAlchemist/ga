/**
 * Sand Dunes test page — cinematic re-implementation.
 *
 * Mirrors FluffyGrassDemo's layout: viewport on the left, vertical controls
 * panel on the right. Surfaces the day-cycle / wind / particle / mirage
 * uniforms exposed by the new SandDunes component.
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
import SandDunes from '../components/SandDunes/SandDunes';
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

const SandDunesTest: React.FC = () => {
  const [autoCycle, setAutoCycle] = useState<boolean>(true);
  const [dayLengthSeconds, setDayLengthSeconds] = useState<number>(90);
  const [fixedTimeOfDay, setFixedTimeOfDay] = useState<number>(0.20);

  const [windDeg, setWindDeg] = useState<number>(23);
  const [fieldSize, setFieldSize] = useState<number>(600);
  const [fieldSegments, setFieldSegments] = useState<number>(320);

  const [autoRotate, setAutoRotate] = useState<boolean>(true);
  const [sandParticles, setSandParticles] = useState<boolean>(true);
  const [mirage, setMirage] = useState<boolean>(true);

  const sceneKey = `${fieldSize}-${fieldSegments}-${windDeg}-${autoCycle ? 'cycle' : 'fixed'}-${dayLengthSeconds}-${fixedTimeOfDay.toFixed(2)}-${autoRotate}-${sandParticles}-${mirage}`;

  const sliderSx = {
    color: '#ffd58a',
    '& .MuiSlider-thumb': { backgroundColor: '#ffd58a' },
    '& .MuiSlider-track': { backgroundColor: '#ffd58a' },
    '& .MuiSlider-rail':  { backgroundColor: '#ffd58a', opacity: 0.3 },
  };
  const labelSx = { color: '#f4ddc0', fontFamily: 'monospace', mb: 1 };
  const headSx  = { color: '#ffd58a', fontFamily: 'monospace', mb: 1, mt: 2 };

  return (
    <DemoErrorBoundary demoName="Sand Dunes">
      <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
        <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
          <Box sx={{ flex: 1, display: 'flex', position: 'relative' }}>
            <CastButton />
            <SandDunes
              key={sceneKey}
              fieldSize={fieldSize}
              fieldSegments={fieldSegments}
              windDirRad={(windDeg * Math.PI) / 180}
              dayLengthSeconds={autoCycle ? dayLengthSeconds : 0}
              fixedTimeOfDay={fixedTimeOfDay}
              autoRotate={autoRotate}
              sandParticles={sandParticles}
              mirage={mirage}
            />
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
            <Typography variant="h5" sx={{ color: '#ffd58a', fontFamily: 'monospace', mb: 1 }}>
              🏜️ SAND DUNES
            </Typography>
            <Typography variant="caption" sx={{ color: '#c8a47a', fontFamily: 'monospace', display: 'block', mb: 2 }}>
              cinematic v2 — ridged dunes · day/night · mirage · sand drift
            </Typography>

            <Typography variant="subtitle2" sx={headSx}>ATMOSPHERE</Typography>

            <FormControlLabel
              control={<Switch checked={autoCycle} onChange={(_, v) => setAutoCycle(v)} />}
              label={<span style={{ color: '#f4ddc0', fontFamily: 'monospace', fontSize: 13 }}>Day/Night Cycle</span>}
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
              label={<span style={{ color: '#f4ddc0', fontFamily: 'monospace', fontSize: 13 }}>Auto-rotate camera</span>}
            />
            <FormControlLabel
              control={<Switch checked={sandParticles} onChange={(_, v) => setSandParticles(v)} />}
              label={<span style={{ color: '#f4ddc0', fontFamily: 'monospace', fontSize: 13 }}>Airborne sand</span>}
            />
            <FormControlLabel
              control={<Switch checked={mirage} onChange={(_, v) => setMirage(v)} />}
              label={<span style={{ color: '#f4ddc0', fontFamily: 'monospace', fontSize: 13 }}>Heat shimmer (mirage)</span>}
            />

            <Divider sx={{ my: 2, borderColor: '#6e4c1f' }} />

            <Typography variant="subtitle2" sx={headSx}>WIND</Typography>
            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" sx={labelSx}>Direction: {windDeg}°</Typography>
              <Slider value={windDeg} onChange={(_, v) => setWindDeg(v as number)} min={0} max={359} step={1} sx={sliderSx} />
            </Box>

            <Divider sx={{ my: 2, borderColor: '#6e4c1f' }} />

            <Typography variant="subtitle2" sx={headSx}>FIELD</Typography>
            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" sx={labelSx}>Field Size: {fieldSize}m</Typography>
              <Slider value={fieldSize} onChange={(_, v) => setFieldSize(v as number)} min={300} max={1200} step={50} sx={sliderSx} />
            </Box>
            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" sx={labelSx}>Heightmap Resolution: {fieldSegments}²</Typography>
              <Slider value={fieldSegments} onChange={(_, v) => setFieldSegments(v as number)} min={128} max={512} step={32} sx={sliderSx} />
            </Box>

            <Typography variant="caption" sx={{ color: '#c8a47a', fontFamily: 'monospace', display: 'block', mt: 3 }}>
              Drag to look · scroll to zoom
            </Typography>
          </Paper>
        </Stack>
      </Box>
    </DemoErrorBoundary>
  );
};

export default SandDunesTest;
