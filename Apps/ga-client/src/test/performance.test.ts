// @ts-nocheck
/**
 * Performance tests for chat interface
 * Tests handling of large message histories
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { createStore } from 'jotai';
import { chatMessagesAtom, visibleMessagesAtom, addMessageAtom } from '../store/chatAtoms';

describe('Chat Performance', () => {
  let store: ReturnType<typeof createStore>;

  beforeEach(() => {
    store = createStore();
  });

  it('should handle 100 messages efficiently', () => {
    const startTime = performance.now();

    // Add 100 messages
    for (let i = 0; i < 100; i++) {
      store.set(addMessageAtom, {
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}: This is a test message with some content to simulate real chat.`,
      });
    }

    const endTime = performance.now();
    const duration = endTime - startTime;

    expect(store.get(chatMessagesAtom).length).toBe(101); // 100 + welcome message
    expect(duration).toBeLessThan(1000); // Should complete in less than 1 second
  });

  it('should handle 1000 messages efficiently', () => {
    const startTime = performance.now();

    // Add 1000 messages
    for (let i = 0; i < 1000; i++) {
      store.set(addMessageAtom, {
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}: This is a test message with some content to simulate real chat.`,
      });
    }

    const endTime = performance.now();
    const duration = endTime - startTime;

    expect(store.get(chatMessagesAtom).length).toBe(1001); // 1000 + welcome message
    expect(duration).toBeLessThan(5000); // Should complete in less than 5 seconds
  });

  it('should filter visible messages efficiently with large history', () => {
    // Add 1000 messages
    for (let i = 0; i < 1000; i++) {
      store.set(addMessageAtom, {
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}`,
      });
    }

    const startTime = performance.now();
    const visibleMessages = store.get(visibleMessagesAtom);
    const endTime = performance.now();
    const duration = endTime - startTime;

    expect(visibleMessages.length).toBeGreaterThan(0);
    expect(duration).toBeLessThan(100); // Should be very fast (< 100ms)
  });

  it('should handle messages with large content', () => {
    const largeContent = 'A'.repeat(10000); // 10KB of text

    const startTime = performance.now();

    for (let i = 0; i < 100; i++) {
      store.set(addMessageAtom, {
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: largeContent,
      });
    }

    const endTime = performance.now();
    const duration = endTime - startTime;

    expect(store.get(chatMessagesAtom).length).toBe(101); // 100 + welcome message
    expect(duration).toBeLessThan(2000); // Should complete in less than 2 seconds
  });

  it('should handle mixed content types efficiently', () => {
    const contentTypes = [
      'Simple text message',
      '```typescript\nconst code = "example";\n```',
      '```vextab\ntabstave notation=true\nnotes :q 4/4 5/5\n```',
      '# Heading\n\n**Bold** and *italic* text',
      '| Column 1 | Column 2 |\n|----------|----------|\n| Data 1   | Data 2   |',
    ];

    const startTime = performance.now();

    for (let i = 0; i < 200; i++) {
      store.set(addMessageAtom, {
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: contentTypes[i % contentTypes.length],
      });
    }

    const endTime = performance.now();
    const duration = endTime - startTime;

    expect(store.get(chatMessagesAtom).length).toBe(201); // 200 + welcome message
    expect(duration).toBeLessThan(2000); // Should complete in less than 2 seconds
  });

  it('should maintain performance with localStorage persistence', () => {
    // Clear localStorage
    localStorage.clear();

    const startTime = performance.now();

    // Add 500 messages (localStorage will persist each one)
    for (let i = 0; i < 500; i++) {
      store.set(addMessageAtom, {
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}`,
      });
    }

    const endTime = performance.now();
    const duration = endTime - startTime;

    // Verify localStorage has the data
    const storedData = localStorage.getItem('ga-chat-messages');
    expect(storedData).toBeTruthy();

    const parsedData = JSON.parse(storedData!);
    expect(parsedData.length).toBe(501); // 500 + welcome message
    expect(duration).toBeLessThan(5000); // Should complete in less than 5 seconds
  });

  it('should verify localStorage persistence works', () => {
    // Clear localStorage
    localStorage.clear();

    // Add messages
    for (let i = 0; i < 100; i++) {
      store.set(addMessageAtom, {
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}`,
      });
    }

    // Verify localStorage has the data
    const storedData = localStorage.getItem('ga-chat-messages');
    expect(storedData).toBeTruthy();

    const parsedData = JSON.parse(storedData!);
    expect(parsedData.length).toBe(101); // 100 + welcome message

    // Verify data structure
    expect(parsedData[0]).toHaveProperty('id');
    expect(parsedData[0]).toHaveProperty('role');
    expect(parsedData[0]).toHaveProperty('content');
    expect(parsedData[0]).toHaveProperty('timestamp');
  });
});


