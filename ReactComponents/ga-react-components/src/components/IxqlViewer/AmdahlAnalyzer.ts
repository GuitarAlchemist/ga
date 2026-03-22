// Amdahl Analyzer — Serial fraction computation for IxQL pipelines
// Amdahl's Law: S(N) = 1 / (s + (1-s)/N) where s = serial fraction

import type { IxqlGraph, AmdahlReport } from './types';

/**
 * Compute Amdahl's Law metrics for a parsed IxQL graph.
 * Serial stages: bindings with executionMode === 'serial'
 * Parallel stages: bindings with executionMode === 'parallel' (fan_out, parallel)
 */
export function analyzeAmdahl(graph: IxqlGraph): AmdahlReport {
  const { bindings } = graph;

  let totalCost = 0;
  let serialCost = 0;
  let serialCount = 0;
  let parallelCount = 0;

  for (const b of bindings) {
    totalCost += b.estimatedCost;
    if (b.executionMode === 'serial') {
      serialCost += b.estimatedCost;
      serialCount++;
    } else {
      parallelCount++;
    }
  }

  const serialFraction = totalCost > 0 ? serialCost / totalCost : 1;

  return {
    totalStages: bindings.length,
    serialStages: serialCount,
    parallelStages: parallelCount,
    serialFraction,
    speedupAtN: (n: number) => {
      if (serialFraction >= 1) return 1;
      return 1 / (serialFraction + (1 - serialFraction) / n);
    },
    theoreticalMax: serialFraction > 0 ? 1 / serialFraction : Infinity,
  };
}
