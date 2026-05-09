/**
 * Stonehenge test page — uses ResponsiveDemoShell.
 */

import React, { useState } from 'react';
import {
  Box,
  Typography,
  Slider,
  Switch,
  FormControlLabel,
} from '@mui/material';
import { Stonehenge } from '../components/Stonehenge';
import ResponsiveDemoShell, { useIsMobile } from '../components/Common/ResponsiveDemoShell';
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
  const isMobile = useIsMobile();

  const [autoCycle, setAutoCycle] = useState<boolean>(true);
  const [dayLengthSeconds, setDayLengthSeconds] = useState<number>(120);
  const [fixedTimeOfDay, setFixedTimeOfDay] = useState<number>(0.05);
  const [autoRotate, setAutoRotate] = useState<boolean>(true);
  const [ravens, setRavens] = useState<boolean>(true);

  const sceneKey = `${autoCycle ? 'cycle' : 'fixed'}-${dayLengthSeconds}-${fixedTimeOfDay.toFixed(2)}-${autoRotate}-${ravens}`;

  const sliderSx = {
    color: '#d4c5a0',
    '& .MuiSlider-thumb': { backgroundColor: '#d4c5a0' },
    '& .MuiSlider-track': { backgroundColor: '#d4c5a0' },
    '& .MuiSlider-rail':  { backgroundColor: '#d4c5a0', opacity: 0.3 },
  };
  const labelSx = { color: '#e8dfc8', fontFamily: 'monospace', mb: 1 };
  const headSx  = { color: '#d4c5a0', fontFamily: 'monospace', mb: 1, mt: 2 };

  const controls = (
    <>
      <Typography variant="h5" sx={{ color: '#d4c5a0', fontFamily: 'monospace', mb: 1 }}>
        🪨 STONEHENGE
      </Typography>
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

  const viewport = (
    <Stonehenge
      key={sceneKey}
      dayLengthSeconds={autoCycle ? dayLengthSeconds : 0}
      fixedTimeOfDay={fixedTimeOfDay}
      autoRotate={autoRotate}
      ravens={ravens}
    />
  );

  return (
    <DemoErrorBoundary demoName="Stonehenge">
      <ResponsiveDemoShell
        viewport={viewport}
        controls={controls}
        panelBackgroundColor="rgba(20, 16, 8, 0.94)"
        panelBorderColor="#6e5a3a"
        cogColor="#d4c5a0"
      />
    </DemoErrorBoundary>
  );
};

export default StonehengeTest;
