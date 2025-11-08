import React, { useState } from 'react';
import {
  Box,
  Typography,
  Paper,
  Slider,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Alert,
  Stack,
  Chip
} from '@mui/material';
import { ThreeFretboard } from '../components/ThreeFretboard';
import { StringedInstrumentFretboard } from '../components/StringedInstrumentFretboard';
import { PRESET_INSTRUMENTS } from '../utils/instrumentLoader';
import type { InstrumentConfig } from '../types/InstrumentConfig';

// Simple guitar instruments array for testing
const GUITAR_INSTRUMENTS: InstrumentConfig[] = [
  {
    family: 'Guitar',
    variant: 'Electric',
    displayName: 'Electric Guitar',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 22,
    bodyStyle: 'electric',
  },
  {
    family: 'Guitar',
    variant: 'Acoustic',
    displayName: 'Acoustic Guitar',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 19,
    bodyStyle: 'acoustic',
  }
];

// Sample chord positions for testing
const cMajorChord = [
  { string: 1, fret: 0, label: 'E', color: '#4CAF50' },
  { string: 2, fret: 1, label: 'C', color: '#2196F3' },
  { string: 3, fret: 0, label: 'G', color: '#FF9800' },
  { string: 4, fret: 2, label: 'C', color: '#2196F3' },
  { string: 5, fret: 3, label: 'E', color: '#4CAF50' },
  { string: 6, fret: 0, label: 'C', color: '#2196F3' },
];

export const CapoModelTest: React.FC = () => {
  const [capoPosition, setCapoPosition] = useState(2);
  const [guitarModel, setGuitarModel] = useState('electric_fender_strat');

  return (
    <Box sx={{ width: '100vw', minHeight: '100vh', p: 2, bgcolor: '#f5f5f5' }}>
      <Box sx={{ mb: 3, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h3" gutterBottom>
          3D Capo Model Test
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          Testing the integration of the Sketchfab guitar capo 3D model into the fretboard controls.
        </Typography>
        
        <Alert severity="info" sx={{ mb: 3 }}>
          <Typography variant="body2">
            <strong>Model Source:</strong> "Guitar Capo" by Chad (@cpenfold) from Sketchfab<br/>
            <strong>License:</strong> CC Attribution<br/>
            <strong>URL:</strong> https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7
          </Typography>
        </Alert>

        <Stack direction="row" spacing={2} sx={{ mb: 3 }}>
          <Chip 
            label="3D Model Loading" 
            color="primary" 
            variant="outlined" 
          />
          <Chip 
            label="Fallback Geometry" 
            color="secondary" 
            variant="outlined" 
          />
          <Chip 
            label="Real-time Positioning" 
            color="success" 
            variant="outlined" 
          />
        </Stack>
      </Box>

      {/* Controls */}
      <Paper sx={{ p: 3, mb: 4, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h6" gutterBottom>
          Controls
        </Typography>
        
        <Box sx={{ display: 'flex', gap: 4, flexWrap: 'wrap', alignItems: 'center' }}>
          {/* Capo Position Slider */}
          <Box sx={{ minWidth: 200 }}>
            <Typography gutterBottom>
              Capo Position: {capoPosition === 0 ? 'No Capo' : `Fret ${capoPosition}`}
            </Typography>
            <Slider
              value={capoPosition}
              onChange={(_, value) => setCapoPosition(value as number)}
              min={0}
              max={12}
              step={1}
              marks={[
                { value: 0, label: 'None' },
                { value: 2, label: '2' },
                { value: 5, label: '5' },
                { value: 7, label: '7' },
                { value: 12, label: '12' }
              ]}
              sx={{ width: 200 }}
            />
          </Box>

          {/* Guitar Model Selector */}
          <FormControl sx={{ minWidth: 200 }}>
            <InputLabel>Guitar Model</InputLabel>
            <Select
              value={guitarModel}
              label="Guitar Model"
              onChange={(e) => setGuitarModel(e.target.value)}
            >
              <MenuItem value="electric_fender_strat">Fender Stratocaster</MenuItem>
              <MenuItem value="electric_fender_telecaster">Fender Telecaster</MenuItem>
              <MenuItem value="electric_gibson_les_paul">Gibson Les Paul</MenuItem>
              <MenuItem value="acoustic_martin_d28">Martin D-28</MenuItem>
              <MenuItem value="classical_yamaha_c40">Yamaha C40</MenuItem>
            </Select>
          </FormControl>
        </Box>
      </Paper>

      {/* ThreeFretboard with 3D Capo Model */}
      <Paper sx={{ p: 3, mb: 4, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h5" gutterBottom>
          ThreeFretboard (3D WebGPU) - Sketchfab Capo Model
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          This component attempts to load the 3D capo model from Sketchfab. If the model fails to load,
          it will automatically fall back to the geometric capo representation.
        </Typography>
        <Alert severity="warning" sx={{ mb: 2 }}>
          <Typography variant="body2">
            <strong>Setup Required:</strong> Download the capo model from Sketchfab and place it at 
            <code>/public/models/guitar-capo.glb</code>. See the setup instructions in 
            <code>/public/models/CAPO_MODEL_SETUP.md</code>
          </Typography>
        </Alert>
        
        <ThreeFretboard
          title={`3D Fretboard - ${capoPosition === 0 ? 'No Capo' : `Capo at Fret ${capoPosition}`}`}
          positions={cMajorChord}
          config={{
            fretCount: 22,
            stringCount: 6,
            guitarModel: guitarModel,
            capoFret: capoPosition,
            leftHanded: false,
            enableOrbitControls: true,
            height: 500,
          }}
        />
      </Paper>

      {/* StringedInstrumentFretboard with 3D Capo Model */}
      <Paper sx={{ p: 3, mb: 4, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h5" gutterBottom>
          StringedInstrumentFretboard (Generic) - 3D Capo Model
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          The generic instrument fretboard component also supports the 3D capo model through 
          the MinimalThreeInstrument renderer.
        </Typography>
        
        <StringedInstrumentFretboard
          instrument={GUITAR_INSTRUMENTS.find(g => g.variant === 'Electric') || GUITAR_INSTRUMENTS[0]}
          renderMode="3d-webgpu"
          positions={cMajorChord}
          capoFret={capoPosition}
          leftHanded={false}
          options={{
            showStringLabels: true,
            showInlays: true,
            enableOrbitControls: true,
          }}
          title={`Generic Fretboard - ${capoPosition === 0 ? 'No Capo' : `Capo at Fret ${capoPosition}`}`}
        />
      </Paper>

      {/* Technical Information */}
      <Paper sx={{ p: 3, mb: 4, maxWidth: '1200px', mx: 'auto' }}>
        <Typography variant="h6" gutterBottom>
          Technical Implementation
        </Typography>
        
        <Typography variant="body2" paragraph>
          <strong>Model Loading:</strong> The capo model is loaded asynchronously using the GLTFLoader 
          from Three.js. The model is cached to avoid reloading on subsequent uses.
        </Typography>
        
        <Typography variant="body2" paragraph>
          <strong>Positioning:</strong> The capo is positioned dynamically based on the selected fret 
          using the same fret position calculations as the geometric elements.
        </Typography>
        
        <Typography variant="body2" paragraph>
          <strong>Fallback:</strong> If the 3D model fails to load (e.g., file not found), the system 
          automatically falls back to the original geometric capo representation.
        </Typography>
        
        <Typography variant="body2" paragraph>
          <strong>Materials:</strong> The 3D model materials can be customized (color, metalness, roughness) 
          to match the overall fretboard aesthetic.
        </Typography>

        <Box sx={{ mt: 2, p: 2, bgcolor: '#f9f9f9', borderRadius: 1 }}>
          <Typography variant="caption" display="block" gutterBottom>
            <strong>Model Specifications:</strong>
          </Typography>
          <Typography variant="caption" display="block">
            • Format: GLB (binary glTF)<br/>
            • Triangles: 389.5k<br/>
            • Vertices: 195.9k<br/>
            • License: CC Attribution<br/>
            • Scale: Adjustable (typically 0.05-0.1 for guitar fretboards)
          </Typography>
        </Box>
      </Paper>

      {/* Attribution */}
      <Paper sx={{ p: 2, maxWidth: '1200px', mx: 'auto', bgcolor: '#f9f9f9' }}>
        <Typography variant="caption" color="text.secondary">
          "Guitar Capo" by Chad (@cpenfold) is licensed under CC Attribution.<br/>
          Original model: https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7
        </Typography>
      </Paper>
    </Box>
  );
};
