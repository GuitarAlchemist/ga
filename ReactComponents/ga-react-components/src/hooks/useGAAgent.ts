import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import { HttpAgent } from '@ag-ui/client';
import type { Message } from '@ag-ui/client';
import { EMPTY_GA_STATE, type ChordInContext, type GaAgentState, type ScaleNote } from '../types/agent-state';

// ── State management ────────────────────────────────────────────────────────

type GaAction =
  | { type: 'RESET' }
  | { type: 'SNAPSHOT'; payload: Partial<GaAgentState> }
  | { type: 'SET_DIATONIC'; chords: readonly ChordInContext[] }
  | { type: 'SET_SCALE'; notes: readonly ScaleNote[] }
  | { type: 'SET_PHASE'; phase: GaAgentState['analysisPhase'] }
  | { type: 'SET_ERROR'; error: string | null };

function gaReducer(state: GaAgentState, action: GaAction): GaAgentState {
  switch (action.type) {
    case 'RESET':        return EMPTY_GA_STATE;
    case 'SNAPSHOT':     return { ...state, ...action.payload };
    case 'SET_DIATONIC': return { ...state, diatonicChords: action.chords };
    case 'SET_SCALE':    return { ...state, scaleNotes: action.notes };
    case 'SET_PHASE':    return { ...state, analysisPhase: action.phase };
    case 'SET_ERROR':    return { ...state, lastError: action.error };
    default: return state;
  }
}

// ── Public interface ────────────────────────────────────────────────────────

export interface UseGAAgentReturn {
  state: GaAgentState;
  messages: Message[];
  isStreaming: boolean;
  run: (userMessage: string, threadId?: string) => Promise<void>;
  abort: () => void;
}

// ── Hook ────────────────────────────────────────────────────────────────────

export function useGAAgent(endpointUrl: string): UseGAAgentReturn {
  const [state, dispatch]  = useReducer(gaReducer, EMPTY_GA_STATE);
  const [messages, setMessages] = useState<Message[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const agentRef = useRef<HttpAgent | null>(null);

  // Create agent once per endpoint URL
  useEffect(() => {
    const agent = new HttpAgent({ url: endpointUrl });
    agentRef.current = agent;
    return () => {
      agent.abortRun();
    };
  }, [endpointUrl]);

  const run = useCallback(async (userMessage: string, threadId?: string) => {
    const agent = agentRef.current;
    if (!agent) return;

    setIsStreaming(true);
    dispatch({ type: 'RESET' });

    // Add user message to thread
    agent.addMessage({ role: 'user', content: userMessage, id: crypto.randomUUID() } as Message);

    await agent.runAgent(
      { runId: threadId },
      {
        onRunStartedEvent() {
          dispatch({ type: 'SET_PHASE', phase: 'identifying' });
        },

        onStateSnapshotEvent({ event }) {
          const snap = event.snapshot as Partial<GaAgentState>;
          dispatch({ type: 'SNAPSHOT', payload: snap });
        },

        onStateDeltaEvent({ state: patchedState }) {
          // SDK has already applied the JSON patch; use the resulting state
          const next = patchedState as Partial<GaAgentState>;
          dispatch({ type: 'SNAPSHOT', payload: next });
        },

        onCustomEvent({ event }) {
          const name  = (event as { name?: string }).name;
          const value = (event as { value?: unknown }).value;
          if (name === 'ga:diatonic' && Array.isArray(value)) {
            dispatch({ type: 'SET_DIATONIC', chords: value as ChordInContext[] });
          } else if (name === 'ga:scale' && Array.isArray(value)) {
            dispatch({ type: 'SET_SCALE', notes: value as ScaleNote[] });
          }
        },

        onTextMessageStartEvent() {
          // begin accumulating assistant message
        },

        onTextMessageEndEvent({ textMessageBuffer }) {
          const assistantMsg: Message = {
            role:    'assistant',
            content: textMessageBuffer,
            id:      crypto.randomUUID(),
          } as Message;
          setMessages(prev => [...prev, assistantMsg]);
        },

        onRunFinishedEvent() {
          dispatch({ type: 'SET_PHASE', phase: 'complete' });
          setIsStreaming(false);
        },

        onRunFailed({ error }) {
          dispatch({ type: 'SET_ERROR', error: error.message });
          setIsStreaming(false);
          return { stopPropagation: true };
        },
      }
    );
  }, []);

  const abort = useCallback(() => {
    agentRef.current?.abortRun();
    setIsStreaming(false);
  }, []);

  return { state, messages, isStreaming, run, abort };
}
