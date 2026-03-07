import React, { useEffect, useMemo, useState } from 'react';
import { Box, Chip, Paper, Tab, Tabs, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useAtomValue } from 'jotai';
import { selectedChordAtom } from '../store/musicSelectionAtoms';
import { chatConfigAtom } from '../store/chatAtoms';
import { fetchVoicingsForChord } from '../services/musicService';
import type { VoicingWithAnalysis } from '../types/music';
import GuitarFretboard, { type FretboardPosition } from 'ga-react-components/src/components/GuitarFretboard';

const SmartVoicingDisplay: React.FC = () => {
  const theme = useTheme();
  const selectedChord = useAtomValue(selectedChordAtom);
  const { apiEndpoint } = useAtomValue(chatConfigAtom);

  const [voicings, setVoicings] = useState<VoicingWithAnalysis[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tabValue, setTabValue] = useState(0);

  useEffect(() => {
    if (!apiEndpoint || !selectedChord) {
      setVoicings([]);
      return;
    }

    let isMounted = true;
    const controller = new AbortController();

    const load = async () => {
      setLoading(true);
      setError(null);

      try {
        const data = await fetchVoicingsForChord(
          apiEndpoint,
          selectedChord.contextualName,
          undefined, // maxDifficulty
          controller.signal
        );

        if (!isMounted) return;
        setVoicings(data);
      } catch (err) {
        if (!isMounted) return;
        setError(err instanceof Error ? err.message : 'Failed to load voicings');
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    load();

    return () => {
      isMounted = false;
      controller.abort();
    };
  }, [apiEndpoint, selectedChord]);

  const groupedVoicings = useMemo(() => {
    return voicings.reduce<Record<string, VoicingWithAnalysis[]>>((acc, v) => {
      const difficulty = v.difficulty || 'Unknown';
      if (!acc[difficulty]) acc[difficulty] = [];
      acc[difficulty].push(v);
      return acc;
    }, {});
  }, [voicings]);

  const difficultyLevels = ['Beginner', 'Intermediate', 'Advanced', 'Expert'];
  const availableLevels = difficultyLevels.filter(level => groupedVoicings[level]?.length > 0);

  const handleTabChange = (_: React.SyntheticEvent, newValue: number) => {
    setTabValue(newValue);
  };

  if (!selectedChord) {
    return (
      <Paper sx={{ p: 3, textAlign: 'center', color: 'text.secondary' }}>
        Select a chord from the palette to see intelligent voicings.
      </Paper>
    );
  }

  return (
    <Box sx={{ mt: 3 }}>
      <Typography variant="h5" gutterBottom sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        Smart Voicings for <strong>{selectedChord.contextualName}</strong>
        {selectedChord.romanNumeral && <Chip label={selectedChord.romanNumeral} size="small" />}
      </Typography>

      {loading && <Typography>Analyzing guitar fretboard...</Typography>}
      {error && <Typography color="error">{error}</Typography>}

      {!loading && !error && (
        <>
          <Tabs value={tabValue} onChange={handleTabChange} sx={{ mb: 2 }}>
            {availableLevels.map((level, index) => (
              <Tab key={level} label={`${level} (${groupedVoicings[level].length})`} />
            ))}
          </Tabs>

          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 3 }}>
            {availableLevels.length > 0 && groupedVoicings[availableLevels[tabValue]]?.map((voicing, idx) => (
              <VoicingCard key={idx} voicing={voicing} />
            ))}
          </Box>

          {!loading && availableLevels.length === 0 && (
            <Typography sx={{ py: 4, textAlign: 'center', color: 'text.secondary' }}>
              No voicings found matching the current criteria.
            </Typography>
          )}
        </>
      )}
    </Box>
  );
};

const VoicingCard: React.FC<{ voicing: VoicingWithAnalysis }> = ({ voicing }) => {
  const minFret = Math.max(0, Math.min(...voicing.frets.filter(f => f > 0)) - 1);
  const maxFret = Math.max(...voicing.frets);
  const span = Math.max(4, maxFret - minFret + 1);

  const positions: FretboardPosition[] = voicing.frets
    .map((fret, stringIndex) => ({
      string: 5 - stringIndex, // Flip for high-E on top if needed, but GuitarFretboard seems 0=highE
      fret: fret,
    }))
    .filter(p => p.fret >= 0) as FretboardPosition[];

  // Note: GuitarFretboard 0 is high E, 5 is low E.
  // Our frets array is usually Low to High? Let's check logic.
  // In VoicingFilterService: voicing.Positions.Select(p => p is Position.Played played ? played.Location.Fret.Value : -1).ToArray()
  // PositionLocation string index is 1-based. 1 = lowest string (usually).
  // Let's assume frets[0] is Low E.
  const adjustedPositions: FretboardPosition[] = voicing.frets.map((fret, idx) => ({
    string: 5 - idx, // Map index 0 (Low E) to string 5
    fret: fret
  })).filter(p => p.fret >= 0);

  return (
    <Paper elevation={3} sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="subtitle1" fontWeight="bold">
          {voicing.handPosition}
        </Typography>
        <Chip 
          label={voicing.difficulty} 
          size="small" 
          color={voicing.difficulty === 'Beginner' ? 'success' : 'primary'} 
        />
      </Box>
      
      <Box sx={{ height: 120 }}>
        <GuitarFretboard 
          config={{
            fretCount: span,
            startFret: minFret,
            width: 260,
            height: 100,
            showStringLabels: false,
            showFretNumbers: true
          }}
          positions={adjustedPositions}
        />
      </Box>

      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mt: 1 }}>
        {voicing.semanticTags.map(tag => (
          <Chip key={tag} label={tag} size="small" variant="outlined" sx={{ fontSize: '0.7rem' }} />
        ))}
      </Box>
    </Paper>
  );
};

export default SmartVoicingDisplay;
