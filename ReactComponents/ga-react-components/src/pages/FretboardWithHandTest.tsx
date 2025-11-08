import React, { useState } from 'react';
import { Box, Container, Typography, TextField, Button, Stack, Paper } from '@mui/material';
import { FretboardWithHand } from '../components/FretboardWithHand';

export const FretboardWithHandTest: React.FC = () => {
  const [chordName, setChordName] = useState('G');
  const [currentChord, setCurrentChord] = useState('G');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCurrentChord(chordName);
  };

  const commonChords = ['C', 'D', 'E', 'F', 'G', 'A', 'Am', 'Dm', 'Em', 'Cmaj7', 'Gmaj7', 'G7'];

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" gutterBottom>
          3D Fretboard with Hand Visualization
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          This component combines a 3D fretboard with hand pose visualization showing how to play chords.
          It uses the backend API to fetch chord voicings and displays finger positions in 3D.
        </Typography>
      </Box>

      <Paper elevation={2} sx={{ p: 3, mb: 4 }}>
        <form onSubmit={handleSubmit}>
          <Stack direction="row" spacing={2} alignItems="center">
            <TextField
              label="Chord Name"
              value={chordName}
              onChange={(e) => setChordName(e.target.value)}
              placeholder="e.g., G, Cmaj7, Am"
              size="small"
              sx={{ width: 200 }}
            />
            <Button type="submit" variant="contained">
              Load Chord
            </Button>
          </Stack>
        </form>

        <Box sx={{ mt: 2 }}>
          <Typography variant="body2" color="text.secondary" gutterBottom>
            Quick select:
          </Typography>
          <Stack direction="row" spacing={1} flexWrap="wrap">
            {commonChords.map((chord) => (
              <Button
                key={chord}
                size="small"
                variant={currentChord === chord ? 'contained' : 'outlined'}
                onClick={() => {
                  setChordName(chord);
                  setCurrentChord(chord);
                }}
              >
                {chord}
              </Button>
            ))}
          </Stack>
        </Box>
      </Paper>

      <Paper elevation={2} sx={{ p: 3 }}>
        <FretboardWithHand
          chordName={currentChord}
          apiBaseUrl="https://localhost:7001"
          width={1200}
          height={600}
        />
      </Paper>

      <Box sx={{ mt: 4 }}>
        <Typography variant="h5" gutterBottom>
          Features
        </Typography>
        <Typography variant="body2" component="div">
          <ul>
            <li>✅ Fetches chord voicings from backend API</li>
            <li>✅ 3D fretboard visualization with WebGPU/WebGL</li>
            <li>✅ Hand pose visualization showing finger positions</li>
            <li>✅ Interactive orbit controls (drag to rotate, scroll to zoom)</li>
            <li>✅ Displays chord difficulty level</li>
            <li>🚧 TODO: Load rigged hand model (GLB format)</li>
            <li>🚧 TODO: Animate fingers to chord positions</li>
            <li>🚧 TODO: Use biomechanical IK solver for realistic poses</li>
          </ul>
        </Typography>
      </Box>
    </Container>
  );
};

export default FretboardWithHandTest;

