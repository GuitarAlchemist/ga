import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createStore } from 'jotai';
import { buildAgUiRunInput, toAgUiHistory, MAX_AG_UI_HISTORY_TURNS } from '../services/agUiChatService';
import { chatMessagesAtom, sendMessageAtom, type ChatMessage } from '../store/chatAtoms';

const message = (role: ChatMessage['role'], content: string, id = `${role}-${content}`): ChatMessage => ({
  id,
  role,
  content,
  timestamp: new Date(0),
});

describe('toAgUiHistory', () => {
  it('keeps user and assistant turns in order and drops system and blank messages', () => {
    const history = toAgUiHistory([
      message('system', 'Welcome'),
      message('user', 'I am playing Dm7.'),
      message('assistant', '   '),
      message('assistant', 'Its notes are D F A C.'),
    ]);

    expect(history).toEqual([
      { role: 'user', content: 'I am playing Dm7.', id: 'user-I am playing Dm7.' },
      { role: 'assistant', content: 'Its notes are D F A C.', id: 'assistant-Its notes are D F A C.' },
    ]);
  });

  it('keeps only the most recent turns', () => {
    const messages = Array.from({ length: MAX_AG_UI_HISTORY_TURNS + 3 }, (_, i) => message('user', `turn ${i}`));

    const history = toAgUiHistory(messages);

    expect(history).toHaveLength(MAX_AG_UI_HISTORY_TURNS);
    expect(history[0].content).toBe('turn 3');
    expect(history[history.length - 1].content).toBe(`turn ${MAX_AG_UI_HISTORY_TURNS + 2}`);
  });
});

describe('buildAgUiRunInput', () => {
  it('sends prior turns followed by the current user message', () => {
    const input = buildAgUiRunInput('Which scale fits?', [{ role: 'user', content: 'I am playing Dm7.' }], 42);

    expect(input.messages).toEqual([
      { role: 'user', content: 'I am playing Dm7.' },
      { role: 'user', content: 'Which scale fits?', id: 'msg_42' },
    ]);
    expect(input.threadId).toBe('thread_42');
    expect(input.runId).toBe('run_42');
  });
});

describe('sendMessageAtom', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    localStorage.clear();
    fetchMock.mockReset();
    fetchMock.mockImplementation(() =>
      Promise.resolve(new Response('data: {"type":"RUN_FINISHED"}\n\n', { status: 200 })),
    );
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('posts the prior conversation before the new message', async () => {
    const store = createStore();
    store.set(chatMessagesAtom, [
      message('system', 'Welcome', 'system-welcome'),
      message('user', 'I am playing Dm7.'),
      message('assistant', 'Its notes are D F A C.'),
    ]);

    await store.set(sendMessageAtom, 'Which scale fits?');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    const body = JSON.parse(init.body as string) as { messages: Array<{ role: string; content: string }> };
    expect(body.messages.map(({ role, content }) => [role, content])).toEqual([
      ['user', 'I am playing Dm7.'],
      ['assistant', 'Its notes are D F A C.'],
      ['user', 'Which scale fits?'],
    ]);
  });
});
