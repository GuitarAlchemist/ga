import React from 'react';
import { Breadcrumbs, Link, Typography, Box, Paper, Chip } from '@mui/material';
import { useAtom, useAtomValue } from 'jotai';
import { 
  navigationLevelAtom, 
  selectedKeyAtom, 
  selectedScaleAtom, 
  selectedModeAtom,
  type NavigationLevel 
} from '../store/atoms';
import KeySelector from './KeySelector';
import { NavigateNext } from '@mui/icons-material';

const HierarchicalNavigation: React.FC = () => {
  const [level, setLevel] = useAtom(navigationLevelAtom);
  const selectedKey = useAtomValue(selectedKeyAtom);
  const selectedScale = useAtomValue(selectedScaleAtom);
  const selectedMode = useAtomValue(selectedModeAtom);

  const handleLevelClick = (newLevel: NavigationLevel) => (event: React.MouseEvent) => {
    event.preventDefault();
    setLevel(newLevel);
  };

  return (
    <Box sx={{ mb: 3 }}>
      <Paper elevation={1} sx={{ p: 1.5, mb: 2, bgcolor: 'background.paper', borderRadius: 2 }}>
        <Breadcrumbs 
          separator={<NavigateNext fontSize="small" />} 
          aria-label="musical-context-breadcrumb"
        >
          <Link
            underline="hover"
            color={level === 'key' ? 'primary' : 'inherit'}
            href="#"
            onClick={handleLevelClick('key')}
            sx={{ display: 'flex', alignItems: 'center', fontWeight: level === 'key' ? 700 : 400 }}
          >
            KEY: {selectedKey.root} {selectedKey.mode}
          </Link>
          
          {level !== 'key' || selectedScale ? (
            <Link
              underline="hover"
              color={level === 'scale' ? 'primary' : 'inherit'}
              href="#"
              onClick={handleLevelClick('scale')}
              sx={{ display: 'flex', alignItems: 'center', fontWeight: level === 'scale' ? 700 : 400 }}
            >
              SCALE: {selectedScale || (selectedKey.mode === 'Major' ? 'Major' : 'Minor')}
            </Link>
          ) : null}

          {level === 'mode' || selectedMode ? (
            <Typography
              color={level === 'mode' ? 'primary' : 'text.primary'}
              sx={{ fontWeight: level === 'mode' ? 700 : 400 }}
            >
              MODE: {selectedMode || 'Ionian'}
            </Typography>
          ) : null}
        </Breadcrumbs>
      </Paper>

      <Box>
        {level === 'key' && <KeySelector />}
        {level === 'scale' && <ScaleSelector />}
        {level === 'mode' && <ModeSelector />}
      </Box>
    </Box>
  );
};

const ScaleSelector: React.FC = () => {
  const [selectedScale, setSelectedScale] = useAtom(selectedScaleAtom);
  const setLevel = useSetAtom(navigationLevelAtom);
  
  const scales = ['Major', 'Natural Minor', 'Harmonic Minor', 'Melodic Minor', 'Pentatonic Major', 'Pentatonic Minor', 'Blues'];

  const handleSelect = (scale: string) => {
    setSelectedScale(scale);
    setLevel('mode');
  };

  return (
    <Paper elevation={2} sx={{ p: 3 }}>
      <Typography variant="h6" gutterBottom>Select Scale Type</Typography>
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
        {scales.map(scale => (
          <Chip 
            key={scale} 
            label={scale} 
            onClick={() => handleSelect(scale)}
            color={selectedScale === scale ? 'primary' : 'default'}
            variant={selectedScale === scale ? 'filled' : 'outlined'}
            clickable
          />
        ))}
      </Box>
    </Paper>
  );
};

const ModeSelector: React.FC = () => {
  const [selectedMode, setSelectedMode] = useAtom(selectedModeAtom);
  const modes = ['Ionian', 'Dorian', 'Phrygian', 'Lydian', 'Mixolydian', 'Aeolian', 'Locrian'];

  return (
    <Paper elevation={2} sx={{ p: 3 }}>
      <Typography variant="h6" gutterBottom>Select Mode</Typography>
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
        {modes.map(mode => (
          <Chip 
            key={mode} 
            label={mode} 
            onClick={() => setSelectedMode(mode)}
            color={selectedMode === mode ? 'primary' : 'default'}
            variant={selectedMode === mode ? 'filled' : 'outlined'}
            clickable
          />
        ))}
      </Box>
    </Paper>
  );
};

// Need to import useSetAtom
import { useSetAtom } from 'jotai';

export default HierarchicalNavigation;
