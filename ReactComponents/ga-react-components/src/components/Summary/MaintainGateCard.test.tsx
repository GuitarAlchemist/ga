// MaintainGateCard.test — renders the advisory maintain-gate tile (ga#446)
// against the real federated snapshot shape. Asserts the four things the issue
// requires: hexavalent status chip, advisory badge, per-signal breakdown, and
// an honest freshness/stale read (never a fake green).

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { MaintainGateCard } from './MaintainGateCard';

const SNAPSHOT = {
  schema_version: 'maintain-verdict-snapshot.v0.1',
  domain: 'maintain-gate',
  emitted_at: '2026-06-21T00:41:51.122362635+00:00',
  metric_name: 'maintain_yield_delta',
  metric_value: 0.0,
  oracle_status: 'warn',
  summary: 'metric evidence missing — cannot decide',
  advisory: true,
  status: 'U',
  decision: 'escalate',
  signals: [
    { lens: 'metric', status: 'unknown', detail: 'no externally-derived metric evidence' },
    { lens: 'guardrail', status: 'unknown', detail: 'chatbot gate: skipped (0 regression(s))' },
    { lens: 'convergence', status: 'unknown', detail: 'no loop data (advisory)' },
    { lens: 'drift', status: 'unknown', detail: 'no query embeddings (advisory)' },
  ],
  maintain_trend: { total: 0, accepts: 0, rejects: 0, escalates: 0, reward_hacks: 0, latest_status: null },
};

function mockFetch(body: unknown, ok = true) {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
    ok,
    status: ok ? 200 : 404,
    json: async () => body,
  }));
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('MaintainGateCard', () => {
  it('renders the hexavalent status chip, advisory badge, summary, and per-signal breakdown', async () => {
    mockFetch({ generated_at: '2026-06-21T01:00:00Z', snapshot: SNAPSHOT, source: 'live', age_hours: 0.3, stale: false });
    render(<MaintainGateCard />);

    const chip = await screen.findByTestId('maintain-status-chip');
    expect(chip.textContent).toContain('U');
    expect(chip.textContent).toMatch(/escalate/i);

    expect(screen.getByTestId('maintain-advisory-badge').textContent).toMatch(/advisory/i);
    expect(screen.getByText('metric evidence missing — cannot decide')).toBeInTheDocument();

    // All four sub-signals present.
    for (const lens of ['metric', 'guardrail', 'convergence', 'drift']) {
      expect(screen.getByTestId(`maintain-signal-${lens}`)).toBeInTheDocument();
    }

    // Oracle traffic light reflects warn.
    expect(screen.getByTestId('maintain-oracle-dot').getAttribute('data-oracle')).toBe('warn');
  });

  it('surfaces STALE rather than a fake green when the snapshot is old', async () => {
    mockFetch({ generated_at: '2026-06-21T01:00:00Z', snapshot: SNAPSHOT, source: 'live', age_hours: 72, stale: true });
    render(<MaintainGateCard />);
    const badge = await screen.findByTestId('maintain-freshness-badge');
    expect(badge.textContent).toMatch(/stale/i);
  });

  it('renders a quiet hint (not blank, not green) on fetch error', async () => {
    mockFetch({ error: 'not found' }, false);
    render(<MaintainGateCard />);
    await waitFor(() => {
      expect(screen.getByTestId('maintain-gate-card').textContent).toMatch(/no maintain verdict yet/i);
    });
    expect(screen.queryByTestId('maintain-status-chip')).toBeNull();
  });
});
