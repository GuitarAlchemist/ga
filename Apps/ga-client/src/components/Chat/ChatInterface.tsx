import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Paper, Typography, IconButton, Tooltip, Chip } from '@mui/material';
import {
  Delete,
  Settings,
  MusicNote,
  Info,
} from '@mui/icons-material';
import { useTheme } from '@mui/material/styles';
import { useAtomValue, useSetAtom } from 'jotai';
import {
  visibleMessagesAtom,
  clearMessagesAtom,
  isLoadingAtom,
  chatConfigAtom,
  chatInputAtom,
  sendMessageAtom,
} from '../../store/chatAtoms';
import ChatMessage from './ChatMessage';
import ChatInput from './ChatInput';
import {
  fetchChatExamples,
  fetchChatStatus,
  type ChatbotStatusResponse,
} from '../../services/chatService';

const ChatInterface: React.FC = () => {
  const theme = useTheme();
  const messages = useAtomValue(visibleMessagesAtom);
  const isLoading = useAtomValue(isLoadingAtom);
  const chatConfig = useAtomValue(chatConfigAtom);
  const setChatInput = useSetAtom(chatInputAtom);
  const clearMessages = useSetAtom(clearMessagesAtom);
  const sendMessage = useSetAtom(sendMessageAtom);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const [status, setStatus] = useState<ChatbotStatusResponse | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [containerHeight, setContainerHeight] = useState<number>(600);
  const [suggestions, setSuggestions] = useState<string[]>([
    'Show me a C major scale in tab',
    'Explain the circle of fifths',
    'What is a ii-V-I progression?',
    'Show me chord voicings for Cmaj7',
  ]);

  // Measure container height for virtualization
  useEffect(() => {
    const updateHeight = () => {
      if (messagesContainerRef.current) {
        const height = messagesContainerRef.current.clientHeight;
        setContainerHeight(height);
      }
    };

    updateHeight();
    window.addEventListener('resize', updateHeight);
    return () => window.removeEventListener('resize', updateHeight);
  }, []);

  // Auto-scroll to bottom when new messages arrive (only for non-virtualized)
  useEffect(() => {
    if (messages.length < VIRTUALIZATION_THRESHOLD) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages]);

  // Keep track of backend status and suggested prompts
  useEffect(() => {
    const controller = new AbortController();
    setStatusError(null);

    fetchChatStatus(chatConfig.apiEndpoint, controller.signal)
      .then(setStatus)
      .catch((error) => {
        console.error('Failed to fetch chatbot status:', error);
        setStatus(null);
        setStatusError('Chat backend unreachable. Check GaApi / Ollama.');
      });

    return () => controller.abort();
  }, [chatConfig.apiEndpoint]);

  useEffect(() => {
    const controller = new AbortController();

    fetchChatExamples(chatConfig.apiEndpoint, controller.signal)
      .then((examples) => {
        if (Array.isArray(examples) && examples.length > 0) {
          setSuggestions(examples);
        }
      })
      .catch((error) => {
        console.warn('Failed to load chat examples. Using defaults.', error);
      });

    return () => controller.abort();
  }, [chatConfig.apiEndpoint]);

  const handleSendMessage = useCallback(
    async (content: string) => {
      // Use the new sendMessageAtom which handles all API logic
      await sendMessage(content);
    },
    [sendMessage]
  );

  const handleClearChat = () => {
    if (window.confirm('Are you sure you want to clear the chat history?')) {
      clearMessages();
    }
  };

  const handleSuggestionClick = (suggestion: string) => {
    setChatInput('');
    handleSendMessage(suggestion);
  };

  const statusChip = useMemo(() => {
    if (status) {
      return (
        <Chip
          size="small"
          color={status.isAvailable ? 'success' : 'warning'}
          label={status.isAvailable ? 'Online' : 'Offline'}
        />
      );
    }

    if (statusError) {
      return (
        <Chip
          size="small"
          color="error"
          label="Disconnected"
        />
      );
    }

    return (
      <Chip size="small" color="default" label="Connecting..." />
    );
  }, [status, statusError]);

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        minHeight: '100vh',
        backgroundColor: theme.palette.background.default,
      }}
    >
      {/* Header */}
      <Paper
        elevation={2}
        style={{
          padding: '16px',
          borderBottom: `1px solid ${theme.palette.divider}`,
          backgroundColor: theme.palette.primary.main,
          color: theme.palette.primary.contrastText,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <MusicNote sx={{ fontSize: 32 }} />
            <div>
              <Typography variant="h5" component="h1">
                Guitar Alchemist Chat
              </Typography>
              <Typography variant="caption">
                AI-powered music theory assistant
              </Typography>
            </div>
            {statusChip}
          </div>

          <div style={{ display: 'flex', gap: '8px' }}>
            <Tooltip title="About">
              <IconButton color="inherit" size="small">
                <Info />
              </IconButton>
            </Tooltip>
            <Tooltip title="Settings">
              <IconButton color="inherit" size="small">
                <Settings />
              </IconButton>
            </Tooltip>
            <Tooltip title="Clear chat">
              <IconButton color="inherit" size="small" onClick={handleClearChat}>
                <Delete />
              </IconButton>
            </Tooltip>
          </div>
        </div>
      </Paper>

      {/* Messages Area */}
      <div
        ref={messagesContainerRef}
        style={{
          flex: 1,
          overflowY: messages.length >= VIRTUALIZATION_THRESHOLD ? 'hidden' : 'auto',
          padding: '24px',
          backgroundColor: theme.palette.background.default,
        }}
      >
        {statusError && (
          <div style={{ marginBottom: '16px' }}>
            <Typography variant="body2" color="error">
              {statusError}
            </Typography>
          </div>
        )}

        {messages.length === 1 && (
          <div style={{ textAlign: 'center', marginTop: '32px', marginBottom: '24px' }}>
            <Typography variant="h6" gutterBottom>
              Try these suggestions:
            </Typography>
            <div style={{ display: 'flex', justifyContent: 'center', flexWrap: 'wrap', gap: '8px' }}>
              {suggestions.map((suggestion, index) => (
                <Chip
                  key={index}
                  label={suggestion}
                  onClick={() => handleSuggestionClick(suggestion)}
                  clickable
                  color="primary"
                  variant="outlined"
                  sx={{ cursor: 'pointer' }}
                />
              ))}
            </div>
          </div>
        )}

        {/* Use virtualization for large message lists */}
        {messages.length >= VIRTUALIZATION_THRESHOLD ? (
          <VirtualizedMessageList messages={messages} containerHeight={containerHeight - 100} />
        ) : (
          <>
            {messages.map((message) => (
              <ChatMessage key={message.id} message={message} />
            ))}
            <div ref={messagesEndRef} />
          </>
        )}
      </div>

      {/* Input Area */}
      <ChatInput onSend={handleSendMessage} isLoading={isLoading} onClear={clearMessages} />
    </div>
  );
};

export default ChatInterface;
