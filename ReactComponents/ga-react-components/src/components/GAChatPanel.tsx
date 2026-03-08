import React, { useState } from 'react';
import { Box, Chip, CircularProgress, IconButton, InputAdornment, OutlinedInput, Typography } from '@mui/material';
import SendIcon from '@mui/icons-material/Send';
import { useGAAgent } from '../hooks/useGAAgent';
import DiatonicChordTable from './DiatonicChordTable';
import type { ChordInContext } from '../types/agent-state';

export interface GAChatPanelProps {
  /** URL of the AG-UI stream endpoint, e.g. "https://localhost:7001/api/chatbot/agui/stream" */
  agentUrl: string;
}

/**
 * Composable chat panel that wires useGAAgent to DiatonicChordTable.
 * Left column: chat input + streaming text.
 * Right column: DiatonicChordTable when diatonic chords are available.
 */
const GAChatPanel: React.FC<GAChatPanelProps> = ({ agentUrl }) => {
  const { state, messages, isStreaming, run, abort } = useGAAgent(agentUrl);
  const [input, setInput] = useState('');

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

  const handleChordClick = (_chord: ChordInContext) => {
    // Future: trigger lazy voicing fetch to VexTabViewer
  };

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
              <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: msg.role === 'assistant' ? 'serif' : undefined }}>
                {typeof msg.content === 'string' ? msg.content : ''}
              </Typography>
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
          <DiatonicChordTable
            chords={state.diatonicChords}
            onChordClick={handleChordClick}
          />
        </Box>
      )}
    </Box>
  );
};

export default GAChatPanel;
