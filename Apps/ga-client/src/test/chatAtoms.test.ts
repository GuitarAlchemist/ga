import { describe, it, expect, beforeEach } from 'vitest';
import { createStore } from 'jotai';
import {
  chatMessagesAtom,
  chatInputAtom,
  isLoadingAtom,
  currentStreamingMessageAtom,
  visibleMessagesAtom,
  addMessageAtom,
  clearMessagesAtom,
} from '../store/chatAtoms';

describe('Chat Atoms', () => {
  let store: ReturnType<typeof createStore>;

  beforeEach(() => {
    store = createStore();
    localStorage.clear();
  });

  describe('chatMessagesAtom', () => {
    it('should initialize with welcome message', () => {
      const messages = store.get(chatMessagesAtom);
      expect(messages).toHaveLength(1);
      expect(messages[0].role).toBe('system');
      expect(messages[0].content).toContain('Guitar Alchemist');
    });

    it('should persist to localStorage', () => {
      const newMessage = {
        id: '2',
        role: 'user' as const,
        content: 'Test message',
        timestamp: new Date(),
      };

      store.set(chatMessagesAtom, [newMessage]);

      const stored = localStorage.getItem('ga-chat-messages');
      expect(stored).toBeTruthy();
      const parsed = JSON.parse(stored!);
      expect(parsed).toHaveLength(1);
      expect(parsed[0].content).toBe('Test message');
    });
  });

  describe('chatInputAtom', () => {
    it('should initialize as empty string', () => {
      const input = store.get(chatInputAtom);
      expect(input).toBe('');
    });

    it('should update input value', () => {
      store.set(chatInputAtom, 'Test input');
      expect(store.get(chatInputAtom)).toBe('Test input');
    });
  });

  describe('isLoadingAtom', () => {
    it('should initialize as false', () => {
      const loading = store.get(isLoadingAtom);
      expect(loading).toBe(false);
    });

    it('should toggle loading state', () => {
      store.set(isLoadingAtom, true);
      expect(store.get(isLoadingAtom)).toBe(true);

      store.set(isLoadingAtom, false);
      expect(store.get(isLoadingAtom)).toBe(false);
    });
  });

  describe('currentStreamingMessageAtom', () => {
    it('should initialize as null', () => {
      const streaming = store.get(currentStreamingMessageAtom);
      expect(streaming).toBeNull();
    });

    it('should set streaming message', () => {
      const message = {
        id: 'stream-1',
        role: 'assistant' as const,
        content: 'Streaming...',
        timestamp: new Date(),
      };

      store.set(currentStreamingMessageAtom, message);
      expect(store.get(currentStreamingMessageAtom)).toEqual(message);
    });
  });

  describe('visibleMessagesAtom', () => {
    it('should return messages when no streaming message', () => {
      const messages = [
        { id: '1', role: 'user' as const, content: 'Hello', timestamp: new Date() },
      ];
      store.set(chatMessagesAtom, messages);

      const visible = store.get(visibleMessagesAtom);
      expect(visible).toEqual(messages);
    });

    it('should append streaming message when present', () => {
      const messages = [
        { id: '1', role: 'user' as const, content: 'Hello', timestamp: new Date() },
      ];
      const streamingMessage = {
        id: 'stream-1',
        role: 'assistant' as const,
        content: 'Streaming...',
        timestamp: new Date(),
      };

      store.set(chatMessagesAtom, messages);
      store.set(currentStreamingMessageAtom, streamingMessage);

      const visible = store.get(visibleMessagesAtom);
      expect(visible).toHaveLength(2);
      expect(visible[1]).toEqual(streamingMessage);
    });
  });

  describe('addMessageAtom', () => {
    it('should add user message', () => {
      const initialCount = store.get(chatMessagesAtom).length;

      store.set(addMessageAtom, {
        role: 'user',
        content: 'Test message',
      });

      const messages = store.get(chatMessagesAtom);
      expect(messages).toHaveLength(initialCount + 1);
      expect(messages[messages.length - 1].content).toBe('Test message');
      expect(messages[messages.length - 1].role).toBe('user');
    });

    it('should add assistant message', () => {
      const initialCount = store.get(chatMessagesAtom).length;

      store.set(addMessageAtom, {
        role: 'assistant',
        content: 'Response message',
      });

      const messages = store.get(chatMessagesAtom);
      expect(messages).toHaveLength(initialCount + 1);
      expect(messages[messages.length - 1].content).toBe('Response message');
      expect(messages[messages.length - 1].role).toBe('assistant');
    });

    it('should generate unique IDs', () => {
      store.set(addMessageAtom, { role: 'user', content: 'Message 1' });
      store.set(addMessageAtom, { role: 'user', content: 'Message 2' });

      const messages = store.get(chatMessagesAtom);
      const ids = messages.map(m => m.id);
      const uniqueIds = new Set(ids);

      expect(uniqueIds.size).toBe(ids.length);
    });
  });

  describe('clearMessagesAtom', () => {
    it('should clear all messages', () => {
      store.set(addMessageAtom, { role: 'user', content: 'Message 1' });
      store.set(addMessageAtom, { role: 'user', content: 'Message 2' });

      expect(store.get(chatMessagesAtom).length).toBeGreaterThan(1);

      store.set(clearMessagesAtom);

      const messages = store.get(chatMessagesAtom);
      expect(messages).toHaveLength(1);
      expect(messages[0].id).toBe('system-welcome');
    });

    it('should clear localStorage', () => {
      store.set(addMessageAtom, { role: 'user', content: 'Message 1' });

      expect(localStorage.getItem('ga-chat-messages')).toBeTruthy();

      store.set(clearMessagesAtom);

      const stored = localStorage.getItem('ga-chat-messages');
      const parsed = stored ? JSON.parse(stored) : [];
      expect(parsed).toHaveLength(1);
      expect(parsed[0].id).toBe('system-welcome');
    });
  });
});
