// Pure-function tests for the heatmap aggregator + bucketizer.
// The full component (fetch + MUI render) is covered by the playwright spec
// over in tests/dashboard/value-complexity-heatmap.spec.ts.

import { describe, it, expect } from 'vitest';
import { aggregate, bucketize } from './ValueComplexityHeatmap';
import type { Annotation } from './types';

function mkAnnotation(partial: Partial<Annotation>): Annotation {
  return {
    schema_version: 2,
    id: `sha256:${'0'.repeat(64)}`,
    kind: 'business-value',
    claim: 'test claim',
    truth_value: 'T',
    certainty: 'manually-reviewed',
    confidence: 0.9,
    source: { author: 'human' },
    location: { path: 'src/foo.ts', line_start: 1, line_end: 1 },
    created_at: '2026-05-24T00:00:00Z',
    updated_at: '2026-05-24T00:00:00Z',
    ...partial,
  } as Annotation;
}

describe('bucketize', () => {
  it('assigns high-value + high-complexity to REFACTOR FIRST', () => {
    expect(bucketize(0.9, 0.9)).toBe('refactor-first');
    expect(bucketize(0.5, 0.5)).toBe('refactor-first'); // boundary inclusive
  });

  it('assigns low-value + high-complexity to DELETE CANDIDATE', () => {
    expect(bucketize(0.1, 0.8)).toBe('delete-candidate');
  });

  it('assigns high-value + low-complexity to KEEP STABLE', () => {
    expect(bucketize(0.9, 0.2)).toBe('keep-stable');
  });

  it('assigns low-value + low-complexity to MAINTENANCE BURDEN', () => {
    expect(bucketize(0, 0)).toBe('maintenance-burden');
    expect(bucketize(0.4, 0.4)).toBe('maintenance-burden');
  });
});

describe('aggregate', () => {
  it('returns empty list for no annotations', () => {
    expect(aggregate([])).toEqual([]);
  });

  it('scores a business-value T annotation as the value axis', () => {
    const files = aggregate([
      mkAnnotation({
        kind: 'business-value',
        truth_value: 'T',
        confidence: 0.95,
        location: { path: 'src/engine.ts', line_start: 10, line_end: 10 },
      }),
    ]);
    expect(files).toHaveLength(1);
    expect(files[0].path).toBe('src/engine.ts');
    expect(files[0].value_score).toBe(0.95);
    expect(files[0].complexity_score).toBe(0);
  });

  it('scores a smell annotation as the complexity axis', () => {
    const files = aggregate([
      mkAnnotation({
        kind: 'smell',
        truth_value: 'D',
        confidence: 0.7,
        location: { path: 'src/hairy.ts', line_start: 1, line_end: 1 },
      }),
    ]);
    expect(files).toHaveLength(1);
    expect(files[0].value_score).toBe(0);
    expect(files[0].complexity_score).toBe(0.7);
  });

  it('ignores business-value annotations with truth_value=F or U', () => {
    // Per spec: only T or P count toward value score.
    const files = aggregate([
      mkAnnotation({
        kind: 'business-value',
        truth_value: 'F',
        confidence: 0.99,
        location: { path: 'src/a.ts', line_start: 1, line_end: 1 },
      }),
      mkAnnotation({
        kind: 'business-value',
        truth_value: 'U',
        confidence: 0.99,
        location: { path: 'src/a.ts', line_start: 2, line_end: 2 },
      }),
    ]);
    // Both ignored => no files emitted (no other annotation kinds present).
    expect(files).toEqual([]);
  });

  it('takes the max confidence per axis per file', () => {
    const files = aggregate([
      mkAnnotation({
        kind: 'business-value',
        truth_value: 'T',
        confidence: 0.5,
        location: { path: 'src/x.ts', line_start: 1, line_end: 1 },
      }),
      mkAnnotation({
        kind: 'business-value',
        truth_value: 'P',
        confidence: 0.85,
        location: { path: 'src/x.ts', line_start: 2, line_end: 2 },
      }),
    ]);
    expect(files[0].value_score).toBe(0.85);
  });

  it('produces a REFACTOR FIRST candidate when both axes are high', () => {
    const annotations: Annotation[] = [
      mkAnnotation({
        kind: 'business-value',
        truth_value: 'T',
        confidence: 0.95,
        location: { path: 'src/engine.ts', line_start: 1, line_end: 1 },
      }),
      mkAnnotation({
        kind: 'smell',
        truth_value: 'D',
        confidence: 0.8,
        location: { path: 'src/engine.ts', line_start: 20, line_end: 20 },
      }),
    ];
    const files = aggregate(annotations);
    expect(files).toHaveLength(1);
    expect(bucketize(files[0].value_score, files[0].complexity_score)).toBe(
      'refactor-first',
    );
  });

  it('drops other kinds (invariant, assumption, hypothesis) from heatmap math', () => {
    const files = aggregate([
      mkAnnotation({
        kind: 'invariant',
        confidence: 0.99,
        location: { path: 'src/foo.ts', line_start: 1, line_end: 1 },
      }),
      mkAnnotation({
        kind: 'assumption',
        confidence: 0.99,
        location: { path: 'src/foo.ts', line_start: 2, line_end: 2 },
      }),
    ]);
    expect(files).toEqual([]);
  });
});
