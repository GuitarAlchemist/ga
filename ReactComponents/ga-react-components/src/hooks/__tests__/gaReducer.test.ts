import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useGAAgent } from '../useGAAgent';

// Mock @ag-ui/client so no real network calls are made
vi.mock('@ag-ui/client', () => {
  const abortRun = vi.fn();
  const addMessage = vi.fn();
  const runAgent = vi.fn(() => Promise.resolve());

  class HttpAgent {
    constructor(_opts: { url: string }) {}
    abortRun = abortRun;
    addMessage = addMessage;
    runAgent = runAgent;
  }

  return { HttpAgent };
});

describe('useGAAgent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('initial state has analysisPhase idle and empty diatonicChords', () => {
    const { result } = renderHook(() => useGAAgent('http://localhost:7001/agent'));
    const { state } = result.current;

    expect(state.analysisPhase).toBe('idle');
    expect(state.diatonicChords).toHaveLength(0);
    expect(state.key).toBeNull();
    expect(state.mode).toBeNull();
    expect(state.lastError).toBeNull();
  });

  it('initial isStreaming is false', () => {
    const { result } = renderHook(() => useGAAgent('http://localhost:7001/agent'));
    expect(result.current.isStreaming).toBe(false);
  });

  it('initial messages array is empty', () => {
    const { result } = renderHook(() => useGAAgent('http://localhost:7001/agent'));
    expect(result.current.messages).toHaveLength(0);
  });

  it('abort does not throw when called before run', () => {
    const { result } = renderHook(() => useGAAgent('http://localhost:7001/agent'));
    expect(() => result.current.abort()).not.toThrow();
    // isStreaming remains false after abort
    expect(result.current.isStreaming).toBe(false);
  });

  it('exposes run and abort as functions', () => {
    const { result } = renderHook(() => useGAAgent('http://localhost:7001/agent'));
    expect(typeof result.current.run).toBe('function');
    expect(typeof result.current.abort).toBe('function');
  });
});
