// src/components/PrimeRadiant/DynamicPanel.test.tsx
// An expanded row of the list-detail layout must stay the same row when the
// list is filtered or a poll returns the rows in another order.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';

const rows = [
  { id: 'a', name: 'Alpha', kind: 'x', detail: 'alpha detail' },
  { id: 'b', name: 'Beta', kind: 'y', detail: 'beta detail' },
];

let pollCallback: ((data: unknown[]) => void) | null = null;

vi.mock('./DataFetcher', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./DataFetcher')>();
  return {
    ...actual,
    resolve: vi.fn(async () => rows),
    poll: vi.fn((_source: string, _ms: number, cb: (data: unknown[]) => void) => {
      pollCallback = cb;
      return () => { pollCallback = null; };
    }),
  };
});

import { DynamicPanel, type DynamicPanelDefinition } from './DynamicPanel';

const definition = (filter: DynamicPanelDefinition['filter']): DynamicPanelDefinition => ({
  id: 'p',
  label: 'Panel',
  source: 'graph://nodes',
  layout: 'list-detail',
  wherePredicates: [],
  showFields: ['name', 'detail'],
  filter,
});

describe('DynamicPanel list-detail expansion', () => {
  beforeEach(() => {
    pollCallback = null;
  });

  it('keeps the same row expanded when a filter removes the rows above it', async () => {
    render(<DynamicPanel definition={definition({ field: 'kind', mode: 'chips' })} />);
    fireEvent.click(await screen.findByText('Beta'));
    expect(screen.getByText('beta detail')).toBeInTheDocument();

    // Show only kind "y": Beta moves from position 1 to position 0.
    fireEvent.click(screen.getByRole('button', { name: 'y' }));

    expect(screen.getByText('beta detail')).toBeInTheDocument();
  });

  it('keeps the same row expanded when a poll reorders the rows', async () => {
    render(<DynamicPanel definition={definition(null)} />);
    fireEvent.click(await screen.findByText('Beta'));
    expect(screen.getByText('beta detail')).toBeInTheDocument();
    expect(screen.queryByText('alpha detail')).not.toBeInTheDocument();

    act(() => pollCallback?.([rows[1], rows[0]]));

    expect(screen.getByText('beta detail')).toBeInTheDocument();
    expect(screen.queryByText('alpha detail')).not.toBeInTheDocument();
  });
});
