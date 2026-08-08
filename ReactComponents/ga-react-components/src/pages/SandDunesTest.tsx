/**
 * Sand Dunes test page — phone/tablet-friendly via ResponsiveDemoShell.
 *
 * Surfaces day-cycle / wind / particle / mirage uniforms exposed by the
 * SandDunes component through a controls panel that becomes a Drawer on
 * mobile.
 */

import React, { useState } from 'react';
import {
  Box,
  Typography,
  Slider,
  Switch,
  FormControlLabel,
  Divider,
} from '@mui/material';
import SandDunes from '../components/SandDunes/SandDunes';
import ResponsiveDemoShell, { useIsMobile } from '../components/Common/ResponsiveDemoShell';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

/** Compass + wind arrow that tracks the current wind direction. */
const SandDunesCompass: React.FC<{ windDeg: number }> = ({ windDeg }) => (
  <svg width={88} height={88} viewBox="0 0 88 88">
    <circle cx={44} cy={44} r={40} fill="rgba(20,12,6,0.7)" stroke="#6e4c1f" strokeWidth={1} />
    <line x1={44} y1={8} x2={44} y2={80} stroke="#c8a47a" strokeWidth={1} opacity={0.5} />
    <line x1={8} y1={44} x2={80} y2={44} stroke="#c8a47a" strokeWidth={1} opacity={0.5} />
    <text x={44} y={18} textAnchor="middle" fill="#ff6b6b" fontSize="11" fontFamily="monospace" fontWeight={700}>
      N
    </text>
    <text x={78} y={48} textAnchor="middle" fill="#f4ddc0" fontSize="11" fontFamily="monospace">
      E
    </text>
    <text x={44} y={80} textAnchor="middle" fill="#f4ddc0" fontSize="11" fontFamily="monospace">
      S
    </text>
    <text x={10} y={48} textAnchor="middle" fill="#f4ddc0" fontSize="11" fontFamily="monospace">
      W
    </text>
    <g transform={`rotate(${windDeg} 44 44)`}>
      <path d="M44 12 L38 32 L44 26 L50 32 Z" fill="#ffd58a" />
      <path d="M44 76 L38 56 L44 62 L50 56 Z" fill="#c8a47a" opacity={0.6} />
    </g>
    <text x={44} y={56} textAnchor="middle" fill="#ffd58a" fontSize="8" fontFamily="monospace">
      wind
    </text>
  </svg>
);

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
  const isMobile = useIsMobile();

  const [autoCycle, setAutoCycle] = useState<boolean>(true);
  const [dayLengthSeconds, setDayLengthSeconds] = useState<number>(90);
  const [fixedTimeOfDay, setFixedTimeOfDay] = useState<number>(0.20);

  const [windDeg, setWindDeg] = useState<number>(23);
  // Mobile defaults: smaller field + lower-res heightmap so phones hit 60fps.
  const [fieldSize, setFieldSize] = useState<number>(isMobile ? 450 : 600);
  const [fieldSegments, setFieldSegments] = useState<number>(isMobile ? 192 : 320);

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

  const controls = (
    <>
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
        {isMobile ? 'Pinch to zoom · drag to look' : 'Drag to look · scroll to zoom'}
      </Typography>
    </>
  );

  const viewport = (
    <Box sx={{ position: 'relative', width: '100%', height: '100%' }}>
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
      <Box
        sx={{
          position: 'absolute',
          bottom: 16,
          left: 16,
          bgcolor: 'rgba(20,12,6,0.5)',
          backdropFilter: 'blur(6px)',
          borderRadius: '50%',
          border: '1px solid #6e4c1f',
          p: 0.5,
          pointerEvents: 'none',
        }}
      >
        <SandDunesCompass windDeg={windDeg} />
      </Box>
    </Box>
  );

  return (
    <DemoErrorBoundary demoName="Sand Dunes">
      <ResponsiveDemoShell
        viewport={viewport}
        controls={controls}
        panelBackgroundColor="rgba(20, 12, 6, 0.94)"
        panelBorderColor="#6e4c1f"
        cogColor="#ffd58a"
      />
    </DemoErrorBoundary>
  );
};

export default SandDunesTest;
