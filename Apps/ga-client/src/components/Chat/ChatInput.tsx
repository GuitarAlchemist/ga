import React, { useRef, useEffect } from 'react';
import { TextField, IconButton, Paper, Tooltip } from '@mui/material';
import { Send, Clear } from '@mui/icons-material';
import { useAtom } from 'jotai';
import { chatInputAtom, isLoadingAtom } from '../../store/chatAtoms';

interface ChatInputProps {
  onSend: (message: string) => void;
  onClear?: () => void;
  isLoading?: boolean; // Optional prop for testing
}

const ChatInput: React.FC<ChatInputProps> = ({ onSend, onClear, isLoading: isLoadingProp }) => {
  const [input, setInput] = useAtom(chatInputAtom);
  const [isLoadingFromAtom] = useAtom(isLoadingAtom);
  const isLoading = isLoadingProp ?? isLoadingFromAtom; // Use prop if provided, otherwise use atom
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    // Focus input on mount
    inputRef.current?.focus();
  }, []);

  const handleSend = () => {
    if (input.trim() && !isLoading) {
      onSend(input.trim());
      setInput('');
      // Refocus input after sending
      setTimeout(() => inputRef.current?.focus(), 100);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleClear = () => {
    setInput('');
    onClear?.();
    inputRef.current?.focus();
  };

  return (
    <Paper elevation={3} sx={{ p: 2, borderTop: 1, borderColor: 'divider', bgcolor: 'background.paper' }}>
      <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-end' }}>
        <TextField
          inputRef={inputRef}
          fullWidth
          multiline
          maxRows={4}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask about chords, scales, progressions, or music theory..."
          disabled={isLoading}
          variant="outlined"
          size="small"
          sx={{
            '& .MuiOutlinedInput-root': {
              bgcolor: 'background.default',
            },
          }}
        />

        {input && (
          <Tooltip title="Clear input">
            <IconButton
              onClick={handleClear}
              disabled={isLoading}
              color="default"
              size="small"
            >
              <Clear />
            </IconButton>
          </Tooltip>
        )}

        <Tooltip title={isLoading ? 'Generating response...' : 'Send message (Enter)'}>
          <span>
            <IconButton
              onClick={handleSend}
              disabled={!input.trim() || isLoading}
              color="primary"
              size="large"
              aria-label="Send message"
              sx={{
                bgcolor: 'primary.main',
                color: 'primary.contrastText',
                '&:hover': {
                  bgcolor: 'primary.dark',
                },
                '&.Mui-disabled': {
                  bgcolor: 'action.disabledBackground',
                  color: 'action.disabled',
                },
              }}
            >
              <Send />
            </IconButton>
          </span>
        </Tooltip>
      </div>

      <div style={{ marginTop: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.75rem', color: 'rgba(255,255,255,0.7)' }}>
        <div>
          Press <kbd style={{ padding: '2px 6px', borderRadius: '4px', border: '1px solid', fontSize: '0.7rem' }}>Enter</kbd> to send, <kbd style={{ padding: '2px 6px', borderRadius: '4px', border: '1px solid', fontSize: '0.7rem' }}>Shift+Enter</kbd> for new line
        </div>
      </div>
    </Paper>
  );
};

export default ChatInput;

