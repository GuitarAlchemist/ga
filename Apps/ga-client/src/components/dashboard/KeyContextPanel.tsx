import React, { useEffect, useMemo, useState } from 'react';
import { Chip, CircularProgress } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useAtomValue, useSetAtom } from 'jotai';
import { selectedKeyAtom, selectedKeyNameAtom } from '../../store/atoms';
import { chatConfigAtom, chatInputAtom } from '../../store/chatAtoms';
import { fetchKeyNotes, fetchScaleDegrees } from '../../services/musicService';
import type { KeyNotes, ScaleDegree } from '../../types/music';
import { keyNotesAtom, scaleDegreesAtom } from '../../store/musicDataAtoms';

const containerStyle = (background: string): React.CSSProperties => ({
  display: 'flex',
  flexDirection: 'column',
  gap: '24px',
  padding: '24px',
  borderRadius: '18px',
  background,
  minHeight: '320px',
});

const sectionDivider: React.CSSProperties = {
  height: 1,
  backgroundColor: 'rgba(255,255,255,0.08)',
};

const degreeGridStyle: React.CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
  gap: '12px',
};

const degreeCardStyle = (
  isTonic: boolean,
  primary: string,
  paper: string,
): React.CSSProperties => ({
  padding: '12px',
  borderRadius: 12,
  border: '1px solid rgba(255,255,255,0.12)',
  backgroundColor: isTonic ? primary : paper,
  color: isTonic ? '#0e1014' : 'inherit',
});

const KeyContextPanel: React.FC = () => {
  const theme = useTheme();
  const keyName = useAtomValue(selectedKeyNameAtom);
  const selectedKey = useAtomValue(selectedKeyAtom);
  const chatConfig = useAtomValue(chatConfigAtom);
  const setChatInput = useSetAtom(chatInputAtom);
  const setKeyNotesAtom = useSetAtom(keyNotesAtom);
  const setScaleDegreesAtom = useSetAtom(scaleDegreesAtom);

  const [keyNotes, setKeyNotesLocal] = useState<KeyNotes | null>(null);
  const [scaleDegrees, setScaleDegreesLocal] = useState<ScaleDegree[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!chatConfig.apiEndpoint) {
      return;
    }

    const controller = new AbortController();
    let isMounted = true;

    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const [notes, degrees] = await Promise.all([
          fetchKeyNotes(chatConfig.apiEndpoint, keyName, controller.signal),
          fetchScaleDegrees(chatConfig.apiEndpoint, controller.signal),
        ]);

        if (!isMounted) {
          return;
        }

        setKeyNotesLocal(notes);
        setScaleDegreesLocal(degrees);
        setKeyNotesAtom(notes);
        setScaleDegreesAtom(degrees);
      } catch (err) {
        if (!isMounted) {
          return;
        }
        setError(
          err instanceof Error
            ? err.message
            : 'Unable to load key insights. Ensure the backend is running.',
        );
        setKeyNotesAtom(null);
        setScaleDegreesAtom([]);
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    };

    load();

    return () => {
      isMounted = false;
      controller.abort();
    };
  }, [chatConfig.apiEndpoint, keyName, setKeyNotesAtom, setScaleDegreesAtom]);

  const degreeRows = useMemo(() => {
    if (!keyNotes || scaleDegrees.length === 0) {
      return [];
    }
    return scaleDegrees.map((degree, index) => ({
      ...degree,
      note: keyNotes.notes[index % keyNotes.notes.length] ?? '-',
    }));
  }, [keyNotes, scaleDegrees]);

  const handleAskAiClick = () => {
    if (!keyNotes) {
      return;
    }

    const prompt = `Give me practical ideas for improvising in ${keyNotes.keyName}. The notes are ${keyNotes.notes.join(', ')}. Suggest chord tone targets for ${
      keyNotes.mode === 'Major' ? 'dominant and tonic' : 'tonic and subdominant'
    } functions.`;
    setChatInput(prompt);
  };

  return (
    <div style={containerStyle(theme.palette.background.paper)}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <h2 style={{ margin: 0 }}>Key Context</h2>
        <p style={{ margin: 0, color: theme.palette.text.secondary }}>
          Data pulled from GaApi music-theory module · ground truth for scales & functions
        </p>
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
        <div />
        <Chip label="Ask AI how to use this key" color="primary" onClick={handleAskAiClick} sx={{ cursor: 'pointer' }} />
      </div>

      {loading && (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 120 }}>
          <CircularProgress size={32} />
        </div>
      )}

      {!loading && error && <div style={{ color: theme.palette.error.main }}>{error}</div>}

      {!loading && !error && keyNotes && (
        <>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div>
              <h3 style={{ margin: 0 }}>{keyNotes.keyName}</h3>
              <p style={{ margin: 0, color: theme.palette.text.secondary }}>
                {keyNotes.keySignature === 0
                  ? 'No accidentals'
                  : `${Math.abs(keyNotes.keySignature)} ${
                      keyNotes.keySignature > 0 ? 'sharps' : 'flats'
                    } (${keyNotes.accidentalKind})`}
              </p>
            </div>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              {keyNotes.notes.map((note) => (
                <Chip
                  key={note}
                  label={note}
                  color={note === selectedKey.root ? 'primary' : 'default'}
                  variant={note === selectedKey.root ? 'filled' : 'outlined'}
                />
              ))}
            </div>
          </div>

          <div style={sectionDivider} />

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h4 style={{ margin: 0 }}>Scale Degrees</h4>
            <div style={degreeGridStyle}>
              {degreeRows.map((degree) => (
                <div
                  key={degree.degree}
                  style={degreeCardStyle(
                    degree.degree === 1,
                    theme.palette.primary.main,
                    'rgba(255,255,255,0.05)',
                  )}
                >
                  <strong>
                    {degree.romanNumeral} · {degree.note}
                  </strong>
                  <div style={{ fontSize: '0.85rem', color: 'rgba(255,255,255,0.75)' }}>{degree.name}</div>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default KeyContextPanel;
