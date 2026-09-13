/**
 * AG-UI chat service — replaces the ad-hoc SSE parsers in chatApi.ts / chatService.ts.
 *
 * Posts to POST /api/chatbot/agui/stream, parses the standardised AG-UI event stream,
 * and routes each event type to the appropriate callback.
 * No framework dependency — works inside Jotai atoms, React effects, or plain code.
 */

import type { ChordInContext } from 'ga-react-components/src/types/agent-state';

// ── Minimal AG-UI event types we handle ─────────────────────────────────────

interface AgUiBaseEvent {
  type: string;
}

interface AgUiTextContentEvent extends AgUiBaseEvent {
  type: 'TEXT_MESSAGE_CONTENT';
  messageId: string;
  delta: string;
}

interface AgUiCustomEvent extends AgUiBaseEvent {
  type: 'CUSTOM';
  name: string;
  value: unknown;
}

interface AgUiRunErrorEvent extends AgUiBaseEvent {
  type: 'RUN_ERROR';
  message: string;
  code: string;
}

type AgUiEvent = AgUiTextContentEvent | AgUiCustomEvent | AgUiRunErrorEvent | AgUiBaseEvent;

// ── Callbacks ────────────────────────────────────────────────────────────────

export interface AgUiStreamCallbacks {
  onChunk?: (delta: string) => void;
  onDiatonicChords?: (chords: ChordInContext[]) => void;
  onComplete?: () => void;
  onError?: (message: string) => void;
}

// ── Input model (mirrors C# RunAgentInput) ───────────────────────────────────

export interface AgUiMessage {
  role: string;
  content: string;
  id?: string;
}

export interface AgUiRunInput {
  threadId: string;
  runId: string;
  messages: AgUiMessage[];
  state?: unknown;
}

// Chat messages persist in localStorage, so bound the context sent with each run.
export const MAX_AG_UI_HISTORY_TURNS = 12;

/** Prior user/assistant turns, oldest first, as AG-UI messages. */
export function toAgUiHistory(
  messages: ReadonlyArray<{ id: string; role: string; content: string }>,
): AgUiMessage[] {
  return messages
    .filter(m => (m.role === 'user' || m.role === 'assistant') && m.content.trim() !== '')
    .slice(-MAX_AG_UI_HISTORY_TURNS)
    .map(({ role, content, id }) => ({ role, content, id }));
}

/** AG-UI sends the thread; GaApi treats the last user message as the current turn. */
export function buildAgUiRunInput(
  userMessage: string,
  history: readonly AgUiMessage[],
  now: number = Date.now(),
): AgUiRunInput {
  return {
    threadId: `thread_${now}`,
    runId: `run_${now}`,
    messages: [...history, { role: 'user', content: userMessage, id: `msg_${now}` }],
  };
}

// ── Parser ───────────────────────────────────────────────────────────────────

function* parseAgUiFrames(chunk: string): Generator<AgUiEvent> {
  const frames = chunk.split('\n\n');
  for (const frame of frames) {
    const dataLine = frame.split('\n').find(l => l.startsWith('data:'));
    if (!dataLine) continue;
    const payload = dataLine.slice(5).trim();
    if (!payload) continue;
    try {
      yield JSON.parse(payload) as AgUiEvent;
    } catch {
      // malformed frame — skip
    }
  }
}

// ── Main stream function ──────────────────────────────────────────────────────

export async function streamAgUiChat(
  baseUrl: string,
  userMessage: string,
  history: readonly AgUiMessage[],
  callbacks: AgUiStreamCallbacks,
  signal?: AbortSignal,
): Promise<void> {
  const input = buildAgUiRunInput(userMessage, history);

  const base = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  const response = await fetch(`${base}/api/chatbot/agui/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  });

  if (!response.ok) {
    throw new Error(`AG-UI stream failed: ${response.status} ${response.statusText}`);
  }

  if (!response.body) {
    throw new Error('Response body unavailable');
  }

  const reader  = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer    = '';

  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      // Keep incomplete frame in buffer
      const cut    = buffer.lastIndexOf('\n\n');
      if (cut === -1) continue;
      const ready  = buffer.slice(0, cut + 2);
      buffer       = buffer.slice(cut + 2);

      for (const event of parseAgUiFrames(ready)) {
        switch (event.type) {
          case 'TEXT_MESSAGE_CONTENT':
            callbacks.onChunk?.((event as AgUiTextContentEvent).delta);
            break;
          case 'CUSTOM': {
            const custom = event as AgUiCustomEvent;
            if (custom.name === 'ga:diatonic' && Array.isArray(custom.value)) {
              callbacks.onDiatonicChords?.(custom.value as ChordInContext[]);
            }
            break;
          }
          case 'RUN_FINISHED':
            callbacks.onComplete?.();
            return;
          case 'RUN_ERROR':
            callbacks.onError?.((event as AgUiRunErrorEvent).message);
            return;
        }
      }
    }
  } finally {
    reader.releaseLock();
  }

  callbacks.onComplete?.();
}
