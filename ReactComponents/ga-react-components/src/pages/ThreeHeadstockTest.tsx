import React, { useState } from 'react';
import {
  Container,
  Box,
  Typography,
  Grid,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormControlLabel,
  Switch,
  Paper,
  Divider,
} from '@mui/material';
import { ThreeHeadstock } from '../components/ThreeHeadstock';
import type { HeadstockStyle } from '../components/GuitarModels';
import { GUITAR_MODELS } from '../components/GuitarModels';

export const ThreeHeadstockTest: React.FC = () => {
  const [guitarModel, setGuitarModel] = useState('electric_fender_strat');
  const [headstockStyle, setHeadstockStyle] = useState<HeadstockStyle>('electric');
  const [stringCount, setStringCount] = useState(6);
  const [leftHanded, setLeftHanded] = useState(false);
  const [enableOrbitControls, setEnableOrbitControls] = useState(true);

  const selectedGuitarStyle = GUITAR_MODELS[guitarModel];
  const tuning = selectedGuitarStyle?.tuning || ['E', 'A', 'D', 'G', 'B', 'E'];

  const headstockStyles: { value: HeadstockStyle; label: string }[] = [
    { value: 'electric', label: 'Electric (6-Inline)' },
    { value: 'acoustic', label: 'Acoustic (3x3)' },
    { value: 'classical', label: 'Classical (Slotted)' },
  ];

  const handleTuningPegClick = (stringIndex: number, tuning: string) => {
    console.log(`Clicked tuning peg for string ${stringIndex + 1}: ${tuning}`);
  };

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" gutterBottom>
          ThreeHeadstock Test Page
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          This page is dedicated to testing the ThreeHeadstock component (Three.js 3D WebGPU).
          Test different guitar models, headstock styles, and configurations.
        </Typography>
      </Box>

      <Grid container spacing={4}>
        {/* Controls Panel */}
        <Grid item xs={12} md={4}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Configuration
            </Typography>
            
            <Box sx={{ mb: 3 }}>
              <FormControl fullWidth>
                <InputLabel>Guitar Model</InputLabel>
                <Select
                  value={guitarModel}
                  label="Guitar Model"
                  onChange={(e) => setGuitarModel(e.target.value)}
                >
                  {Object.entries(GUITAR_MODELS).map(([key, model]) => (
                    <MenuItem key={key} value={key}>
                      {model.name} ({model.brand})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>

            <Box sx={{ mb: 3 }}>
              <FormControl fullWidth>
                <InputLabel>Headstock Style</InputLabel>
                <Select
                  value={headstockStyle}
                  label="Headstock Style"
                  onChange={(e) => setHeadstockStyle(e.target.value as HeadstockStyle)}
                >
                  {headstockStyles.map((style) => (
                    <MenuItem key={style.value} value={style.value}>
                      {style.label}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>

            <Box sx={{ mb: 3 }}>
              <FormControl fullWidth>
                <InputLabel>String Count</InputLabel>
                <Select
                  value={stringCount}
                  label="String Count"
                  onChange={(e) => setStringCount(Number(e.target.value))}
                >
                  <MenuItem value={4}>4 Strings (Bass)</MenuItem>
                  <MenuItem value={6}>6 Strings (Guitar)</MenuItem>
                  <MenuItem value={7}>7 Strings (Extended)</MenuItem>
                  <MenuItem value={12}>12 Strings (Double)</MenuItem>
                </Select>
              </FormControl>
            </Box>

            <Box sx={{ mb: 3 }}>
              <FormControlLabel
                control={
                  <Switch
                    checked={leftHanded}
                    onChange={(e) => setLeftHanded(e.target.checked)}
                  />
                }
                label="Left Handed"
              />
            </Box>

            <Box sx={{ mb: 3 }}>
              <FormControlLabel
                control={
                  <Switch
                    checked={enableOrbitControls}
                    onChange={(e) => setEnableOrbitControls(e.target.checked)}
                  />
                }
                label="Enable Orbit Controls"
              />
            </Box>

            <Divider sx={{ my: 2 }} />

            <Typography variant="subtitle2" gutterBottom>
              Current Configuration:
            </Typography>
            <Typography variant="body2" color="text.secondary">
              <strong>Model:</strong> {selectedGuitarStyle?.name}<br />
              <strong>Brand:</strong> {selectedGuitarStyle?.brand}<br />
              <strong>Category:</strong> {selectedGuitarStyle?.category}<br />
              <strong>Tuning:</strong> {tuning.join(' - ')}<br />
              <strong>Scale Length:</strong> {selectedGuitarStyle?.scaleLength}mm<br />
              <strong>Nut Width:</strong> {selectedGuitarStyle?.nutWidth}mm
            </Typography>
          </Paper>
        </Grid>

        {/* 3D Headstock Display */}
        <Grid item xs={12} md={8}>
          <Paper sx={{ p: 2 }}>
            <ThreeHeadstock
              title={`3D ${selectedGuitarStyle?.name} Headstock`}
              guitarModel={guitarModel}
              headstockStyle={headstockStyle}
              stringCount={stringCount}
              tuning={tuning}
              leftHanded={leftHanded}
              width={800}
              height={600}
              enableOrbitControls={enableOrbitControls}
              onTuningPegClick={handleTuningPegClick}
            />
          </Paper>
        </Grid>
      </Grid>

      {/* Multiple Headstock Comparison */}
      <Box sx={{ mt: 6 }}>
        <Typography variant="h5" gutterBottom>
          Headstock Style Comparison
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          Compare different headstock styles side by side:
        </Typography>
        
        <Grid container spacing={3}>
          <Grid item xs={12} md={4}>
            <Paper sx={{ p: 2 }}>
              <ThreeHeadstock
                title="Electric Style (6-Inline)"
                guitarModel="electric_fender_strat"
                headstockStyle="electric"
                stringCount={6}
                width={400}
                height={300}
                enableOrbitControls={true}
              />
            </Paper>
          </Grid>

          <Grid item xs={12} md={4}>
            <Paper sx={{ p: 2 }}>
              <ThreeHeadstock
                title="Acoustic Style (3x3)"
                guitarModel="acoustic_martin_d28"
                headstockStyle="acoustic"
                stringCount={6}
                width={400}
                height={300}
                enableOrbitControls={true}
              />
            </Paper>
          </Grid>

          <Grid item xs={12} md={4}>
            <Paper sx={{ p: 2 }}>
              <ThreeHeadstock
                title="Classical Style (Slotted)"
                guitarModel="classical_yamaha_cg"
                headstockStyle="classical"
                stringCount={6}
                width={400}
                height={300}
                enableOrbitControls={true}
              />
            </Paper>
          </Grid>
        </Grid>
      </Box>

      {/* Technical Information */}
      <Box sx={{ mt: 6 }}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>
            Technical Information
          </Typography>
          <Typography variant="body2" color="text.secondary">
            <strong>Rendering:</strong> Three.js with WebGPU backend<br />
            <strong>Materials:</strong> PBR (Physically Based Rendering) with wood textures<br />
            <strong>Lighting:</strong> Ambient + Directional + Fill lighting setup<br />
            <strong>Controls:</strong> Orbit controls for 3D navigation<br />
            <strong>Features:</strong> Real-time 3D rendering, multiple headstock styles, configurable guitar models
          </Typography>
        </Paper>
      </Box>
    </Container>
  );
};

export default ThreeHeadstockTest;
