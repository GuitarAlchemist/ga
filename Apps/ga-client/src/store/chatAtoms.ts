import { atom } from 'jotai';
import { atomWithStorage } from 'jotai/utils';
import type { ChordInContext } from 'ga-react-components/src/types/agent-state';

// Agent routing metadata surfaced by the agentic pipeline
export interface AgentRouting {
  agentId: string;
  confidence: number;
  routingMethod: string;
}

// Message types
export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  isStreaming?: boolean;
  routing?: AgentRouting;
}

// Chat configuration
export interface ChatConfig {
  apiEndpoint: string;
  model: string;
  temperature: number;
  maxTokens: number;
}

// Default configuration
const defaultConfig: ChatConfig = {
  apiEndpoint: 'https://localhost:7184', // GaApi endpoint (HTTPS)
  model: 'llama3.2:3b', // Ollama model
  temperature: 0.7,
  maxTokens: 2000,
};

// Atoms
export const chatMessagesAtom = atomWithStorage<ChatMessage[]>('ga-chat-messages', [
  {
    id: 'system-welcome',
    role: 'system',
    content: 'Welcome to Guitar Alchemist! I can help you with chord progressions, music theory, guitar techniques, and more. Try asking me about scales, chords, or music notation!',
    timestamp: new Date(),
  },
]);

export const chatInputAtom = atom<string>('');

export const isLoadingAtom = atom<boolean>(false);

export const chatConfigAtom = atomWithStorage<ChatConfig>('ga-chat-config', defaultConfig);

export const currentStreamingMessageAtom = atom<ChatMessage | null>(null);

/** Diatonic chords populated by the AG-UI ga:diatonic CUSTOM event. */
export const diatonicChordsAtom = atom<readonly ChordInContext[]>([]);

/** Current key identified by the agent (e.g. "G major"). */
export const detectedKeyAtom = atom<string | null>(null);

// Derived atoms
export const visibleMessagesAtom = atom((get) => {
  const messages = get(chatMessagesAtom);
  const streamingMessage = get(currentStreamingMessageAtom);

  // Filter out system messages for display (except welcome)
  const filtered = messages.filter(m => m.role !== 'system' || m.id === 'system-welcome');

  // Add streaming message if present
  if (streamingMessage) {
    return [...filtered, streamingMessage];
  }

  return filtered;
});

// Actions (write-only atoms)
export const addMessageAtom = atom(
  null,
  (get, set, message: Omit<ChatMessage, 'id' | 'timestamp'>) => {
    const newMessage: ChatMessage = {
      ...message,
      id: `msg-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      timestamp: new Date(),
    };

    const messages = get(chatMessagesAtom);
    set(chatMessagesAtom, [...messages, newMessage]);

    return newMessage;
  }
);

export const clearMessagesAtom = atom(
  null,
  (_get, set) => {
    set(chatMessagesAtom, [
      {
        id: 'system-welcome',
        role: 'system',
        content: 'Welcome to Guitar Alchemist! I can help you with chord progressions, music theory, guitar techniques, and more. Try asking me about scales, chords, or music notation!',
        timestamp: new Date(),
      },
    ]);
    set(currentStreamingMessageAtom, null);
  }
);

// Send message atom — uses AG-UI protocol (POST /api/chatbot/agui/stream).
export const sendMessageAtom = atom(
  null,
  async (get, set, userMessage: string) => {
    if (!userMessage.trim()) return;

    set(isLoadingAtom, true);
    set(addMessageAtom, { role: 'user', content: userMessage });
    set(chatInputAtom, '');
    set(diatonicChordsAtom, []);
    set(detectedKeyAtom, null);

    const config      = get(chatConfigAtom);
    const streamingId = `msg-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    let   accumulated = '';

    set(currentStreamingMessageAtom, {
      id: streamingId, role: 'assistant', content: '', timestamp: new Date(), isStreaming: true,
    });

    try {
      const { streamAgUiChat } = await import('../services/agUiChatService');

      await streamAgUiChat(config.apiEndpoint, userMessage, {
        onChunk(delta) {
          accumulated += delta;
          set(currentStreamingMessageAtom, {
            id: streamingId, role: 'assistant', content: accumulated, timestamp: new Date(), isStreaming: true,
          });
        },
        onDiatonicChords(chords) {
          set(diatonicChordsAtom, chords);
        },
        onComplete() {
          if (accumulated) {
            set(addMessageAtom, { role: 'assistant', content: accumulated });
          }
        },
        onError(msg) {
          set(addMessageAtom, {
            role: 'assistant',
            content: `Sorry, the assistant encountered an error: ${msg}`,
          });
        },
      });
    } catch (error) {
      console.error('AG-UI stream error:', error);
      set(addMessageAtom, {
        role: 'assistant',
        content: 'Sorry, I encountered an error. Please make sure GaApi is running and try again.',
      });
    } finally {
      set(currentStreamingMessageAtom, null);
      set(isLoadingAtom, false);
    }
  }
);
