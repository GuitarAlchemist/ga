import React, { useMemo } from 'react';
import { Paper, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import { useAtom } from 'jotai';
import { selectedKeyAtom, type KeyMode } from '../store/atoms';

interface KeySelectorProps {
  useFlats?: boolean;
}

const SHARP_NOTES_BASE = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
const FLAT_NOTES_BASE = ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B'];

const KeySelector: React.FC<KeySelectorProps> = ({ useFlats = false }) => {
  const [selectedKey, setSelectedKey] = useAtom(selectedKeyAtom);

  const noteRoots = useMemo<string[]>(() => {
    const source = useFlats ? FLAT_NOTES_BASE : SHARP_NOTES_BASE;
    return Array.from(source);
  }, [useFlats]);

  const currentRoot = useMemo(() => String(selectedKey.root), [selectedKey.root]);

  const rootOptions = useMemo<React.ReactNode[]>(() => {
    const options: React.ReactNode[] = [];
    for (const note of noteRoots) {
      options.push(
        <option key={note} value={note}>
          {note}
        </option>,
      );
    }
    return options;
  }, [noteRoots]);

  const handleRootChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    setSelectedKey({
      ...selectedKey,
      root: event.target.value,
    });
  };

  const handleModeChange = (_event: React.MouseEvent<HTMLElement>, newMode: KeyMode | null) => {
    if (newMode !== null) {
      setSelectedKey({
        ...selectedKey,
        mode: newMode,
      });
    }
  };

  return (
    <Paper elevation={2} sx={{ p: 3, mb: 3 }}>
      <Typography variant="h6" gutterBottom>
        Select Musical Key
      </Typography>

      <div style={{ display: 'flex', gap: '16px', alignItems: 'center', flexWrap: 'wrap' }}>
        <div style={{ minWidth: 160, display: 'flex', flexDirection: 'column' }}>
          <Typography variant="caption" color="text.secondary" sx={{ mb: 0.5 }}>
            Root Note
          </Typography>
          <select
            value={currentRoot}
            onChange={handleRootChange}
            style={{
              width: '100%',
              padding: '8px',
              borderRadius: 8,
              border: '1px solid rgba(255,255,255,0.2)',
              backgroundColor: 'transparent',
              color: 'inherit',
            }}
          >
            {rootOptions}
          </select>
        </div>

        <ToggleButtonGroup
          value={selectedKey.mode}
          exclusive
          onChange={handleModeChange}
          aria-label="key mode"
        >
          <ToggleButton value="Major" aria-label="major">
            Major
          </ToggleButton>
          <ToggleButton value="Minor" aria-label="minor">
            Minor
          </ToggleButton>
        </ToggleButtonGroup>

        <div style={{ marginLeft: 'auto' }}>
          <Typography variant="body1" color="text.secondary">
            Selected Key:
          </Typography>
          <Typography variant="h5" color="primary">
            {selectedKey.root} {selectedKey.mode}
          </Typography>
        </div>
      </div>
    </Paper>
  );
};

export default KeySelector;
