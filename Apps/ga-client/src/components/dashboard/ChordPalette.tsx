import React, { useEffect, useMemo, useState } from 'react';
import { Chip } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useAtom, useAtomValue, useSetAtom } from 'jotai';
import {
  keyChordFiltersAtom,
  selectedKeyNameAtom,
} from '../../store/atoms';
import type { KeyChordFilters } from '../../store/atoms';
import { fetchChordsForKey } from '../../services/musicService';
import type { ChordInContext } from '../../types/music';
import {
  highlightedRomanNumeralAtom,
  selectedChordAtom,
} from '../../store/musicSelectionAtoms';
import { chatConfigAtom, chatInputAtom } from '../../store/chatAtoms';

type BooleanFilterKey =
  | 'onlyNaturallyOccurring'
  | 'includeBorrowedChords'
  | 'includeSecondaryDominants'
  | 'includeSecondaryTwoFive';

const containerStyle = (background: string): React.CSSProperties => ({
  display: 'flex',
  flexDirection: 'column',
  gap: '20px',
  padding: '24px',
  borderRadius: '18px',
  background,
  minHeight: '320px',
});

const chipRowStyle: React.CSSProperties = {
  display: 'flex',
  flexWrap: 'wrap',
  gap: '8px',
  alignItems: 'center',
};

const controlRowStyle: React.CSSProperties = {
  display: 'flex',
  flexWrap: 'wrap',
  gap: '16px',
  alignItems: 'center',
};

const cardStyle: React.CSSProperties = {
  padding: '16px',
  borderRadius: '16px',
  border: '1px solid rgba(255,255,255,0.12)',
  backgroundColor: 'rgba(255,255,255,0.04)',
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'center',
  gap: '12px',
};

const actionsStyle: React.CSSProperties = {
  display: 'flex',
  gap: '8px',
};

const secondaryButtonStyle: React.CSSProperties = {
  padding: '8px 16px',
  borderRadius: 999,
  border: '1px solid rgba(255,255,255,0.2)',
  background: 'transparent',
  color: '#ff7a45',
  cursor: 'pointer',
  fontWeight: 600,
};

const primaryButtonStyle: React.CSSProperties = {
  padding: '8px 16px',
  borderRadius: 999,
  border: 'none',
  backgroundColor: '#ff7a45',
  color: '#0e1014',
  cursor: 'pointer',
  fontWeight: 600,
};

const formatCommonality = (value: number) => `${Math.round(value * 100)}%`;

const booleanFilters: Array<[BooleanFilterKey, string]> = [
  ['onlyNaturallyOccurring', 'Diatonic only'],
  ['includeBorrowedChords', 'Borrowed'],
  ['includeSecondaryDominants', 'Secondary dominants'],
  ['includeSecondaryTwoFive', 'Secondary ii–V'],
];

const presetFilters: KeyChordFilters = {
  onlyNaturallyOccurring: false,
  includeBorrowedChords: true,
  includeSecondaryDominants: true,
  includeSecondaryTwoFive: true,
  minCommonality: 0.35,
  limit: 24,
};

const ChordPalette: React.FC = () => {
  const theme = useTheme();
  const keyName = useAtomValue(selectedKeyNameAtom);
  const { apiEndpoint } = useAtomValue(chatConfigAtom);
  const [filters, setFilters] = useAtom(keyChordFiltersAtom);
  const setSelectedChord = useSetAtom(selectedChordAtom);
  const setHighlightedRomanNumeral = useSetAtom(highlightedRomanNumeralAtom);
  const setChatInput = useSetAtom(chatInputAtom);

  const [chords, setChords] = useState<ChordInContext[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
        const data = await fetchChordsForKey({
          baseUrl: apiEndpoint,
          keyName,
          filters,
          signal: controller.signal,
        });

        if (!isMounted) {
          return;
        }

        setChords(data);
      } catch (err) {
        if (!isMounted) {
          return;
        }
        setError(
          err instanceof Error
            ? err.message
            : 'Unable to load contextual chords. Ensure GaApi is running.',
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
  }, [apiEndpoint, keyName, filters]);

  const groupedChords = useMemo(() => {
    return chords.reduce<Record<string, ChordInContext[]>>((acc, chord) => {
      const bucket = chord.function ?? 'Unclassified';
      if (!acc[bucket]) {
        acc[bucket] = [];
      }
      acc[bucket].push(chord);
      return acc;
    }, {});
  }, [chords]);

  const handleCheckbox = (field: BooleanFilterKey, checked: boolean) => {
    setFilters((current) => ({
      ...current,
      [field]: checked,
    }));
  };

  const handleCommonalityChange = (value: number) => {
    setFilters((current) => ({
      ...current,
      minCommonality: Number(value.toFixed(2)),
    }));
  };

  const handleLimitChange = (limit: number) => {
    setFilters((current) => ({
      ...current,
      limit,
    }));
  };

  const handleSelectChord = (chord: ChordInContext) => {
    setSelectedChord(chord);
    setHighlightedRomanNumeral(chord.romanNumeral ?? null);
  };

  const handleAskAi = (chord: ChordInContext) => {
    const label = chord.romanNumeral
      ? `${chord.romanNumeral} (${chord.contextualName})`
      : chord.contextualName;
    const prompt = `In ${keyName}, explain how to voice-lead into ${label}. The harmonic function is ${
      chord.function
    }. Provide voice-leading tips and practice ideas.`;
    setChatInput(prompt);
  };

  return (
    <div style={containerStyle(theme.palette.background.paper)}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <h2 style={{ margin: 0 }}>Chord Palette</h2>
        <p style={{ margin: 0, color: theme.palette.text.secondary }}>
          Curated from contextual-chords service · tuned to real harmonic usage
        </p>
      </div>

      <div style={controlRowStyle}>
        {booleanFilters.map(([field, label]) => (
          <label key={field} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <input
              type="checkbox"
              checked={filters[field]}
              onChange={(event) => handleCheckbox(field, event.target.checked)}
            />
            <span>{label}</span>
          </label>
        ))}
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <span style={{ fontSize: '0.8rem', color: theme.palette.text.secondary }}>
          Minimum commonality: {formatCommonality(filters.minCommonality)}
        </span>
        <input
          type="range"
          min={0}
          max={1}
          step={0.05}
          value={filters.minCommonality}
          onChange={(event) => handleCommonalityChange(Number(event.target.value))}
          style={{ width: '100%' }}
        />
        <div style={chipRowStyle}>
          {[12, 24, 48].map((limit) => (
            <Chip
              key={limit}
              label={`${limit} chords`}
              color={filters.limit === limit ? 'primary' : 'default'}
              variant={filters.limit === limit ? 'filled' : 'outlined'}
              onClick={() => handleLimitChange(limit)}
              sx={{ cursor: 'pointer' }}
            />
          ))}
          <button
            type="button"
            onClick={() => setFilters({ ...presetFilters })}
            style={secondaryButtonStyle}
          >
            Reset
          </button>
        </div>
      </div>

      <div style={{ height: 1, backgroundColor: 'rgba(255,255,255,0.08)' }} />

      {loading && <div>Loading chord context…</div>}
      {!loading && error && <div style={{ color: theme.palette.error.main }}>{error}</div>}

      {!loading && !error && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {Object.entries(groupedChords).map(([groupName, chordsInGroup]) => (
            <div key={groupName}>
              <div style={{ fontWeight: 600, marginBottom: 8 }}>{groupName}</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                {chordsInGroup.map((chord) => (
                  <div
                    key={`${chord.contextualName}-${chord.romanNumeral ?? 'nr'}`}
                    style={cardStyle}
                  >
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                        <strong style={{ fontSize: '1.1rem' }}>{chord.contextualName}</strong>
                        {chord.romanNumeral && (
                          <Chip size="small" label={chord.romanNumeral} color="secondary" />
                        )}
                        <Chip
                          size="small"
                          label={chord.isNaturallyOccurring ? 'Diatonic' : 'Color tone'}
                          color={chord.isNaturallyOccurring ? 'success' : 'warning'}
                        />
                      </div>
                      <span style={{ fontSize: '0.85rem', color: 'rgba(255,255,255,0.75)' }}>
                        {`Commonality ${formatCommonality(chord.commonality)} · ${
                          chord.functionalDescription ?? 'Functional role available'
                        }`}
                      </span>
                    </div>
                    <div style={actionsStyle}>
                      <button
                        type="button"
                        onClick={() => handleSelectChord(chord)}
                        style={primaryButtonStyle}
                      >
                        Focus
                      </button>
                      <button
                        type="button"
                        onClick={() => handleAskAi(chord)}
                        style={secondaryButtonStyle}
                      >
                        Ask AI
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
          {chords.length === 0 && (
            <span style={{ fontSize: '0.9rem', color: 'rgba(255,255,255,0.7)' }}>
              No chords match the current filters. Try lowering the commonality threshold or enabling borrowed chords.
            </span>
          )}
        </div>
      )}
    </div>
  );
};

export default ChordPalette;
