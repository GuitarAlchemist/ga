// IxqlPipeEngine — unit tests
// Tests the pure data transform pipeline across all 7 pipe step types.

import { describe, it, expect } from 'vitest';
import { executePipeline } from './IxqlPipeEngine';
import type { PipeStep } from './IxqlControlParser';

const SAMPLE_DATA = [
  { id: '1', name: 'alpha', severity: 3, type: 'policy', tags: ['a', 'b'] },
  { id: '2', name: 'beta', severity: 5, type: 'belief', tags: ['b', 'c'] },
  { id: '3', name: 'gamma', severity: 1, type: 'policy', tags: ['a'] },
  { id: '4', name: 'delta', severity: 5, type: 'belief', tags: ['c', 'd'] },
  { id: '5', name: 'epsilon', severity: 2, type: 'persona', tags: [] },
];

// ---------------------------------------------------------------------------
// FILTER
// ---------------------------------------------------------------------------

describe('PIPE FILTER', () => {
  it('filters rows matching a predicate', () => {
    const steps: PipeStep[] = [
      { type: 'filter', predicates: [{ field: 'type', operator: '=', value: 'policy' }] },
    ];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result).toHaveLength(2);
    expect(result.every(r => r.type === 'policy')).toBe(true);
  });

  it('filters with numeric comparison', () => {
    const steps: PipeStep[] = [
      { type: 'filter', predicates: [{ field: 'severity', operator: '>', value: 3 }] },
    ];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result).toHaveLength(2);
  });

  it('returns empty for no matches', () => {
    const steps: PipeStep[] = [
      { type: 'filter', predicates: [{ field: 'type', operator: '=', value: 'nonexistent' }] },
    ];
    expect(executePipeline(SAMPLE_DATA, steps)).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// SORT
// ---------------------------------------------------------------------------

describe('PIPE SORT', () => {
  it('sorts ascending by default', () => {
    const steps: PipeStep[] = [{ type: 'sort', field: 'severity', direction: 'ASC' }];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result[0].severity).toBe(1);
    expect(result[result.length - 1].severity).toBe(5);
  });

  it('sorts descending', () => {
    const steps: PipeStep[] = [{ type: 'sort', field: 'severity', direction: 'DESC' }];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result[0].severity).toBe(5);
    expect(result[result.length - 1].severity).toBe(1);
  });

  it('sorts strings alphabetically', () => {
    const steps: PipeStep[] = [{ type: 'sort', field: 'name', direction: 'ASC' }];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result[0].name).toBe('alpha');
  });

  it('does not mutate original array', () => {
    const original = [...SAMPLE_DATA];
    executePipeline(SAMPLE_DATA, [{ type: 'sort', field: 'severity', direction: 'DESC' }]);
    expect(SAMPLE_DATA[0].id).toBe(original[0].id);
  });
});

// ---------------------------------------------------------------------------
// LIMIT / SKIP
// ---------------------------------------------------------------------------

describe('PIPE LIMIT', () => {
  it('limits to N rows', () => {
    const result = executePipeline(SAMPLE_DATA, [{ type: 'limit', count: 2 }]);
    expect(result).toHaveLength(2);
  });

  it('returns all if limit > length', () => {
    const result = executePipeline(SAMPLE_DATA, [{ type: 'limit', count: 100 }]);
    expect(result).toHaveLength(5);
  });
});

describe('PIPE SKIP', () => {
  it('skips first N rows', () => {
    const result = executePipeline(SAMPLE_DATA, [{ type: 'skip', count: 3 }]);
    expect(result).toHaveLength(2);
    expect(result[0].id).toBe('4');
  });
});

// ---------------------------------------------------------------------------
// DISTINCT
// ---------------------------------------------------------------------------

describe('PIPE DISTINCT', () => {
  it('deduplicates by field', () => {
    const result = executePipeline(SAMPLE_DATA, [{ type: 'distinct', field: 'type' }]);
    expect(result).toHaveLength(3); // policy, belief, persona
  });

  it('deduplicates by severity', () => {
    const result = executePipeline(SAMPLE_DATA, [{ type: 'distinct', field: 'severity' }]);
    expect(result).toHaveLength(4); // 1, 2, 3, 5
  });
});

// ---------------------------------------------------------------------------
// FLATTEN
// ---------------------------------------------------------------------------

describe('PIPE FLATTEN', () => {
  it('expands array fields into rows', () => {
    const result = executePipeline(SAMPLE_DATA, [{ type: 'flatten', field: 'tags' }]);
    // alpha:2 + beta:2 + gamma:1 + delta:2 + epsilon:0(kept as-is) = 7+1 = 8
    // Wait: epsilon has empty array [], so it stays as one row with tags=[]
    // Actually FLATTEN on empty array: val is [] which is Array, loop produces 0 items
    // So epsilon disappears. Total: 2+2+1+2+0 = 7
    expect(result).toHaveLength(7);
  });

  it('respects MAX_FLATTEN_ROWS cap', () => {
    const hugeData = [{ id: '1', items: Array.from({ length: 20000 }, (_, i) => i) }];
    const result = executePipeline(hugeData, [{ type: 'flatten', field: 'items' }]);
    expect(result.length).toBeLessThanOrEqual(10000);
  });
});

// ---------------------------------------------------------------------------
// GROUP BY
// ---------------------------------------------------------------------------

describe('PIPE GROUP BY', () => {
  it('groups by field with COUNT', () => {
    const steps: PipeStep[] = [
      { type: 'group', byField: 'type', aggregates: [{ fn: 'COUNT', field: null, alias: 'count' }] },
    ];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result).toHaveLength(3); // policy, belief, persona
    const policyGroup = result.find(r => r.type === 'policy');
    expect(policyGroup?.count).toBe(2);
  });

  it('groups with SUM aggregate', () => {
    const steps: PipeStep[] = [
      { type: 'group', byField: 'type', aggregates: [
        { fn: 'COUNT', field: null, alias: 'count' },
        { fn: 'SUM', field: 'severity', alias: 'sum_severity' },
      ]},
    ];
    const result = executePipeline(SAMPLE_DATA, steps);
    const beliefGroup = result.find(r => r.type === 'belief');
    expect(beliefGroup?.sum_severity).toBe(10); // 5 + 5
  });

  it('groups with AVG aggregate', () => {
    const steps: PipeStep[] = [
      { type: 'group', byField: 'type', aggregates: [
        { fn: 'AVG', field: 'severity', alias: 'avg_severity' },
      ]},
    ];
    const result = executePipeline(SAMPLE_DATA, steps);
    const policyGroup = result.find(r => r.type === 'policy');
    expect(policyGroup?.avg_severity).toBe(2); // (3+1)/2
  });
});

// ---------------------------------------------------------------------------
// Composed pipelines
// ---------------------------------------------------------------------------

describe('composed pipelines', () => {
  it('FILTER then SORT then LIMIT', () => {
    const steps: PipeStep[] = [
      { type: 'filter', predicates: [{ field: 'type', operator: '=', value: 'belief' }] },
      { type: 'sort', field: 'severity', direction: 'DESC' },
      { type: 'limit', count: 1 },
    ];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result).toHaveLength(1);
    expect(result[0].type).toBe('belief');
  });

  it('GROUP BY then SORT', () => {
    const steps: PipeStep[] = [
      { type: 'group', byField: 'type', aggregates: [{ fn: 'COUNT', field: null, alias: 'count' }] },
      { type: 'sort', field: 'count', direction: 'DESC' },
    ];
    const result = executePipeline(SAMPLE_DATA, steps);
    expect(result[0].count).toBe(2); // policy or belief (both have 2)
  });

  it('empty data through pipeline returns empty', () => {
    const steps: PipeStep[] = [
      { type: 'filter', predicates: [{ field: 'x', operator: '=', value: 1 }] },
      { type: 'sort', field: 'x', direction: 'ASC' },
    ];
    expect(executePipeline([], steps)).toHaveLength(0);
  });
});
