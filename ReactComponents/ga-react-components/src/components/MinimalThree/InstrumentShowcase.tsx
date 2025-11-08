// @ts-nocheck
/**
 * Instrument Showcase Component
 * 
 * Demonstrates the MinimalThreeInstrument component with various instruments
 * from the YAML database. Shows how a single component can handle all instrument types.
 */

import React, { useState, useEffect } from 'react';
import {
  Box,
  Stack,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormControlLabel,
  Switch,
  Paper,
  Chip,
  Grid,
  Alert,
  CircularProgress,
} from '@mui/material';
import type { InstrumentConfig, FretboardPosition } from '../../types/InstrumentConfig';
import { loadInstruments, getInstrument, getAllInstrumentFamilies } from '../../utils/instrumentLoader';
import { MinimalThreeInstrument } from './MinimalThreeInstrument';
import { MusicTheorySelector, MusicTheoryContext } from '../MusicTheorySelector';

// Sample chord positions for demonstration
const SAMPLE_POSITIONS: FretboardPosition[] = [
  { string: 0, fret: 0, label: 'E', color: '#4DABF7' },
  { string: 1, fret: 2, label: 'B', color: '#4DABF7' },
  { string: 2, fret: 2, label: 'G', color: '#4DABF7' },
  { string: 3, fret: 1, label: 'D', color: '#4DABF7' },
  { string: 4, fret: 0, label: 'A', color: '#4DABF7' },
  { string: 5, fret: 0, label: 'E', color: '#4DABF7' },
];

export const InstrumentShowcase: React.FC = () => {
  const [instruments, setInstruments] = useState<Map<string, InstrumentConfig[]>>(new Map());
  const [selectedFamily, setSelectedFamily] = useState<string>('Guitar');
  const [selectedVariant, setSelectedVariant] = useState<string>('Standard');
  const [currentInstrument, setCurrentInstrument] = useState<InstrumentConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  
  // Display options
  const [renderMode, setRenderMode] = useState<'3d-webgl' | '3d-webgpu'>('3d-webgl');
  const viewMode = 'fretboard'; // Always show fretboard only (no headstock)
  const [showPositions, setShowPositions] = useState(true);
  const [capoFret, setCapoFret] = useState(0);
  const [leftHanded, setLeftHanded] = useState(false);

  // Music theory context
  const [musicTheoryContext, setMusicTheoryContext] = useState<MusicTheoryContext>({
    tonality: 'atonal'
  });

  // Load instruments on mount
  useEffect(() => {
    const loadInstrumentData = async () => {
      try {
        setLoading(true);
        setError(null);
        const instrumentMap = await loadInstruments();
        setInstruments(instrumentMap);
        
        // Set default instrument
        const guitar = await getInstrument('Guitar', 'Standard');
        if (guitar) {
          setCurrentInstrument(guitar);
        }
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'Failed to load instruments';
        setError(errorMessage);
        console.error('Failed to load instruments:', err);
      } finally {
        setLoading(false);
      }
    };

    loadInstrumentData();
  }, []);

  // Update current instrument when selection changes
  useEffect(() => {
    const updateInstrument = async () => {
      if (selectedFamily && selectedVariant) {
        try {
          setError(null);
          const instrument = await getInstrument(selectedFamily, selectedVariant);
          if (instrument) {
            setCurrentInstrument(instrument);

            // Reset capo if it exceeds the new instrument's fret count
            if (capoFret > instrument.fretCount) {
              setCapoFret(0);
            }
          } else {
            setError(`Instrument not found: ${selectedFamily} - ${selectedVariant}`);
          }
        } catch (err) {
          const errorMessage = err instanceof Error ? err.message : 'Failed to load instrument';
          setError(errorMessage);
          console.error('Failed to load instrument:', err);
        }
      }
    };

    updateInstrument();
  }, [selectedFamily, selectedVariant]);

  // Get available families
  const families = Array.from(instruments.keys()).sort();
  
  // Get available variants for selected family
  const variants = instruments.get(selectedFamily)?.map(i => i.variant) || [];

  // Generate appropriate positions for the current instrument
  const getPositionsForInstrument = (instrument: InstrumentConfig): FretboardPosition[] => {
    if (!showPositions) return [];

    // If music theory context is tonal and has notes, generate positions for those notes
    if (musicTheoryContext.tonality === 'tonal' && musicTheoryContext.notes && musicTheoryContext.notes.length > 0) {
      return generatePositionsForNotes(musicTheoryContext.notes, instrument.tuning);
    }

    // Otherwise, show a simple chord pattern
    const stringCount = instrument.tuning.length;
    const positions: FretboardPosition[] = [];

    // Create a simple chord pattern that works for any string count
    for (let i = 0; i < Math.min(stringCount, 6); i++) {
      positions.push({
        string: i,
        fret: i % 3, // Simple pattern: 0, 1, 2, 0, 1, 2
        label: instrument.tuning[i],
        color: '#4DABF7',
        emphasized: i === 0, // Emphasize first string
      });
    }

    return positions;
  };

  // Helper function to generate fretboard positions for a set of notes
  const generatePositionsForNotes = (notes: string[], tuning: string[]): FretboardPosition[] => {
    const positions: FretboardPosition[] = [];
    const chromaticScale = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];

    // Normalize note names (remove flats, convert to sharps)
    const normalizeNote = (note: string): string => {
      const flatToSharp: { [key: string]: string } = {
        'Db': 'C#', 'Eb': 'D#', 'Gb': 'F#', 'Ab': 'G#', 'Bb': 'A#'
      };
      return flatToSharp[note] || note;
    };

    const normalizedNotes = notes.map(normalizeNote);

    // For each string
    tuning.forEach((openNote, stringIndex) => {
      const normalizedOpenNote = normalizeNote(openNote);
      const openNoteIndex = chromaticScale.indexOf(normalizedOpenNote);

      if (openNoteIndex === -1) return; // Skip if note not found

      // Check each fret (0-12 for visibility)
      for (let fret = 0; fret <= 12; fret++) {
        const noteIndex = (openNoteIndex + fret) % 12;
        const noteName = chromaticScale[noteIndex];

        // If this note is in our scale
        if (normalizedNotes.includes(noteName)) {
          const isRoot = noteName === normalizedNotes[0];
          positions.push({
            string: stringIndex,
            fret: fret,
            label: noteName,
            color: isRoot ? '#FF6B6B' : '#4DABF7', // Root = red, others = blue
            emphasized: isRoot,
          });
        }
      }
    });

    return positions;
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={400}>
        <Stack spacing={2} alignItems="center">
          <CircularProgress />
          <Typography>Loading instruments...</Typography>
        </Stack>
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error">
        Failed to load instruments: {error}
      </Alert>
    );
  }

  if (!currentInstrument) {
    return (
      <Alert severity="warning">
        No instrument selected
      </Alert>
    );
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h4" gutterBottom>
        Minimal Three.js Instrument Showcase
      </Typography>
      
      <Typography variant="body1" color="text.secondary">
        This demonstrates a single ThreeJS + WebGPU component that can render ANY stringed instrument 
        from the YAML database. Select different instruments to see how the component adapts automatically.
      </Typography>

      {/* Controls */}
      <Paper elevation={1} sx={{ p: 3 }}>
        <Grid container spacing={3}>
          {/* Instrument Selection */}
          <Grid item xs={12} md={6}>
            <Stack spacing={2}>
              <Typography variant="h6">Instrument Selection</Typography>
              
              <FormControl fullWidth>
                <InputLabel>Instrument</InputLabel>
                <Select
                  value={selectedFamily}
                  label="Instrument"
                  onChange={(e) => {
                    setSelectedFamily(e.target.value);
                    // Reset to first variant when family changes
                    const newVariants = instruments.get(e.target.value);
                    if (newVariants && newVariants.length > 0) {
                      setSelectedVariant(newVariants[0].variant);
                    }
                  }}
                >
                  {families.map((family) => {
                    const familyInstruments = instruments.get(family);
                    const stringCount = familyInstruments?.[0]?.tuning.length || 0;
                    return (
                      <MenuItem key={family} value={family}>
                        {family} ({stringCount} strings)
                      </MenuItem>
                    );
                  })}
                </Select>
              </FormControl>
            </Stack>
          </Grid>

          {/* Display Options */}
          <Grid item xs={12} md={6}>
            <Stack spacing={2}>
              <Typography variant="h6">Display Options</Typography>
              
              <FormControl fullWidth>
                <InputLabel>Render Mode</InputLabel>
                <Select
                  value={renderMode}
                  label="Render Mode"
                  onChange={(e) => setRenderMode(e.target.value as any)}
                >
                  <MenuItem value="3d-webgpu">WebGPU (Preferred)</MenuItem>
                  <MenuItem value="3d-webgl">WebGL (Fallback)</MenuItem>
                </Select>
              </FormControl>



              <FormControl fullWidth>
                <InputLabel>Capo Position</InputLabel>
                <Select
                  value={capoFret}
                  label="Capo Position"
                  onChange={(e) => setCapoFret(Number(e.target.value))}
                >
                  <MenuItem value={0}>No Capo</MenuItem>
                  {Array.from({ length: Math.min(12, currentInstrument.fretCount) }, (_, i) => i + 1).map((fret) => (
                    <MenuItem key={fret} value={fret}>
                      Fret {fret}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <Stack direction="row" spacing={2}>
                <FormControlLabel
                  control={
                    <Switch
                      checked={showPositions}
                      onChange={(e) => setShowPositions(e.target.checked)}
                    />
                  }
                  label="Show Positions"
                />
                <FormControlLabel
                  control={
                    <Switch
                      checked={leftHanded}
                      onChange={(e) => setLeftHanded(e.target.checked)}
                    />
                  }
                  label="Left-Handed"
                />
              </Stack>
            </Stack>
          </Grid>
        </Grid>
      </Paper>

      {/* Music Theory Selector */}
      <Paper elevation={1} sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          🎵 Music Theory Context
        </Typography>
        <MusicTheorySelector
          context={musicTheoryContext}
          onContextChange={setMusicTheoryContext}
          apiBaseUrl="http://localhost:7001"
        />
      </Paper>

      {/* Instrument Info */}
      <Paper elevation={1} sx={{ p: 2 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <Typography variant="h6">
            {currentInstrument.displayName}
          </Typography>
          
          <Chip 
            label={`${currentInstrument.tuning.length} strings`} 
            color="primary" 
            variant="outlined"
          />
          
          <Chip 
            label={`${currentInstrument.fretCount} frets`} 
            color="secondary" 
            variant="outlined"
          />
          
          <Chip 
            label={currentInstrument.bodyStyle} 
            variant="outlined"
          />
          
          <Chip 
            label={`${currentInstrument.scaleLength}mm scale`} 
            variant="outlined"
          />
        </Stack>

      </Paper>

      {/* 3D Instrument Renderer */}
      <Paper elevation={2} sx={{ p: 2, width: '100%' }}>
        <Box sx={{ width: '100%', height: 700, bgcolor: '#1a1a1a', borderRadius: 1, display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
          <MinimalThreeInstrument
            instrument={currentInstrument}
            positions={getPositionsForInstrument(currentInstrument)}
            renderMode={renderMode}
            viewMode={viewMode}
            capoFret={capoFret}
            leftHanded={leftHanded}
            showLabels={true}
            showInlays={true}
            showTuningPegs={viewMode !== 'fretboard'}
            width={Math.min(typeof window !== 'undefined' ? window.innerWidth - 100 : 1800, 2400)}
            height={700}
            enableOrbitControls={true}
            onPositionClick={(string, fret) => {
              console.log(`Clicked string ${string}, fret ${fret}`);
            }}
            onPositionHover={(string, fret) => {
              if (string !== null && fret !== null) {
                console.log(`Hovering string ${string}, fret ${fret}`);
              }
            }}
            title={`${currentInstrument.family} - ${currentInstrument.variant}`}
          />
        </Box>
      </Paper>

      {/* Statistics */}
      <Paper elevation={1} sx={{ p: 2, bgcolor: 'background.paper' }}>
        <Typography variant="h6" gutterBottom>
          Database Statistics
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={6} sm={3}>
            <Typography variant="h4" color="primary">
              {families.length}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Instrument Families
            </Typography>
          </Grid>
          <Grid item xs={6} sm={3}>
            <Typography variant="h4" color="secondary">
              {Array.from(instruments.values()).reduce((sum, variants) => sum + variants.length, 0)}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Total Variants
            </Typography>
          </Grid>
          <Grid item xs={6} sm={3}>
            <Typography variant="h4" color="success.main">
              {Math.min(...Array.from(instruments.values()).flat().map(i => i.tuning.length))}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Min Strings
            </Typography>
          </Grid>
          <Grid item xs={6} sm={3}>
            <Typography variant="h4" color="warning.main">
              {Math.max(...Array.from(instruments.values()).flat().map(i => i.tuning.length))}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Max Strings
            </Typography>
          </Grid>
        </Grid>
      </Paper>
    </Stack>
  );
};

export default InstrumentShowcase;
