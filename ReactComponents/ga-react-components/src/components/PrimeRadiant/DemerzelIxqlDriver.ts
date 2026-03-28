// src/components/PrimeRadiant/DemerzelIxqlDriver.ts
// Demerzel's autonomous IXQL driver — evaluates governance state and
// emits IXQL commands to highlight issues, no LLM needed.
// Uses rules over beliefs, health metrics, and staleness to drive
// visual corrections dynamically.

import { parseIxqlCommand, type IxqlParseResult } from './IxqlControlParser';
import type { VisualCriticResult, CriticPhase } from './VisualCriticLoop';

interface DriverConfig {
  intervalMs?: number;          // default: 30_000 (30s)
  enabled?: boolean;
  onResult?: (result: VisualCriticResult) => void;
  onIxqlCommand?: (result: IxqlParseResult) => void;
  onPhaseChange?: (phase: CriticPhase) => void;
  getGraphData?: () => { nodes: unknown[]; links: unknown[] } | null;
}

interface GraphNode {
  id?: string;
  name?: string;
  type?: string;
  healthStatus?: string;
  health?: { resilienceScore?: number; staleness?: number; lolliCount?: number; ergolCount?: number };
}

// Rule-based visual quality assessment
function evaluateVisualQuality(nodes: GraphNode[]): VisualCriticResult {
  const issues: string[] = [];
  const commands: string[] = [];
  const suggestions: string[] = [];
  let score = 10;

  // Count by health status
  const statusCounts: Record<string, number> = {};
  let totalStaleness = 0;
  let staleNodes = 0;
  let totalResilience = 0;
  let lowResilience = 0;

  for (const n of nodes) {
    const hs = n.healthStatus ?? 'unknown';
    statusCounts[hs] = (statusCounts[hs] ?? 0) + 1;

    const staleness = n.health?.staleness ?? 0;
    totalStaleness += staleness;
    if (staleness > 0.5) staleNodes++;

    const resilience = n.health?.resilienceScore ?? 0.8;
    totalResilience += resilience;
    if (resilience < 0.5) lowResilience++;
  }

  const total = nodes.length || 1;
  const avgStaleness = totalStaleness / total;
  const avgResilience = totalResilience / total;

  // Rule 1: Error nodes should pulse red
  const errorCount = (statusCounts['error'] ?? 0) + (statusCounts['critical'] ?? 0);
  if (errorCount > 0) {
    score -= Math.min(3, errorCount);
    issues.push(`${errorCount} node(s) in error/critical state`);
    commands.push(`SELECT nodes WHERE health.resilience < 0.3 SET pulse = true, color = '#FF4444'`);
  }

  // Rule 2: Contradictory nodes should flicker
  const contradictory = statusCounts['contradictory'] ?? 0;
  if (contradictory > 0) {
    score -= Math.min(2, contradictory);
    issues.push(`${contradictory} contradictory belief(s) detected`);
    commands.push(`SELECT nodes WHERE health.resilience < 0.4 SET glow = true, color = '#FF8800'`);
  }

  // Rule 3: High staleness = governance attention needed
  if (avgStaleness > 0.4) {
    score -= 1;
    issues.push(`Average staleness ${(avgStaleness * 100).toFixed(0)}% — governance artifacts need refresh`);
    commands.push(`SELECT nodes WHERE health.staleness > 0.5 SET opacity = 0.5`);
    suggestions.push('Run governance audit to update stale artifacts');
  }

  // Rule 4: Low resilience across the board
  if (avgResilience < 0.6) {
    score -= 2;
    issues.push(`Average resilience ${(avgResilience * 100).toFixed(0)}% — below healthy threshold`);
    commands.push(`SELECT nodes WHERE health.resilience < 0.5 SET size = 1.5, glow = true`);
    suggestions.push('Investigate low-resilience nodes for missing tests or policies');
  }

  // Rule 5: Too many unknown nodes = not enough instrumentation
  const unknown = statusCounts['unknown'] ?? 0;
  const unknownPct = unknown / total;
  if (unknownPct > 0.3) {
    score -= 1;
    issues.push(`${(unknownPct * 100).toFixed(0)}% of nodes have unknown health status`);
    suggestions.push('Add health checks to unmonitored governance artifacts');
  }

  // Rule 6: LOLLI inflation warning
  let totalLolli = 0;
  for (const n of nodes) {
    totalLolli += n.health?.lolliCount ?? 0;
  }
  if (totalLolli > 5) {
    score -= 1;
    issues.push(`${totalLolli} LOLLI (unused artifacts) detected`);
    commands.push(`SELECT nodes WHERE health.staleness > 0.7 SET color = '#666666', opacity = 0.3`);
    suggestions.push('Clean up unused governance artifacts (LOLLI → ERGOL conversion)');
  }

  // Rule 7: Healthy state — make healthy nodes glow subtly
  const healthy = statusCounts['healthy'] ?? 0;
  if (healthy > total * 0.7) {
    commands.push(`SELECT nodes WHERE health.resilience > 0.8 SET glow = true, color = '#33CC66'`);
    suggestions.push('Governance health is strong — maintain current practices');
  }

  score = Math.max(1, Math.min(10, score));
  const signalType = score >= 5 ? 'pain' as const : 'pleasure' as const;

  return {
    quality: score,
    issues,
    ixql_commands: commands,
    signal_type: score < 5 ? 'pain' : 'pleasure',
    signal_severity: score < 3 ? 'critical' : score < 5 ? 'warning' : 'info',
    signal_description: `Governance visual quality: ${score}/10 (${issues.length} issues, ${commands.length} corrections)`,
    suggestions,
  };
}

/**
 * Start Demerzel's autonomous IXQL driver.
 * Evaluates governance graph health and emits IXQL commands to
 * highlight issues — no LLM needed, pure rule-based assessment.
 * Returns a cleanup function.
 */
export function startDemerzelDriver(config: DriverConfig = {}): () => void {
  const {
    intervalMs = 30_000,
    enabled = true,
    onResult,
    onIxqlCommand,
    onPhaseChange,
    getGraphData,
  } = config;

  if (!enabled || !getGraphData) return () => {};

  let running = true;

  function evaluate() {
    if (!running) return;

    try {
    onPhaseChange?.('capturing');

    const data = getGraphData?.();
    if (!data || !data.nodes?.length) {
      onPhaseChange?.('idle');
      return;
    }

    onPhaseChange?.('analyzing');

    const result = evaluateVisualQuality(data.nodes as GraphNode[]);

    onPhaseChange?.('executing');

    // Execute IXQL commands
    if (result.ixql_commands?.length && onIxqlCommand) {
      for (const cmd of result.ixql_commands) {
        try {
          const parsed = parseIxqlCommand(cmd);
          if (parsed.ok) {
            onIxqlCommand(parsed);
          }
        } catch { /* skip bad command */ }
      }
    }

    onResult?.(result);

    const bar = '\u2588'.repeat(result.quality) + '\u2591'.repeat(10 - result.quality);
    console.info(`[Demerzel] Governance Quality: [${bar}] ${result.quality}/10 | ${result.issues.length} issues | ${result.ixql_commands?.length ?? 0} corrections`);

    onPhaseChange?.('complete');
    setTimeout(() => { if (running) onPhaseChange?.('idle'); }, 5000);
    } catch (err) {
      console.warn('[Demerzel] Driver evaluation error:', err);
      onPhaseChange?.('idle');
    }
  }

  // First evaluation after scene settles
  const initial = setTimeout(evaluate, 8000);
  const interval = setInterval(evaluate, intervalMs);

  return () => {
    running = false;
    clearTimeout(initial);
    clearInterval(interval);
  };
}
