// LOLLI Analyzer — Dead binding detection and scoring

import type { IxqlGraph, LolliReport, LolliStatus } from './types';

/**
 * Analyze an IxQL graph for LOLLI (dead bindings).
 *
 * - Green/live: binding is referenced downstream, on path to an output
 * - Red/dead: binding is never referenced by any other binding
 * - Gray/external: binding produces output (write/alert) but not consumed by other pipelines
 */
export function analyzeLolli(graph: IxqlGraph): LolliReport {
  const { bindings } = graph;

  // Find output nodes (write, alert, compound — these are "sinks")
  const outputKinds = new Set(['write', 'alert', 'compound']);

  // Walk backwards from outputs to find all live bindings
  const liveIds = new Set<string>();

  // Mark all outputs as live (or external)
  for (const b of bindings) {
    if (outputKinds.has(b.kind)) {
      liveIds.add(b.id);
      // Trace upstream
      markUpstreamLive(b.id, bindings, liveIds);
    }
  }

  // Also mark any binding that is referenced by others as live
  for (const b of bindings) {
    if (b.referencedBy.length > 0) {
      liveIds.add(b.id);
      markUpstreamLive(b.id, bindings, liveIds);
    }
  }

  // Assign statuses
  const deadNames: string[] = [];
  for (const b of bindings) {
    if (liveIds.has(b.id)) {
      if (outputKinds.has(b.kind) && b.referencedBy.length === 0) {
        b.lolliStatus = 'external' as LolliStatus;
      } else {
        b.lolliStatus = 'live' as LolliStatus;
      }
    } else {
      b.lolliStatus = 'dead' as LolliStatus;
      if (!b.id.startsWith('__')) {
        deadNames.push(b.name);
      }
    }
  }

  const namedBindings = bindings.filter((b) => !b.id.startsWith('__'));
  const deadCount = namedBindings.filter((b) => b.lolliStatus === 'dead').length;
  const externalCount = namedBindings.filter((b) => b.lolliStatus === 'external').length;
  const liveCount = namedBindings.length - deadCount - externalCount;

  return {
    totalBindings: namedBindings.length,
    liveBindings: liveCount,
    deadBindings: deadCount,
    externalBindings: externalCount,
    lolliScore: namedBindings.length > 0 ? (deadCount / namedBindings.length) * 100 : 0,
    deadNames,
  };
}

function markUpstreamLive(
  id: string,
  bindings: IxqlGraph['bindings'],
  liveIds: Set<string>,
): void {
  const binding = bindings.find((b) => b.id === id);
  if (!binding) return;

  for (const ref of binding.references) {
    if (!liveIds.has(ref)) {
      liveIds.add(ref);
      markUpstreamLive(ref, bindings, liveIds);
    }
  }
}
