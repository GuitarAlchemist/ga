import { beforeEach, describe, expect, it, vi } from 'vitest';

const signalRState = vi.hoisted(() => ({
  handlers: new Map<string, (data: unknown) => void>(),
  connection: {
    on: vi.fn((event: string, handler: (data: unknown) => void) => {
      signalRState.handlers.set(event, handler);
    }),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
    start: vi.fn(async () => undefined),
    invoke: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
    state: 'Connected',
  },
}));

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return signalRState.connection; }
  },
  HubConnectionState: { Connected: 'Connected' },
  LogLevel: { Warning: 3 },
}));

import { startLivePolling } from './DataLoader';
import type { GovernanceGraph } from './types';

const fullGraph = (): GovernanceGraph => ({
  nodes: [
    {
      id: 'ga.node.one',
      name: 'One',
      type: 'policy',
      description: '',
      color: '#33CC66',
      health: { resilienceScore: 0.9, lolliCount: 0, ergolCount: 4 },
      healthStatus: 'healthy',
    },
    {
      id: 'ga.node.two',
      name: 'Two',
      type: 'policy',
      description: '',
      color: '#888888',
      health: { resilienceScore: 0.5, lolliCount: 0, ergolCount: 2 },
      healthStatus: 'unknown',
    },
  ],
  edges: [
    { id: 'one-two', source: 'ga.node.one', target: 'ga.node.two', type: 'contains' },
  ],
  globalHealth: { resilienceScore: 0.7, lolliCount: 0, ergolCount: 6 },
  timestamp: '2026-09-20T00:00:00Z',
});

beforeEach(() => {
  signalRState.handlers.clear();
  vi.clearAllMocks();
});

describe('startLivePolling NodeChanged seam', () => {
  it('preserves the complete graph when one node changes', async () => {
    const updates: GovernanceGraph[] = [];
    const handle = startLivePolling({
      url: '/api/governance',
      hubUrl: '/hubs/governance',
      onUpdate: graph => updates.push(structuredClone(graph)),
    });

    await vi.waitFor(() => expect(signalRState.connection.invoke).toHaveBeenCalledWith('Subscribe'));

    signalRState.handlers.get('GraphUpdate')?.(fullGraph());
    signalRState.handlers.get('NodeChanged')?.({
      nodeId: 'ga.node.one',
      health: { resilienceScore: 0.2, lolliCount: 3, ergolCount: 1 },
      healthStatus: 'error',
      color: '#FF4444',
      timestamp: '2026-09-20T00:00:01Z',
    });

    expect(updates).toHaveLength(2);
    expect(updates[1].nodes).toHaveLength(2);
    expect(updates[1].edges).toEqual(fullGraph().edges);
    expect(updates[1].globalHealth).toEqual(fullGraph().globalHealth);
    expect(updates[1].timestamp).toBe('2026-09-20T00:00:01Z');
    expect(updates[1].nodes.find(node => node.id === 'ga.node.one')).toMatchObject({
      health: { resilienceScore: 0.2, lolliCount: 3, ergolCount: 1 },
      healthStatus: 'error',
      color: '#FF4444',
    });
    expect(updates[1].nodes.find(node => node.id === 'ga.node.two')?.name).toBe('Two');

    handle.stop();
  });
});
