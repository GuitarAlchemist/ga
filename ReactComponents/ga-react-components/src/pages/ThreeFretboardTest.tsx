import React, { useMemo, useState } from 'react';
import {
  Box,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Button,
  Stack,
  Chip,
} from '@mui/material';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import { ThreeFretboard, ThreeFretboardPosition } from '../components/ThreeFretboard';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

interface ChordShape {
  id: string;
  label: string;
  // High-E to low-E: -1 = muted, 0 = open, n = fret n.
  frets: [number, number, number, number, number, number];
  // Note name per string for the displayed voicing.
  notes: [string, string, string, string, string, string];
}

// Common open + first-position chord voicings. String index 0 = high E, 5 = low E
// (matches ThreeFretboardPosition.string convention used elsewhere on the page).
const CHORD_SHAPES: ChordShape[] = [
  { id: 'C',     label: 'C major',       frets: [ 0,  1,  0,  2,  3, -1], notes: ['E', 'C', 'G', 'E', 'C', '·'] },
  { id: 'Cmaj7', label: 'Cmaj7',         frets: [ 0,  0,  0,  2,  3, -1], notes: ['E', 'B', 'G', 'E', 'C', '·'] },
  { id: 'C7',    label: 'C7',            frets: [ 0,  1,  3,  2,  3, -1], notes: ['E', 'C', 'Bb','E', 'C', '·'] },
  { id: 'D',     label: 'D major',       frets: [ 2,  3,  2,  0, -1, -1], notes: ['F#','D', 'A', 'D', '·', '·'] },
  { id: 'Dm',    label: 'D minor',       frets: [ 1,  3,  2,  0, -1, -1], notes: ['F', 'D', 'A', 'D', '·', '·'] },
  { id: 'D7',    label: 'D7',            frets: [ 2,  1,  2,  0, -1, -1], notes: ['F#','C', 'A', 'D', '·', '·'] },
  { id: 'E',     label: 'E major',       frets: [ 0,  0,  1,  2,  2,  0], notes: ['E', 'B', 'G#','E', 'B', 'E'] },
  { id: 'Em',    label: 'E minor',       frets: [ 0,  0,  0,  2,  2,  0], notes: ['E', 'B', 'G', 'E', 'B', 'E'] },
  { id: 'E7',    label: 'E7',            frets: [ 0,  0,  1,  0,  2,  0], notes: ['E', 'B', 'G#','D', 'B', 'E'] },
  { id: 'F',     label: 'F major (barre)', frets: [ 1,  1,  2,  3,  3,  1], notes: ['F', 'C', 'A', 'F', 'C', 'F'] },
  { id: 'G',     label: 'G major',       frets: [ 3,  3,  0,  0,  2,  3], notes: ['G', 'D', 'G', 'D', 'B', 'G'] },
  { id: 'G7',    label: 'G7',            frets: [ 1,  3,  0,  0,  2,  3], notes: ['F', 'D', 'G', 'D', 'B', 'G'] },
  { id: 'A',     label: 'A major',       frets: [ 0,  2,  2,  2,  0, -1], notes: ['E', 'C#','A', 'E', 'A', '·'] },
  { id: 'Am',    label: 'A minor',       frets: [ 0,  1,  2,  2,  0, -1], notes: ['E', 'C', 'A', 'E', 'A', '·'] },
  { id: 'Am7',   label: 'Am7',           frets: [ 0,  1,  0,  2,  0, -1], notes: ['E', 'C', 'G', 'E', 'A', '·'] },
  { id: 'B7',    label: 'B7',            frets: [ 2,  0,  2,  1,  2, -1], notes: ['F#','D#','A', 'D#','B', '·'] },
];

// String tunings (high E down to low E) → MIDI numbers for open strings.
// String index matches ThreeFretboardPosition.string (0 = high E).
const OPEN_STRING_MIDI = [64, 59, 55, 50, 45, 40];

// Predictable chord-tone palette by interval role.
const NOTE_COLORS: Record<string, string> = {
  root: '#ff6b6b',
  third: '#4dabf7',
  fifth: '#51cf66',
  seventh: '#ffd43b',
  other: '#cc5de8',
};

function classifyNote(note: string, root: string): keyof typeof NOTE_COLORS {
  const NOTE_INDEX: Record<string, number> = {
    'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4,
    'F': 5, 'F#': 6, 'Gb': 6, 'G': 7, 'G#': 8, 'Ab': 8, 'A': 9,
    'A#': 10, 'Bb': 10, 'B': 11,
  };
  const r = NOTE_INDEX[root];
  const n = NOTE_INDEX[note];
  if (r === undefined || n === undefined) return 'other';
  const interval = ((n - r) % 12 + 12) % 12;
  if (interval === 0) return 'root';
  if (interval === 3 || interval === 4) return 'third';
  if (interval === 7) return 'fifth';
  if (interval === 10 || interval === 11) return 'seventh';
  return 'other';
}

function shapeToPositions(shape: ChordShape): ThreeFretboardPosition[] {
  const root = shape.id.replace(/(maj7|m7|7|m)$/, '');
  return shape.frets
    .map((fret, stringIdx) => ({ fret, stringIdx, note: shape.notes[stringIdx] }))
    .filter(({ fret }) => fret >= 0)
    .map(({ fret, stringIdx, note }) => {
      const role = classifyNote(note, root);
      return {
        string: stringIdx,
        fret,
        label: note,
        color: NOTE_COLORS[role],
        emphasized: role === 'root',
      };
    });
}

// Web Audio strum / arpeggiate using simple sine + envelope.
function playStrum(shape: ChordShape, mode: 'strum' | 'arpeggio'): void {
  if (typeof window === 'undefined' || !('AudioContext' in window || 'webkitAudioContext' in window)) {
    console.warn('Web Audio not supported');
    return;
  }
  const Ctx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
  const ctx = new Ctx();
  const masterGain = ctx.createGain();
  masterGain.gain.value = 0.18;
  masterGain.connect(ctx.destination);

  // Iterate low-E to high-E so the strum walks across the strings naturally.
  const ordered = shape.frets.map((fret, stringIdx) => ({ fret, stringIdx })).reverse();
  const stagger = mode === 'strum' ? 0.025 : 0.18;

  ordered.forEach(({ fret, stringIdx }, n) => {
    if (fret < 0) return;
    const midi = OPEN_STRING_MIDI[stringIdx] + fret;
    const freq = 440 * Math.pow(2, (midi - 69) / 12);
    const start = ctx.currentTime + n * stagger;
    const dur = mode === 'strum' ? 1.2 : 0.6;

    const osc = ctx.createOscillator();
    osc.type = 'triangle';
    osc.frequency.value = freq;

    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(1.0, start + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + dur);

    osc.connect(gain);
    gain.connect(masterGain);
    osc.start(start);
    osc.stop(start + dur);
  });

  // Auto-close so we don't leak AudioContexts.
  setTimeout(() => ctx.close(), (ordered.length * stagger + 1.5) * 1000);
}

export const ThreeFretboardTest: React.FC = () => {
  const [chordId, setChordId] = useState<string>('C');
  const shape = useMemo(() => CHORD_SHAPES.find((c) => c.id === chordId) ?? CHORD_SHAPES[0], [chordId]);
  const positions = useMemo(() => shapeToPositions(shape), [shape]);

  return (
    <DemoErrorBoundary demoName="Three Fretboard">
      <Box sx={{ width: '100vw', minHeight: '100vh', p: 2 }}>
        <Box sx={{ mb: 3, maxWidth: '1200px', mx: 'auto' }}>
          <Typography variant="h3" gutterBottom>
            3D Fretboard
          </Typography>
          <Typography variant="body1" color="text.secondary" paragraph>
            Pick a chord, watch the voicing light up on the 3D neck, and strum it. WebGPU when your browser supports it, WebGL otherwise.
          </Typography>

          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ md: 'center' }} sx={{ mb: 2, flexWrap: 'wrap' }}>
            <FormControl sx={{ minWidth: 220 }}>
              <InputLabel id="chord-picker-label">Chord</InputLabel>
              <Select
                labelId="chord-picker-label"
                value={chordId}
                label="Chord"
                onChange={(e) => setChordId(String(e.target.value))}
              >
                {CHORD_SHAPES.map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <Button
              variant="contained"
              startIcon={<VolumeUpIcon />}
              onClick={() => playStrum(shape, 'strum')}
            >
              Strum
            </Button>
            <Button
              variant="outlined"
              startIcon={<VolumeUpIcon />}
              onClick={() => playStrum(shape, 'arpeggio')}
            >
              Arpeggiate
            </Button>

            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
              <Chip size="small" sx={{ bgcolor: NOTE_COLORS.root, color: '#fff' }} label="root" />
              <Chip size="small" sx={{ bgcolor: NOTE_COLORS.third, color: '#fff' }} label="3rd" />
              <Chip size="small" sx={{ bgcolor: NOTE_COLORS.fifth, color: '#fff' }} label="5th" />
              <Chip size="small" sx={{ bgcolor: NOTE_COLORS.seventh, color: '#000' }} label="7th" />
            </Stack>
          </Stack>
        </Box>

        <ThreeFretboard
          title={`3D Fretboard — ${shape.label}`}
          positions={positions}
          config={{
            fretCount: 22,
            stringCount: 6,
            guitarModel: 'electric_fender_strat',
            capoFret: 0,
            leftHanded: false,
            enableOrbitControls: true,
          }}
        />
      </Box>
    </DemoErrorBoundary>
  );
};

export default ThreeFretboardTest;
