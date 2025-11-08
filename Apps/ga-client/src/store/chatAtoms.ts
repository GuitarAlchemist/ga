import { atom } from 'jotai';
import { atomWithStorage } from 'jotai/utils';
import type { ChatApiMessage } from '../services/chatApi';

// Message types
export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  isStreaming?: boolean;
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

// Send message atom with real API integration
export const sendMessageAtom = atom(
  null,
  async (get, set, userMessage: string) => {
    if (!userMessage.trim()) {
      return;
    }

    // Set loading state
    set(isLoadingAtom, true);

    // Add user message
    set(addMessageAtom, {
      role: 'user',
      content: userMessage,
    });

    // Clear input
    set(chatInputAtom, '');

    try {
      // Get configuration and conversation history
      const config = get(chatConfigAtom);
      const messages = get(chatMessagesAtom);
      const conversationHistory: ChatApiMessage[] = messages
        .filter(m => m.role !== 'system') // Exclude system messages
        .map(m => ({
          role: m.role,
          content: m.content,
        }));

      // Create API service with configured endpoint
      const apiService = new (await import('../services/chatApi')).ChatApiService(config.apiEndpoint);

      // Create streaming message
      const streamingId = `msg-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
      let streamingContent = '';

      set(currentStreamingMessageAtom, {
        id: streamingId,
        role: 'assistant',
        content: '',
        timestamp: new Date(),
        isStreaming: true,
      });

      // Stream response from API
      try {
        for await (const chunk of apiService.streamChat({
          message: userMessage,
          conversationHistory,
          useSemanticSearch: true,
        })) {
          streamingContent += chunk;

          // Update streaming message
          set(currentStreamingMessageAtom, {
            id: streamingId,
            role: 'assistant',
            content: streamingContent,
            timestamp: new Date(),
            isStreaming: true,
          });
        }

        // Finalize message
        if (streamingContent) {
          set(addMessageAtom, {
            role: 'assistant',
            content: streamingContent,
          });
        }
      } catch (error) {
        console.error('Streaming error:', error);

        // Fallback to non-streaming if streaming fails
        try {
          const response = await apiService.sendMessage({
            message: userMessage,
            conversationHistory,
            useSemanticSearch: true,
          });

          set(addMessageAtom, {
            role: 'assistant',
            content: response.message,
          });
        } catch (fallbackError) {
          console.error('Fallback error:', fallbackError);

          // Add error message
          set(addMessageAtom, {
            role: 'assistant',
            content: 'Sorry, I encountered an error processing your request. Please make sure the GaApi backend is running (https://localhost:7184) and try again.',
          });
        }
      }
    } catch (error) {
      console.error('Error sending message:', error);

      // Add error message
      set(addMessageAtom, {
        role: 'assistant',
        content: 'Sorry, I encountered an error. Please try again.',
      });
    } finally {
      // Clear streaming message and loading state
      set(currentStreamingMessageAtom, null);
      set(isLoadingAtom, false);
    }
  }
);
