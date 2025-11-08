// @ts-nocheck
import React, { useState, useEffect } from 'react';
import {
  Box,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  ToggleButtonGroup,
  ToggleButton,
  Paper,
  Typography,
  Chip,
  Stack,
  SelectChangeEvent,
  CircularProgress,
  Alert
} from '@mui/material';
import MusicNoteIcon from '@mui/icons-material/MusicNote';
import PianoIcon from '@mui/icons-material/Piano';

/**
 * Music theory context for fretboard visualization
 */
export interface MusicTheoryContext {
  tonality: 'atonal' | 'tonal';
  key?: string;           // e.g., "C Major", "A Minor"
  mode?: string;          // e.g., "Ionian", "Dorian"
  scaleDegree?: number;   // 1-7 (I-VII)
  notes?: string[];       // Notes in the selected key/mode
}

/**
 * Props for MusicTheorySelector component
 */
export interface MusicTheorySelectorProps {
  /** Current music theory context */
  context?: MusicTheoryContext;
  /** Callback when context changes */
  onContextChange?: (context: MusicTheoryContext) => void;
  /** API base URL (default: http://localhost:7001) */
  apiBaseUrl?: string;
  /** Show compact version */
  compact?: boolean;
}

interface KeyData {
  name: string;
  root: string;
  mode: string;
  keySignature: number;
  accidentalKind: string;
  notes: string[];
}

interface ModeData {
  name: string;
  degree: number;
  isMinor: boolean;
  intervals: string[];
  characteristicNotes: string[];
}

interface ScaleDegreeData {
  degree: number;
  romanNumeral: string;
  name: string;
}

/**
 * Music Theory Selector Component
 * 
 * Allows users to select:
 * - Tonality (Atonal vs Tonal)
 * - Key (when tonal)
 * - Mode (when tonal)
 * - Scale Degree (when tonal)
 * 
 * Fetches data from the GaApi backend.
 */
export const MusicTheorySelector: React.FC<MusicTheorySelectorProps> = ({
  context: initialContext,
  onContextChange,
  apiBaseUrl = 'http://localhost:7001',
  compact = false
}) => {
  // State
  const [context, setContext] = useState<MusicTheoryContext>(
    initialContext || { tonality: 'atonal' }
  );
  const [keys, setKeys] = useState<KeyData[]>([]);
  const [modes, setModes] = useState<ModeData[]>([]);
  const [scaleDegrees, setScaleDegrees] = useState<ScaleDegreeData[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch music theory data from backend
  useEffect(() => {
    const fetchMusicTheoryData = async () => {
      setLoading(true);
      setError(null);

      try {
        // Fetch keys
        const keysResponse = await fetch(`${apiBaseUrl}/api/music-theory/keys`);
        if (!keysResponse.ok) throw new Error('Failed to fetch keys');
        const keysData = await keysResponse.json();
        setKeys(keysData.data || []);

        // Fetch modes
        const modesResponse = await fetch(`${apiBaseUrl}/api/music-theory/modes`);
        if (!modesResponse.ok) throw new Error('Failed to fetch modes');
        const modesData = await modesResponse.json();
        setModes(modesData.data || []);

        // Fetch scale degrees
        const degreesResponse = await fetch(`${apiBaseUrl}/api/music-theory/scale-degrees`);
        if (!degreesResponse.ok) throw new Error('Failed to fetch scale degrees');
        const degreesData = await degreesResponse.json();
        setScaleDegrees(degreesData.data || []);
      } catch (err) {
        console.error('Error fetching music theory data:', err);
        setError(err instanceof Error ? err.message : 'Unknown error');
      } finally {
        setLoading(false);
      }
    };

    fetchMusicTheoryData();
  }, [apiBaseUrl]);

  // Handle tonality change
  const handleTonalityChange = (_event: React.MouseEvent<HTMLElement>, newTonality: 'atonal' | 'tonal' | null) => {
    if (newTonality !== null) {
      const newContext: MusicTheoryContext = {
        tonality: newTonality,
        ...(newTonality === 'atonal' ? {} : { key: 'Key of C', mode: 'Ionian' })
      };
      setContext(newContext);
      onContextChange?.(newContext);
    }
  };

  // Handle key change
  const handleKeyChange = async (event: SelectChangeEvent<string>) => {
    const keyName = event.target.value;
    
    try {
      // Fetch notes for the selected key
      const response = await fetch(`${apiBaseUrl}/api/music-theory/keys/${encodeURIComponent(keyName)}/notes`);
      if (!response.ok) throw new Error('Failed to fetch key notes');
      const data = await response.json();
      
      const newContext: MusicTheoryContext = {
        ...context,
        key: keyName,
        notes: data.data.notes
      };
      setContext(newContext);
      onContextChange?.(newContext);
    } catch (err) {
      console.error('Error fetching key notes:', err);
    }
  };

  // Handle mode change
  const handleModeChange = (event: SelectChangeEvent<string>) => {
    const modeName = event.target.value;
    const newContext: MusicTheoryContext = {
      ...context,
      mode: modeName
    };
    setContext(newContext);
    onContextChange?.(newContext);
  };

  // Handle scale degree change
  const handleScaleDegreeChange = (event: SelectChangeEvent<number>) => {
    const degree = Number(event.target.value);
    const newContext: MusicTheoryContext = {
      ...context,
      scaleDegree: degree
    };
    setContext(newContext);
    onContextChange?.(newContext);
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" p={3}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error" sx={{ m: 2 }}>
        Error loading music theory data: {error}
      </Alert>
    );
  }

  return (
    <Paper elevation={compact ? 0 : 2} sx={{ p: compact ? 1 : 3, mb: compact ? 1 : 3 }}>
      {!compact && (
        <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <MusicNoteIcon />
          Music Theory Context
        </Typography>
      )}

      <Stack spacing={2}>
        {/* Tonality Toggle */}
        <Box>
          <Typography variant="caption" display="block" gutterBottom>
            Tonality
          </Typography>
          <ToggleButtonGroup
            value={context.tonality}
            exclusive
            onChange={handleTonalityChange}
            aria-label="tonality"
            size={compact ? 'small' : 'medium'}
            fullWidth={compact}
          >
            <ToggleButton value="atonal" aria-label="atonal">
              Atonal
            </ToggleButton>
            <ToggleButton value="tonal" aria-label="tonal">
              Tonal
            </ToggleButton>
          </ToggleButtonGroup>
        </Box>

        {/* Tonal Controls (only shown when tonality is 'tonal') */}
        {context.tonality === 'tonal' && (
          <>
            {/* Key Selector */}
            <FormControl fullWidth size={compact ? 'small' : 'medium'}>
              <InputLabel id="key-selector-label">Key</InputLabel>
              <Select
                labelId="key-selector-label"
                id="key-selector"
                value={context.key || ''}
                label="Key"
                onChange={handleKeyChange}
              >
                <MenuItem value="" disabled>
                  <em>Select a key</em>
                </MenuItem>
                {keys.map((key) => (
                  <MenuItem key={key.name} value={key.name}>
                    {key.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {/* Mode Selector */}
            <FormControl fullWidth size={compact ? 'small' : 'medium'}>
              <InputLabel id="mode-selector-label">Mode</InputLabel>
              <Select
                labelId="mode-selector-label"
                id="mode-selector"
                value={context.mode || ''}
                label="Mode"
                onChange={handleModeChange}
              >
                <MenuItem value="" disabled>
                  <em>Select a mode</em>
                </MenuItem>
                {modes.map((mode) => (
                  <MenuItem key={mode.name} value={mode.name}>
                    {mode.name} {mode.isMinor && '(minor)'}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {/* Scale Degree Selector (Optional) */}
            <FormControl fullWidth size={compact ? 'small' : 'medium'}>
              <InputLabel id="degree-selector-label">Scale Degree (Optional)</InputLabel>
              <Select
                labelId="degree-selector-label"
                id="degree-selector"
                value={context.scaleDegree || ''}
                label="Scale Degree (Optional)"
                onChange={handleScaleDegreeChange}
              >
                <MenuItem value="">
                  <em>None</em>
                </MenuItem>
                {scaleDegrees.map((degree) => (
                  <MenuItem key={degree.degree} value={degree.degree}>
                    {degree.romanNumeral} - {degree.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {/* Display selected notes */}
            {context.notes && context.notes.length > 0 && (
              <Box>
                <Typography variant="caption" display="block" gutterBottom>
                  Notes in {context.key}:
                </Typography>
                <Stack direction="row" spacing={0.5} flexWrap="wrap">
                  {context.notes.map((note, index) => (
                    <Chip
                      key={index}
                      label={note}
                      size="small"
                      icon={<PianoIcon />}
                      color="primary"
                      variant="outlined"
                    />
                  ))}
                </Stack>
              </Box>
            )}
          </>
        )}
      </Stack>
    </Paper>
  );
};

export default MusicTheorySelector;
