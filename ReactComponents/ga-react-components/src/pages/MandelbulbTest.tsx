/**
 * Mandelbulb test page.
 */

import React, { useState } from 'react';
import {
  Box,
  Button,
  Chip,
  FormControlLabel,
  Paper,
  Slider,
  Stack,
  Switch,
  Typography,
} from '@mui/material';
import { Mandelbulb, type MandelbulbProps } from '../components/Mandelbulb';
import CastButton from '../components/Common/CastButton';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

type ColorMode = NonNullable<MandelbulbProps['colorMode']>;

const COLOR_MODES: ColorMode[] = ['obsidian', 'gold', 'ice', 'spectral', 'crystal'];

const MandelbulbTest: React.FC = () => {
  const [power, setPower] = useState(8);
  const [iterations, setIterations] = useState(12);
  const [quality, setQuality] = useState(0.72);
  const [autoRotate, setAutoRotate] = useState(true);
  const [colorMode, setColorMode] = useState<ColorMode>('obsidian');
  const [ior, setIor] = useState(1.45);
  const [dispersion, setDispersion] = useState(0.04);
  const [bloom, setBloom] = useState(0.55);
  const [fur, setFur] = useState(0);

  const sliderSx = {
    color: '#bda4ff',
    '& .MuiSlider-thumb': { backgroundColor: '#f4ecff' },
    '& .MuiSlider-track': { backgroundColor: '#bda4ff' },
    '& .MuiSlider-rail': { backgroundColor: '#3d355d', opacity: 0.7 },
  };

  return (
    <DemoErrorBoundary demoName="Mandelbulb">
      <Box sx={{ width: '100%', height: '100vh', bgcolor: '#05050d', display: 'flex', flexDirection: 'column', position: 'relative' }}>
        <CastButton />
        <Paper
          elevation={0}
          sx={{
            px: 2,
            py: 1.25,
            bgcolor: 'rgba(5,5,13,0.92)',
            color: '#f5f0ff',
            borderRadius: 0,
            borderBottom: '1px solid rgba(189,164,255,0.24)',
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            flexWrap: 'wrap',
          }}
        >
          <Typography variant="h6" sx={{ fontFamily: 'monospace', fontWeight: 800, letterSpacing: 0.5 }}>
            MANDELBULB
          </Typography>
          <Chip label="Raymarched distance field" size="small" sx={{ bgcolor: 'rgba(189,164,255,0.15)', color: '#d8c8ff', fontFamily: 'monospace' }} />
          <Chip label={`power ${power}`} size="small" sx={{ bgcolor: 'rgba(122,226,255,0.12)', color: '#9deaff', fontFamily: 'monospace' }} />
          <Typography variant="caption" sx={{ color: '#a99dcc', fontFamily: 'monospace', ml: { md: 'auto' } }}>
            Finite-difference normals - soft shadow - orbit trap color
          </Typography>
        </Paper>

        <Box sx={{ flex: 1, minHeight: 0, position: 'relative', display: 'flex' }}>
          <Box sx={{ flex: 1, minWidth: 0, position: 'relative' }}>
            <Mandelbulb
              power={power}
              iterations={iterations}
              quality={quality}
              autoRotate={autoRotate}
              colorMode={colorMode}
              ior={ior}
              dispersion={dispersion}
              bloom={bloom}
              fur={fur}
            />
            <Box
              sx={{
                position: 'absolute',
                left: 16,
                bottom: 16,
                maxWidth: 390,
                px: 1.5,
                py: 1.25,
                borderRadius: 1,
                color: '#f5f0ff',
                fontFamily: 'monospace',
                fontSize: 12,
                lineHeight: 1.55,
                bgcolor: 'rgba(5,5,13,0.58)',
                border: '1px solid rgba(189,164,255,0.22)',
                backdropFilter: 'blur(8px)',
              }}
            >
              <div>Drag to orbit. Scroll to zoom into the fractal surface.</div>
              <div style={{ color: '#a99dcc' }}>This is a true Mandelbulb distance estimator, not a point cloud.</div>
            </Box>
          </Box>

          <Paper
            elevation={0}
            sx={{
              width: { xs: 0, md: 330 },
              display: { xs: 'none', md: 'block' },
              p: 2.5,
              bgcolor: 'rgba(8,7,18,0.96)',
              color: '#f5f0ff',
              borderRadius: 0,
              borderLeft: '1px solid rgba(189,164,255,0.22)',
              overflowY: 'auto',
            }}
          >
            <Typography variant="subtitle2" sx={{ fontFamily: 'monospace', color: '#d8c8ff', mb: 1 }}>
              MANDELBULB CONTROLS
            </Typography>

            <Typography variant="caption" sx={{ color: '#a99dcc', fontFamily: 'monospace', display: 'block', mb: 1 }}>
              Color Mode
            </Typography>
            <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: 'wrap', rowGap: 1 }}>
              {COLOR_MODES.map((mode) => (
                <Button
                  key={mode}
                  size="small"
                  variant={colorMode === mode ? 'contained' : 'outlined'}
                  onClick={() => setColorMode(mode)}
                  sx={{
                    fontFamily: 'monospace',
                    fontSize: 11,
                    color: colorMode === mode ? '#080712' : '#e7dcff',
                    borderColor: 'rgba(189,164,255,0.48)',
                    bgcolor: colorMode === mode ? '#d8c8ff' : 'transparent',
                    '&:hover': { borderColor: '#d8c8ff', bgcolor: colorMode === mode ? '#efe8ff' : 'rgba(189,164,255,0.08)' },
                  }}
                >
                  {mode}
                </Button>
              ))}
            </Stack>

            <FormControlLabel
              control={<Switch checked={autoRotate} onChange={(_, checked) => setAutoRotate(checked)} />}
              label={<span style={{ color: '#f5f0ff', fontFamily: 'monospace', fontSize: 13 }}>Auto-rotate camera</span>}
              sx={{ mb: 1 }}
            />

            <Box sx={{ mt: 1.5 }}>
              <Typography variant="body2" sx={{ color: '#f5f0ff', fontFamily: 'monospace', mb: 0.5 }}>
                Power: {power.toFixed(1)}
              </Typography>
              <Slider value={power} onChange={(_, value) => setPower(value as number)} min={5} max={12} step={0.25} sx={sliderSx} />
            </Box>

            <Box sx={{ mt: 1.5 }}>
              <Typography variant="body2" sx={{ color: '#f5f0ff', fontFamily: 'monospace', mb: 0.5 }}>
                Iterations: {iterations}
              </Typography>
              <Slider value={iterations} onChange={(_, value) => setIterations(value as number)} min={7} max={18} step={1} sx={sliderSx} />
            </Box>

            <Box sx={{ mt: 1.5 }}>
              <Typography variant="body2" sx={{ color: '#f5f0ff', fontFamily: 'monospace', mb: 0.5 }}>
                Quality: {quality.toFixed(2)}
              </Typography>
              <Slider value={quality} onChange={(_, value) => setQuality(value as number)} min={0} max={1} step={0.05} sx={sliderSx} />
            </Box>

            <Box sx={{ mt: 1.5 }}>
              <Typography variant="body2" sx={{ color: '#f5f0ff', fontFamily: 'monospace', mb: 0.5 }}>
                Bloom: {bloom.toFixed(2)}
              </Typography>
              <Slider value={bloom} onChange={(_, value) => setBloom(value as number)} min={0} max={1.5} step={0.05} sx={sliderSx} />
            </Box>

            <Box sx={{ mt: 1.5 }}>
              <Typography variant="body2" sx={{ color: '#f5f0ff', fontFamily: 'monospace', mb: 0.5 }}>
                Fur / filaments: {fur.toFixed(2)}
              </Typography>
              <Slider value={fur} onChange={(_, value) => setFur(value as number)} min={0} max={1} step={0.05} sx={sliderSx} />
            </Box>

            {colorMode === 'crystal' && (
              <>
                <Box sx={{ mt: 1.5 }}>
                  <Typography variant="body2" sx={{ color: '#f5f0ff', fontFamily: 'monospace', mb: 0.5 }}>
                    IOR: {ior.toFixed(2)}
                  </Typography>
                  <Slider value={ior} onChange={(_, value) => setIor(value as number)} min={1.05} max={2.4} step={0.01} sx={sliderSx} />
                </Box>
                <Box sx={{ mt: 1.5 }}>
                  <Typography variant="body2" sx={{ color: '#f5f0ff', fontFamily: 'monospace', mb: 0.5 }}>
                    Dispersion: {dispersion.toFixed(3)}
                  </Typography>
                  <Slider value={dispersion} onChange={(_, value) => setDispersion(value as number)} min={0} max={0.20} step={0.005} sx={sliderSx} />
                </Box>
              </>
            )}

            <Typography variant="caption" sx={{ color: '#a99dcc', fontFamily: 'monospace', display: 'block', mt: 3, lineHeight: 1.65 }}>
              Higher quality and iteration counts produce finer bulbs but cost more GPU time. Power 8 is the classic Mandelbulb.
              Crystal mode uses Snell-bent refraction with RGB chromatic dispersion + Fresnel-blended reflection + iridescence.
              Fur/filaments adds a sub-pixel hash field weighted toward silhouettes — works in any colour mode.
            </Typography>
          </Paper>
        </Box>
      </Box>
    </DemoErrorBoundary>
  );
};

export default MandelbulbTest;
