import React, { useState } from 'react';
import { Container, Box, Typography, Grid, Paper, Slider, FormControl, InputLabel, Select, MenuItem } from '@mui/material';
import { ThreeFretboard, ThreeFretboardPosition } from '../components/ThreeFretboard';
import RealisticFretboard from '../components/RealisticFretboard';

type FretboardPosition = ThreeFretboardPosition;

// C Major chord positions
const cMajorChord: FretboardPosition[] = [
  { string: 0, fret: 0, label: 'E', color: '#4dabf7' },
  { string: 1, fret: 1, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 2, fret: 0, label: 'G', color: '#51cf66' },
  { string: 3, fret: 2, label: 'E', color: '#4dabf7' },
  { string: 4, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
  { string: 5, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true },
];

export const CapoTest: React.FC = () => {
  const [capoPosition, setCapoPosition] = useState(0);
  const [guitarModel, setGuitarModel] = useState('electric_fender_strat');

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" gutterBottom>
          🎸 Capo Rendering Test
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          Test capo rendering across different fretboard components and positions.
        </Typography>
      </Box>

      {/* Global Controls */}
      <Paper sx={{ p: 3, mb: 4 }}>
        <Typography variant="h6" gutterBottom>
          Global Capo Controls
        </Typography>
        <Grid container spacing={3} alignItems="center">
          <Grid item xs={12} md={6}>
            <Typography gutterBottom>
              Capo Position: Fret {capoPosition === 0 ? 'None' : capoPosition}
            </Typography>
            <Slider
              value={capoPosition}
              onChange={(_, value) => setCapoPosition(value as number)}
              min={0}
              max={12}
              marks={[
                { value: 0, label: 'No Capo' },
                { value: 3, label: '3' },
                { value: 5, label: '5' },
                { value: 7, label: '7' },
                { value: 12, label: '12' },
              ]}
              valueLabelDisplay="auto"
            />
          </Grid>
          <Grid item xs={12} md={6}>
            <FormControl fullWidth>
              <InputLabel>Guitar Model</InputLabel>
              <Select
                value={guitarModel}
                label="Guitar Model"
                onChange={(e) => setGuitarModel(e.target.value)}
              >
                <MenuItem value="classical_cordoba_c5">Classical - Cordoba C5</MenuItem>
                <MenuItem value="acoustic_martin_d28">Acoustic - Martin D-28</MenuItem>
                <MenuItem value="electric_fender_strat">Electric - Fender Stratocaster</MenuItem>
                <MenuItem value="electric_gibson_les_paul">Electric - Gibson Les Paul</MenuItem>
              </Select>
            </FormControl>
          </Grid>
        </Grid>
      </Paper>

      {/* ThreeFretboard with Capo */}
      <Paper sx={{ p: 3, mb: 4 }}>
        <Typography variant="h5" gutterBottom>
          ThreeFretboard (3D) - Capo Test
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          The capo should appear as a black rubber body with a metallic bar pressing the strings.
        </Typography>
        <ThreeFretboard
          title={`3D Fretboard - Capo at Fret ${capoPosition === 0 ? 'None' : capoPosition}`}
          positions={cMajorChord}
          config={{
            fretCount: 22,
            stringCount: 6,
            guitarModel: guitarModel,
            capoFret: capoPosition,
            leftHanded: false,
            enableOrbitControls: true,
          }}
        />
      </Paper>

      {/* RealisticFretboard with Capo */}
      <Paper sx={{ p: 3, mb: 4 }}>
        <Typography variant="h5" gutterBottom>
          RealisticFretboard (Pixi.js) - Capo Test
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          The capo should appear as a metallic bar with highlights and shadows.
        </Typography>
        <RealisticFretboard
          title={`Realistic Fretboard - Capo at Fret ${capoPosition === 0 ? 'None' : capoPosition}`}
          positions={cMajorChord}
          config={{
            fretCount: 22,
            stringCount: 6,
            guitarModel: guitarModel,
            capoFret: capoPosition,
            leftHanded: false,
            spacingMode: 'realistic',
            width: 1700,
            height: 250,
          }}
        />
      </Paper>

      {/* Capo Position Reference */}
      <Paper sx={{ p: 3 }}>
        <Typography variant="h5" gutterBottom>
          Capo Position Reference
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} md={6}>
            <Typography variant="h6" gutterBottom>
              Common Capo Positions:
            </Typography>
            <ul>
              <li><strong>Fret 1:</strong> Raises pitch by 1 semitone (half step)</li>
              <li><strong>Fret 2:</strong> Raises pitch by 1 tone (whole step)</li>
              <li><strong>Fret 3:</strong> Raises pitch by 1.5 tones (minor third)</li>
              <li><strong>Fret 5:</strong> Raises pitch by 2.5 tones (perfect fourth)</li>
              <li><strong>Fret 7:</strong> Raises pitch by 3.5 tones (perfect fifth)</li>
            </ul>
          </Grid>
          <Grid item xs={12} md={6}>
            <Typography variant="h6" gutterBottom>
              Visual Indicators:
            </Typography>
            <ul>
              <li><strong>3D (ThreeFretboard):</strong>
                <ul>
                  <li>Black rubber/silicone body (top)</li>
                  <li>Metallic bar pressing strings</li>
                  <li>Spring clamp mechanism (back)</li>
                  <li>Positioned between frets</li>
                </ul>
              </li>
              <li><strong>2D (RealisticFretboard):</strong>
                <ul>
                  <li>Metallic gray bar</li>
                  <li>Highlights and shadows for depth</li>
                  <li>Rubber padding visualization</li>
                  <li>Positioned at 70% between frets</li>
                </ul>
              </li>
            </ul>
          </Grid>
        </Grid>
      </Paper>

      {/* Test Instructions */}
      <Box sx={{ mt: 4 }}>
        <Typography variant="h5" gutterBottom>
          Test Instructions:
        </Typography>
        <ol>
          <li>Use the slider above to change the capo position (0-12)</li>
          <li>Observe how the capo renders in both 3D and 2D views</li>
          <li>Try different guitar models to see how capo adapts to neck width</li>
          <li>Verify that:
            <ul>
              <li>Capo appears at the correct fret position</li>
              <li>Capo spans the full width of the neck</li>
              <li>Capo has realistic appearance (metallic bar, rubber body)</li>
              <li>Capo disappears when set to "No Capo" (position 0)</li>
            </ul>
          </li>
          <li>Use orbit controls in 3D view to inspect capo from different angles</li>
        </ol>
      </Box>
    </Container>
  );
};

export default CapoTest;

