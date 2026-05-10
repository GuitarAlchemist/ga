/**
 * Fluffy Animals test page.
 */

import React, { useState } from 'react';
import {
  Box,
  Typography,
  Slider,
  Switch,
  FormControlLabel,
} from '@mui/material';
import { FluffyAnimals } from '../components/FluffyAnimals';
import ResponsiveDemoShell, { useIsMobile } from '../components/Common/ResponsiveDemoShell';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

const FluffyAnimalsTest: React.FC = () => {
  const isMobile = useIsMobile();
  const [furDensity, setFurDensity] = useState<number>(isMobile ? 0.7 : 1.0);
  const [furLength, setFurLength] = useState<number>(1.0);
  const [windStrength, setWindStrength] = useState<number>(0.5);
  const [autoRotate, setAutoRotate] = useState<boolean>(true);

  const sceneKey = `${furDensity.toFixed(2)}-${furLength.toFixed(2)}-${autoRotate}`;

  const sliderSx = {
    color: '#f5c97a',
    '& .MuiSlider-thumb': { backgroundColor: '#f5c97a' },
    '& .MuiSlider-track': { backgroundColor: '#f5c97a' },
    '& .MuiSlider-rail':  { backgroundColor: '#f5c97a', opacity: 0.3 },
  };
  const labelSx = { color: '#fae6c2', fontFamily: 'monospace', mb: 1 };
  const headSx  = { color: '#f5c97a', fontFamily: 'monospace', mb: 1, mt: 2 };

  const controls = (
    <>
      <Typography variant="h5" sx={{ color: '#f5c97a', fontFamily: 'monospace', mb: 1 }}>
        🦊 FLUFFY ANIMALS
      </Typography>
      <Typography variant="caption" sx={{ color: '#c9a87a', fontFamily: 'monospace', display: 'block', mb: 2 }}>
        bezier-blade fur on ellipsoid bodies — bear · sheep · fox · bunny · cat
      </Typography>

      <Typography variant="subtitle2" sx={headSx}>FUR</Typography>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Density: {furDensity.toFixed(2)}×</Typography>
        <Slider value={furDensity} onChange={(_, v) => setFurDensity(v as number)} min={0.3} max={2.0} step={0.05} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Length: {furLength.toFixed(2)}×</Typography>
        <Slider value={furLength} onChange={(_, v) => setFurLength(v as number)} min={0.4} max={2.0} step={0.05} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Wind: {windStrength.toFixed(2)}</Typography>
        <Slider value={windStrength} onChange={(_, v) => setWindStrength(v as number)} min={0} max={2} step={0.05} sx={sliderSx} />
      </Box>

      <FormControlLabel
        control={<Switch checked={autoRotate} onChange={(_, v) => setAutoRotate(v)} />}
        label={<span style={{ color: '#fae6c2', fontFamily: 'monospace', fontSize: 13 }}>Auto-rotate camera</span>}
      />

      <Typography variant="caption" sx={{ color: '#c9a87a', fontFamily: 'monospace', display: 'block', mt: 3 }}>
        {isMobile ? 'Pinch to zoom · drag to look' : 'Drag to look · scroll to zoom'}
      </Typography>
    </>
  );

  const viewport = (
    <FluffyAnimals
      key={sceneKey}
      furDensity={furDensity}
      furLength={furLength}
      windStrength={windStrength}
      autoRotate={autoRotate}
    />
  );

  return (
    <DemoErrorBoundary demoName="Fluffy Animals">
      <ResponsiveDemoShell
        viewport={viewport}
        controls={controls}
        panelBackgroundColor="rgba(20, 14, 6, 0.94)"
        panelBorderColor="#7a5a2e"
        cogColor="#f5c97a"
      />
    </DemoErrorBoundary>
  );
};

export default FluffyAnimalsTest;
