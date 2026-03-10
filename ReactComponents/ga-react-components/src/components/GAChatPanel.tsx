import React, { useState, useRef, useEffect } from 'react';
import { Box, Chip, CircularProgress, IconButton, InputAdornment, OutlinedInput, Popover, Typography } from '@mui/material';
import SendIcon from '@mui/icons-material/Send';
import { useGAAgent } from '../hooks/useGAAgent';
import DiatonicChordTable from './DiatonicChordTable';
import FretDiagram from './FretDiagram';
import type { ChordInContext } from '../types/agent-state';

export interface GAChatPanelProps {
  /** URL of the AG-UI stream endpoint, e.g. "https://localhost:7001/api/chatbot/agui/stream" */
  agentUrl: string;
  /**
   * Optional base URL for voicing lookups (defaults to same origin as agentUrl).
   * Chord name clicks call GET /api/contextual-chords/voicings/{chord}.
   */
  apiBaseUrl?: string;
}

/** Voicing shape returned by GET /api/contextual-chords/voicings/{chord} */
interface Voicing {
  chordName: string;
  frets: number[];
  difficulty: string;
}

// Chord symbol pattern — matches triads and 7th chords in text
const CHORD_REGEX = /\b([A-G][b#]?(?:m7b5|dim7|maj7|m7|7|min|m|dim|aug|\+)?)\b/g;

/**
 * Composable chat panel that wires useGAAgent to DiatonicChordTable.
 * Left column: chat input + streaming text (chord names are clickable — shows fret diagram).
 * Right column: DiatonicChordTable when diatonic chords are available.
 */
const GAChatPanel: React.FC<GAChatPanelProps> = ({ agentUrl, apiBaseUrl }) => {
  const { state, messages, isStreaming, run, abort } = useGAAgent(agentUrl);
  const [input, setInput] = useState('');

  // Chord diagram popover state
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [activeVoicing, setActiveVoicing] = useState<Voicing | null>(null);
  const [voicingLoading, setVoicingLoading] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  const handleSend = () => {
    const msg = input.trim();
    if (!msg || isStreaming) return;
    setInput('');
    void run(msg);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleChordClick = (chord: ChordInContext) => {
    // DiatonicChordTable click — open the voicing popover for this chord
    void fetchAndShowVoicing(chord.symbol, null);
  };

  /** Fetch the most comfortable voicing and show the diagram popover. */
  const fetchAndShowVoicing = async (chordName: string, anchor: HTMLElement | null) => {
    abortRef.current?.abort();
    const ac = new AbortController();
    abortRef.current = ac;

    setAnchorEl(anchor);
    setVoicingLoading(true);
    setActiveVoicing(null);

    try {
      const base  = apiBaseUrl ?? new URL(agentUrl).origin;
      const url   = `${base}/api/contextual-chords/voicings/${encodeURIComponent(chordName)}`;
      const resp  = await fetch(url, { signal: ac.signal });
      if (resp.ok) {
        const data: Voicing[] = await resp.json() as Voicing[];
        const best = data[0];
        if (best) setActiveVoicing(best);
      }
    } catch {
      // Network errors or abort — swallow silently; popover just stays empty
    } finally {
      setVoicingLoading(false);
    }
  };

  const closePopover = () => {
    abortRef.current?.abort();
    setAnchorEl(null);
    setActiveVoicing(null);
  };

  // Abort in-flight fetch on unmount
  useEffect(() => () => { abortRef.current?.abort(); }, []);

  const hasChords = state.diatonicChords.length > 0;

  return (
    <Box sx={{ display: 'flex', gap: 2, height: '100%', minHeight: 300 }}>
      {/* ── Left: Chat ────────────────────────────────────────────── */}
      <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        {/* Routing badge */}
        {state.analysisPhase !== 'idle' && (
          <Chip
            size="small"
            label={state.analysisPhase === 'complete' ? `${state.key ?? 'GA'} · complete` : 'thinking…'}
            sx={{ alignSelf: 'flex-start', fontFamily: 'monospace', fontSize: '0.7rem' }}
          />
        )}

        {/* Message history */}
        <Box sx={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 1 }}>
          {messages.map((msg, i) => (
            <Box
              key={i}
              sx={{
                alignSelf:    msg.role === 'user' ? 'flex-end' : 'flex-start',
                bgcolor:      msg.role === 'user' ? '#e3f2fd' : '#f5f5f5',
                borderRadius: 2,
                px: 1.5,
                py: 0.75,
                maxWidth:     '85%',
              }}
            >
              {msg.role === 'assistant'
                ? <ChordAnnotatedText
                    text={typeof msg.content === 'string' ? msg.content : ''}
                    onChordClick={(chord, el) => { void fetchAndShowVoicing(chord, el); }}
                  />
                : <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                    {typeof msg.content === 'string' ? msg.content : ''}
                  </Typography>
              }
            </Box>
          ))}
          {isStreaming && (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, pl: 0.5 }}>
              <CircularProgress size={14} />
              <Typography variant="caption" color="text.secondary">generating…</Typography>
            </Box>
          )}
          {state.lastError && (
            <Typography variant="caption" color="error">{state.lastError}</Typography>
          )}
        </Box>

        {/* Input */}
        <OutlinedInput
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask about chords, scales, theory…"
          size="small"
          multiline
          maxRows={4}
          disabled={isStreaming}
          endAdornment={
            <InputAdornment position="end">
              {isStreaming
                ? <IconButton size="small" onClick={abort}><CircularProgress size={16} /></IconButton>
                : <IconButton size="small" onClick={handleSend} disabled={!input.trim()}><SendIcon fontSize="small" /></IconButton>
              }
            </InputAdornment>
          }
        />
      </Box>

      {/* ── Right: Domain components ──────────────────────────────── */}
      {hasChords && (
        <Box sx={{ width: 280, flexShrink: 0, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <Typography
            variant="subtitle2"
            sx={{ color: '#666', fontFamily: 'monospace', letterSpacing: 1 }}
          >
            {state.key} {state.mode}
          </Typography>

          {/* Scale note badges — one per degree */}
          {state.scaleNotes.length > 0 && (
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
              {state.scaleNotes.map(sn => (
                <Box
                  key={sn.degree}
                  sx={{
                    px: 0.8, py: 0.2,
                    borderRadius: 1,
                    bgcolor: sn.degree === 1 ? '#1565c0' : '#e3f2fd',
                    color:   sn.degree === 1 ? '#fff'    : '#1565c0',
                    fontFamily:  'monospace',
                    fontSize:    '0.68rem',
                    fontWeight:  sn.degree === 1 ? 700 : 400,
                    lineHeight:  1.4,
                  }}
                  title={`Scale degree ${sn.degree}`}
                >
                  {sn.note}
                </Box>
              ))}
            </Box>
          )}

          <DiatonicChordTable
            chords={state.diatonicChords}
            onChordClick={handleChordClick}
          />
        </Box>
      )}

      {/* ── Chord diagram popover ─────────────────────────────────── */}
      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={closePopover}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
        transformOrigin={{ vertical: 'top', horizontal: 'center' }}
        slotProps={{ paper: { sx: { p: 1.5, borderRadius: 2 } } }}
      >
        {voicingLoading && <CircularProgress size={32} sx={{ m: 2 }} />}
        {!voicingLoading && activeVoicing && (
          <FretDiagram chordName={activeVoicing.chordName} frets={activeVoicing.frets} />
        )}
        {!voicingLoading && !activeVoicing && (
          <Typography variant="caption" sx={{ p: 1, display: 'block' }}>No voicing found</Typography>
        )}
      </Popover>
    </Box>
  );
};

// ── ChordAnnotatedText ─────────────────────────────────────────────────────

interface ChordAnnotatedTextProps {
  text: string;
  onChordClick: (chord: string, anchor: HTMLElement) => void;
}

/**
 * Splits assistant message text into plain segments and clickable chord-name chips.
 * Chord names are highlighted and open a fret diagram popover on click.
 */
const ChordAnnotatedText: React.FC<ChordAnnotatedTextProps> = ({ text, onChordClick }) => {
  const parts: React.ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  // Reset regex state before each render
  CHORD_REGEX.lastIndex = 0;

  while ((match = CHORD_REGEX.exec(text)) !== null) {
    const chord = match[1];
    if (!chord) continue;

    if (match.index > lastIndex)
      parts.push(text.slice(lastIndex, match.index));

    parts.push(
      <Box
        key={`chord-${match.index}`}
        component="span"
        onClick={(e: React.MouseEvent<HTMLSpanElement>) => {
          onChordClick(chord, e.currentTarget);
        }}
        sx={{
          display:       'inline',
          cursor:        'pointer',
          color:         '#1565c0',
          fontWeight:    600,
          fontFamily:    'monospace',
          bgcolor:       'rgba(21,101,192,0.07)',
          borderRadius:  0.5,
          px:            0.4,
          '&:hover':     { bgcolor: 'rgba(21,101,192,0.15)' },
        }}
      >
        {chord}
      </Box>
    );

    lastIndex = match.index + match[0].length;
  }

  if (lastIndex < text.length)
    parts.push(text.slice(lastIndex));

  return (
    <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'serif' }}>
      {parts}
    </Typography>
  );
};

export default GAChatPanel;
