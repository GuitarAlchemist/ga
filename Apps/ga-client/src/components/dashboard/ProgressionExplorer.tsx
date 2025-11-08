import React, { useEffect, useMemo, useState } from 'react';
import { useTheme } from '@mui/material/styles';
import { Chip } from '@mui/material';
import { useAtom, useAtomValue, useSetAtom } from 'jotai';
import { selectedKeyNameAtom } from '../../store/atoms';
import { fetchChordProgressionsForKey } from '../../services/musicService';
import type { ChordProgressionDefinition } from '../../types/music';
import {
  highlightedRomanNumeralAtom,
  selectedProgressionAtom,
} from '../../store/musicSelectionAtoms';
import { chatConfigAtom, chatInputAtom } from '../../store/chatAtoms';

const containerStyle = (background: string): React.CSSProperties => ({
  display: 'flex',
  flexDirection: 'column',
  gap: '24px',
  padding: '24px',
  borderRadius: '18px',
  background,
  minHeight: '320px',
});

const listContainerStyle: React.CSSProperties = {
  display: 'flex',
  gap: '16px',
  minHeight: '220px',
};

const listStyle: React.CSSProperties = {
  flex: '0 0 260px',
  maxHeight: '320px',
  overflowY: 'auto',
  border: '1px solid rgba(255,255,255,0.12)',
  borderRadius: 12,
};

const detailsStyle: React.CSSProperties = {
  flex: 1,
  display: 'flex',
  flexDirection: 'column',
  gap: '12px',
  overflowY: 'auto',
};

const ProgressionExplorer: React.FC = () => {
  const theme = useTheme();
  const keyName = useAtomValue(selectedKeyNameAtom);
  const { apiEndpoint } = useAtomValue(chatConfigAtom);
  const [progressions, setProgressions] = useState<ChordProgressionDefinition[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedProgression, setSelectedProgression] = useAtom(selectedProgressionAtom);
  const highlightedRoman = useAtomValue(highlightedRomanNumeralAtom);
  const setChatInput = useSetAtom(chatInputAtom);

  useEffect(() => {
    if (!apiEndpoint) {
      return;
    }

    let isMounted = true;
    const controller = new AbortController();

    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await fetchChordProgressionsForKey(apiEndpoint, keyName, controller.signal);
        if (!isMounted) {
          return;
        }

        setProgressions(data);
        if (!selectedProgression && data.length > 0) {
          setSelectedProgression(data[0]);
        }
      } catch (err) {
        if (!isMounted) {
          return;
        }
        setError(
          err instanceof Error
            ? err.message
            : 'Unable to load chord progressions. Ensure GaApi is running.',
        );
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
  }, [apiEndpoint, keyName, selectedProgression, setSelectedProgression]);

  const progressionSummaries = useMemo(() => {
    return progressions.map((progression) => ({
      name: progression.name,
      description: progression.description,
      romanNumerals: progression.romanNumerals,
    }));
  }, [progressions]);

  const active = progressions.find((item) => item.name === selectedProgression?.name) ?? null;

  const handleAskAi = () => {
    if (!active) {
      return;
    }
    const prompt = `Break down the ${active.name} progression in ${keyName}. Chords: ${active.chords.join(
      ' - ',
    )} (${active.romanNumerals.join(' · ')}). Offer comping tips and melodic approaches.`;
    setChatInput(prompt);
  };

  return (
    <div style={containerStyle(theme.palette.background.paper)}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <h2 style={{ margin: 0 }}>Progression Explorer</h2>
        <p style={{ margin: 0, color: theme.palette.text.secondary }}>
          Pulling curated progressions from GaApi → ideal for arranging and comping drills
        </p>
      </div>

      {loading && <div>Loading progressions…</div>}
      {!loading && error && <div style={{ color: theme.palette.error.main }}>{error}</div>}

      {!loading && !error && (
        <div style={listContainerStyle}>
          <div style={listStyle}>
            <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
              {progressionSummaries.map((item) => (
                <li key={item.name}>
                  <button
                    type="button"
                    onClick={() => setSelectedProgression(progressions.find((p) => p.name === item.name) ?? null)}
                    style={{
                      display: 'block',
                      width: '100%',
                      textAlign: 'left',
                      padding: '12px 16px',
                      border: 'none',
                      backgroundColor:
                        selectedProgression?.name === item.name
                          ? 'rgba(255,122,69,0.15)'
                          : 'transparent',
                      color: 'inherit',
                      cursor: 'pointer',
                    }}
                  >
                    <strong>{item.name}</strong>
                    <div style={{ fontSize: '0.75rem', opacity: 0.75 }}>{item.description}</div>
                  </button>
                </li>
              ))}
            </ul>
          </div>

          <div style={detailsStyle}>
            {active ? (
              <>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  <h3 style={{ margin: 0 }}>{active.name}</h3>
                  <p style={{ margin: 0, color: theme.palette.text.secondary }}>{active.description}</p>
                </div>

                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {active.romanNumerals.map((roman, index) => (
                    <Chip
                      key={`${roman}-${index}`}
                      label={`${roman} · ${active.chords[index] ?? '?'}`}
                      color={highlightedRoman === roman ? 'primary' : 'default'}
                      variant={highlightedRoman === roman ? 'filled' : 'outlined'}
                    />
                  ))}
                </div>

                {active.function.length > 0 && (
                  <div style={{ fontSize: '0.9rem' }}>
                    <strong>Functional storyline:</strong> {active.function.join(' → ')}
                  </div>
                )}

                {active.voiceLeading && (
                  <div style={{ fontSize: '0.9rem', color: 'rgba(255,255,255,0.75)' }}>
                    <strong>Voice leading notes:</strong> {active.voiceLeading}
                  </div>
                )}

                {active.examples.length > 0 && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <strong>Heard in:</strong>
                    <ul style={{ margin: 0, paddingLeft: '20px' }}>
                      {active.examples.map((example) => (
                        <li key={`${example.artist}-${example.song}`}>
                          {example.song} — {example.artist} ({example.usage})
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {active.variations.length > 0 && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                    <strong>Variations</strong>
                    {active.variations.map((variation) => (
                      <div
                        key={variation.name}
                        style={{
                          border: '1px solid rgba(255,255,255,0.12)',
                          borderRadius: 12,
                          padding: '12px',
                        }}
                      >
                        <div style={{ fontWeight: 600 }}>{variation.name}</div>
                        <div style={{ fontSize: '0.8rem', opacity: 0.75 }}>{variation.context}</div>
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 8 }}>
                          {variation.romanNumerals.map((roman, index) => (
                            <Chip
                              key={`${variation.name}-${roman}-${index}`}
                              label={`${roman} · ${variation.chords[index] ?? '?'}`}
                              size="small"
                            />
                          ))}
                        </div>
                      </div>
                    ))}
                  </div>
                )}

                <button type="button" onClick={handleAskAi} style={primaryButtonStyle}>
                  Ask AI for practice ideas
                </button>
              </>
            ) : (
              <span style={{ fontSize: '0.9rem', color: 'rgba(255,255,255,0.7)' }}>
                Select a progression to inspect its harmonic storyline.
              </span>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

const primaryButtonStyle: React.CSSProperties = {
  padding: '10px 18px',
  borderRadius: 999,
  border: 'none',
  backgroundColor: '#ff7a45',
  color: '#0e1014',
  cursor: 'pointer',
  fontWeight: 600,
  alignSelf: 'flex-start',
};

export default ProgressionExplorer;
