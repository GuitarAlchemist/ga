import React, { useEffect, useState } from 'react';
import { Chip } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useAtomValue } from 'jotai';
import GuitarFretboard, {
  type FretboardPosition,
  type FretboardConfig,
} from 'ga-react-components/src/components/GuitarFretboard';
import { selectedKeyAtom } from '../../store/atoms';
import {
  pinnedNotesAtom,
  selectedChordAtom,
  selectedProgressionAtom,
} from '../../store/musicSelectionAtoms';
import { keyNotesAtom } from '../../store/musicDataAtoms';

const fretboardConfig: FretboardConfig = {
  fretCount: 15,
  stringCount: 6,
  startFret: 0,
  tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
  showFretNumbers: true,
  showStringLabels: true,
  width: 1000,
  height: 260,
  spacingMode: 'realistic',
};

const tuning = ['E', 'B', 'G', 'D', 'A', 'E'] as const;

const noteToValue: Record<string, number> = {
  C: 0,
  'C#': 1,
  Db: 1,
  D: 2,
  'D#': 3,
  Eb: 3,
  E: 4,
  Fb: 4,
  'E#': 5,
  F: 5,
  'F#': 6,
  Gb: 6,
  G: 7,
  'G#': 8,
  Ab: 8,
  A: 9,
  'A#': 10,
  Bb: 10,
  B: 11,
  Cb: 11,
};

const sanitize = (note: string) => note.trim().replace('♯', '#').replace('♭', 'b').toUpperCase();

const toValue = (note: string) => noteToValue[sanitize(note)] ?? null;

const valueToLabel = (value: number, preferFlats: boolean) => {
  const mod = ((value % 12) + 12) % 12;
  const sharpNames = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
  const flatNames = ['C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B'];
  return preferFlats ? flatNames[mod] : sharpNames[mod];
};

const computePositions = (
  values: Set<number>,
  roots: Set<number>,
  preferFlats: boolean,
): FretboardPosition[] => {
  const positions: FretboardPosition[] = [];
  tuning.forEach((openNote, index) => {
    const openValue = toValue(openNote);
    if (openValue == null) {
      return;
    }
    for (let fret = 0; fret <= 12; fret += 1) {
      const pitchValue = (openValue + fret) % 12;
      if (values.has(pitchValue)) {
        const isRoot = roots.has(pitchValue);
        positions.push({
          string: index,
          fret,
          label: valueToLabel(pitchValue, preferFlats),
          color: isRoot ? '#ff6b6b' : '#4dabf7',
          emphasized: isRoot,
        });
      }
    }
  });
  return positions;
};

const FretboardWorkbench: React.FC = () => {
  const theme = useTheme();
  const selectedKey = useAtomValue(selectedKeyAtom);
  const keyNotes = useAtomValue(keyNotesAtom);
  const selectedChord = useAtomValue(selectedChordAtom);
  const selectedProgression = useAtomValue(selectedProgressionAtom);
  const pinnedNotes = useAtomValue(pinnedNotesAtom);

  const [positions, setPositions] = useState<FretboardPosition[]>([]);

  useEffect(() => {
    const preferFlats = keyNotes?.accidentalKind?.toLowerCase().includes('flat') ?? false;

    const pinnedValues = new Set<number>();
    pinnedNotes.forEach((note) => {
      const value = toValue(note);
      if (value != null) {
        pinnedValues.add(((value % 12) + 12) % 12);
      }
    });

    if (selectedChord) {
      const rootValue = toValue(selectedChord.root);
      const chordValues = new Set<number>();
      selectedChord.intervals.forEach((interval) => {
        if (rootValue != null) {
          chordValues.add(((rootValue + interval) % 12 + 12) % 12);
        }
      });
      if (rootValue != null) {
        chordValues.add(((rootValue % 12) + 12) % 12);
      }
      pinnedValues.forEach((value) => chordValues.add(value));
      setPositions(computePositions(chordValues, chordValues, preferFlats));
      return;
    }

    if (keyNotes) {
      const scaleValues = new Set<number>();
      keyNotes.notes.forEach((note) => {
        const value = toValue(note);
        if (value != null) {
          scaleValues.add(((value % 12) + 12) % 12);
        }
      });
      pinnedValues.forEach((value) => scaleValues.add(value));
      const rootValue = toValue(selectedKey.root);
      const rootSet = new Set<number>();
      if (rootValue != null) {
        rootSet.add(((rootValue % 12) + 12) % 12);
      }
      setPositions(computePositions(scaleValues, rootSet, preferFlats));
      return;
    }

    setPositions([]);
  }, [keyNotes, pinnedNotes, selectedChord, selectedKey.root]);

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      gap: '16px',
      padding: '24px',
      borderRadius: '18px',
      background: theme.palette.background.paper,
      minHeight: '320px',
    }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <h2 style={{ margin: 0 }}>Fretboard Workbench</h2>
        <p style={{ margin: 0, color: theme.palette.text.secondary }}>
          Visualize the key or selected chord directly on the fretboard.
        </p>
      </div>

      {positions.length > 0 ? (
        <GuitarFretboard config={fretboardConfig} positions={positions} displayMode={selectedChord ? 'chord' : 'scale'} />
      ) : (
        <div style={{ fontSize: '0.9rem', color: 'rgba(255,255,255,0.7)' }}>
          Select a key or chord to render fretboard positions.
        </div>
      )}

      {selectedChord ? (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
          <strong>{selectedChord.contextualName}</strong>
          <Chip label={selectedChord.romanNumeral ?? 'No Roman numeral'} size="small" />
          <Chip
            label={`${Math.round(selectedChord.commonality * 100)}% usage`}
            size="small"
            color="primary"
          />
        </div>
      ) : keyNotes ? (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {keyNotes.notes.map((note) => (
            <Chip key={note} label={note} size="small" />
          ))}
        </div>
      ) : null}

      {selectedProgression && (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <strong>Progression:</strong>
          {selectedProgression.romanNumerals.map((roman, index) => (
            <Chip
              key={`${roman}-${index}`}
              label={`${roman} · ${selectedProgression.chords[index] ?? '?'}`}
              size="small"
            />
          ))}
        </div>
      )}
    </div>
  );
};

export default FretboardWorkbench;
