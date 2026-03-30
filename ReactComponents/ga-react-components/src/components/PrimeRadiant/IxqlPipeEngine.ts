// src/components/PrimeRadiant/IxqlPipeEngine.ts
// Client-side data transform pipeline for IXQL PIPE clauses.
// Pure functions only — no eval, no arbitrary expressions, no regex.
// Security: whitelisted step kinds and aggregate functions enforced by parser.

import { evaluatePredicate } from './IxqlControlParser';
import type { PipeStep, AggregateSpec } from './IxqlControlParser';
import { resolveField } from './DataFetcher';

// ---------------------------------------------------------------------------
// Main entry point
// ---------------------------------------------------------------------------

export function executePipeline(
  data: Record<string, unknown>[],
  steps: PipeStep[],
): Record<string, unknown>[] {
  let result = data;
  for (const step of steps) {
    result = executeStep(result, step);
  }
  return result;
}

// ---------------------------------------------------------------------------
// Step dispatcher
// ---------------------------------------------------------------------------

function executeStep(
  data: Record<string, unknown>[],
  step: PipeStep,
): Record<string, unknown>[] {
  switch (step.kind) {
    case 'filter':   return stepFilter(data, step);
    case 'sort':     return stepSort(data, step);
    case 'limit':    return stepLimit(data, step);
    case 'skip':     return stepSkip(data, step);
    case 'distinct': return stepDistinct(data, step);
    case 'flatten':  return stepFlatten(data, step);
    case 'group':    return stepGroup(data, step);
  }
}

// ---------------------------------------------------------------------------
// FILTER — reuse evaluatePredicate from parser
// ---------------------------------------------------------------------------

function stepFilter(
  data: Record<string, unknown>[],
  step: Extract<PipeStep, { kind: 'filter' }>,
): Record<string, unknown>[] {
  return data.filter(row =>
    step.predicates.every(p => evaluatePredicate(p, row)),
  );
}

// ---------------------------------------------------------------------------
// SORT — numeric-aware comparison
// ---------------------------------------------------------------------------

function stepSort(
  data: Record<string, unknown>[],
  step: { kind: 'sort'; field: string; direction: 'ASC' | 'DESC' },
): Record<string, unknown>[] {
  const sorted = [...data];
  const dir = step.direction === 'DESC' ? -1 : 1;

  sorted.sort((a, b) => {
    const va = resolveField(a, step.field);
    const vb = resolveField(b, step.field);

    // Nulls sort last
    if (va == null && vb == null) return 0;
    if (va == null) return 1;
    if (vb == null) return -1;

    // Numeric comparison if both are actually numeric (not empty strings)
    const sa = String(va);
    const sb = String(vb);
    const na = sa === '' ? NaN : Number(va);
    const nb = sb === '' ? NaN : Number(vb);
    if (!isNaN(na) && !isNaN(nb)) {
      return (na - nb) * dir;
    }

    // String comparison (sa/sb already defined above)
    if (sa < sb) return -1 * dir;
    if (sa > sb) return 1 * dir;
    return 0;
  });

  return sorted;
}

// ---------------------------------------------------------------------------
// LIMIT / SKIP
// ---------------------------------------------------------------------------

function stepLimit(
  data: Record<string, unknown>[],
  step: { kind: 'limit'; count: number },
): Record<string, unknown>[] {
  return data.slice(0, step.count);
}

function stepSkip(
  data: Record<string, unknown>[],
  step: { kind: 'skip'; count: number },
): Record<string, unknown>[] {
  return data.slice(step.count);
}

// ---------------------------------------------------------------------------
// DISTINCT — Set-based dedup
// ---------------------------------------------------------------------------

function stepDistinct(
  data: Record<string, unknown>[],
  step: { kind: 'distinct'; field: string | null },
): Record<string, unknown>[] {
  const seen = new Set<string>();
  const result: Record<string, unknown>[] = [];

  for (const row of data) {
    const rawVal = step.field ? resolveField(row, step.field) : undefined;
    const key = step.field
      ? (rawVal === null ? '__null__' : rawVal === undefined ? '__undefined__' : String(rawVal))
      : JSON.stringify(row);
    if (!seen.has(key)) {
      seen.add(key);
      result.push(row);
    }
  }

  return result;
}

// ---------------------------------------------------------------------------
// FLATTEN — expand array fields into N rows
// ---------------------------------------------------------------------------

function stepFlatten(
  data: Record<string, unknown>[],
  step: { kind: 'flatten'; field: string },
): Record<string, unknown>[] {
  const result: Record<string, unknown>[] = [];

  for (const row of data) {
    const val = resolveField(row, step.field);
    if (Array.isArray(val)) {
      for (const item of val) {
        result.push({ ...row, [step.field]: item });
      }
    } else {
      result.push(row);
    }
  }

  return result;
}

// ---------------------------------------------------------------------------
// GROUP BY — aggregate computation
// ---------------------------------------------------------------------------

function stepGroup(
  data: Record<string, unknown>[],
  step: { kind: 'group'; byField: string; aggregates: AggregateSpec[] },
): Record<string, unknown>[] {
  // Build groups
  const groups = new Map<string, Record<string, unknown>[]>();

  for (const row of data) {
    const key = String(resolveField(row, step.byField) ?? '');
    let group = groups.get(key);
    if (!group) {
      group = [];
      groups.set(key, group);
    }
    group.push(row);
  }

  // Compute aggregates for each group
  const result: Record<string, unknown>[] = [];

  for (const [key, rows] of groups) {
    const out: Record<string, unknown> = { [step.byField]: key };

    for (const agg of step.aggregates) {
      out[agg.alias] = computeAggregate(rows, agg);
    }

    result.push(out);
  }

  return result;
}

function computeAggregate(
  rows: Record<string, unknown>[],
  agg: AggregateSpec,
): unknown {
  switch (agg.fn) {
    case 'COUNT':
      return rows.length;

    case 'SUM': {
      let sum = 0;
      for (const row of rows) {
        const v = Number(resolveField(row, agg.field!));
        if (!isNaN(v)) sum += v;
      }
      return sum;
    }

    case 'AVG': {
      let sum = 0;
      let count = 0;
      for (const row of rows) {
        const v = Number(resolveField(row, agg.field!));
        if (!isNaN(v)) { sum += v; count++; }
      }
      return count > 0 ? sum / count : 0;
    }

    case 'MIN': {
      let min: number | null = null;
      for (const row of rows) {
        const v = Number(resolveField(row, agg.field!));
        if (!isNaN(v) && (min === null || v < min)) min = v;
      }
      return min ?? 0;
    }

    case 'MAX': {
      let max: number | null = null;
      for (const row of rows) {
        const v = Number(resolveField(row, agg.field!));
        if (!isNaN(v) && (max === null || v > max)) max = v;
      }
      return max ?? 0;
    }

    default:
      return null;
  }
}
