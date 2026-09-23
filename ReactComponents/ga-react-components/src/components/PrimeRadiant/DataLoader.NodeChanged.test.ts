// NodeChanged wiring — unit tests
//
// GovernanceHub.BroadcastNodeChanged sends { nodeId, health, healthStatus,
// color, timestamp }. updateNodeHealth indexes the fresh nodes by `n.id`, so a
// payload used as-is never matches an existing node and the health update is
// dropped in silence.

import { describe, it, expect } from 'vitest';
import { nodeFromNodeChanged, updateNodeHealth } from './DataLoader';
import type { GovernanceNode } from './types';

const existing = (): GovernanceNode[] => [
  {
    id: 'ix.governance.constitution',
    name: 'Constitution',
    type: 'constitution',
    description: '',
    color: '#888888',
    health: { resilienceScore: 0.9, lolliCount: 0, ergolCount: 4 },
    healthStatus: 'healthy',
  } as GovernanceNode,
];

const payload = {
  nodeId: 'ix.governance.constitution',
  health: { resilienceScore: 0.2, lolliCount: 3, ergolCount: 1 },
  healthStatus: 'error' as const,
  color: '#ff0000',
  timestamp: '2026-09-17T00:00:00Z',
};

describe('nodeFromNodeChanged', () => {
  it('renames nodeId to id so the node can be looked up', () => {
    const node = nodeFromNodeChanged(payload);

    expect(node?.id).toBe('ix.governance.constitution');
    expect(node?.health).toEqual(payload.health);
  });

  it('returns null when the payload carries no node id', () => {
    expect(nodeFromNodeChanged({ nodeId: '' })).toBeNull();
    expect(nodeFromNodeChanged(undefined)).toBeNull();
  });
});

describe('updateNodeHealth fed from NodeChanged', () => {
  it('applies the change (the raw payload cast to a node applies nothing)', () => {
    // What the handler used to do: cast the payload straight to GovernanceNode.
    const raw = updateNodeHealth(existing(), [payload as unknown as GovernanceNode]);
    expect(raw.changed).toBe(false);
    expect(raw.updated).toEqual([]);

    // What it does now.
    const nodes = existing();
    const mapped = updateNodeHealth(nodes, [nodeFromNodeChanged(payload)!]);
    expect(mapped.changed).toBe(true);
    expect(mapped.updated).toEqual(['ix.governance.constitution']);
    expect(nodes[0].health?.lolliCount).toBe(3);
    expect(nodes[0].healthStatus).toBe('error');
  });

  it('applies a server status change when metrics stay the same and color is omitted', () => {
    const nodes = existing();
    const changed = updateNodeHealth(nodes, [{
      id: nodes[0].id,
      health: { ...nodes[0].health! },
      healthStatus: 'contradictory',
    } as GovernanceNode]);

    expect(changed).toEqual({
      changed: true,
      updated: ['ix.governance.constitution'],
    });
    expect(nodes[0].healthStatus).toBe('contradictory');
    expect(nodes[0].color).toBe('#FF44FF');
  });

  it('applies a server color change when metrics and status stay the same', () => {
    const nodes = existing();
    const changed = updateNodeHealth(nodes, [{
      ...nodes[0],
      health: { ...nodes[0].health! },
      color: '#FF44FF',
    }]);

    expect(changed).toEqual({
      changed: true,
      updated: ['ix.governance.constitution'],
    });
    expect(nodes[0].healthStatus).toBe('healthy');
    expect(nodes[0].color).toBe('#FF44FF');
  });
});
