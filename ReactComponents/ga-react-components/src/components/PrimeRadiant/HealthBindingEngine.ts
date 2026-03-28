// src/components/PrimeRadiant/HealthBindingEngine.ts
// Evaluates IXQL BIND HEALTH rules — polls data sources, applies WHEN conditions,
// produces panel/node health statuses. Replaces hardcoded health hooks with
// a declarative, IXQL-driven health pipeline.

import { resolve, type GraphContext } from './DataFetcher';
import { evaluatePredicate, type BindHealthCommand, type IxqlPredicate } from './IxqlControlParser';
import type { PanelStatus } from './IconRail';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface HealthBinding {
  id: string;               // unique binding id (e.g. "panel:llm", "node:staleness")
  command: BindHealthCommand;
}

export interface HealthState {
  panelHealth: Map<string, PanelStatus>;
  nodeHealth: Map<string, PanelStatus>;  // keyed by node id
}

type HealthListener = (state: HealthState) => void;

// ---------------------------------------------------------------------------
// Evaluate a single health binding against fetched data
// ---------------------------------------------------------------------------

function evaluateHealthStatus(
  data: unknown[],
  conditions: { predicate: IxqlPredicate; status: string }[],
  fallback: string,
): PanelStatus {
  // Aggregate: check all data items against each WHEN condition
  // If ANY item matches a condition, that status wins (worst-case escalation)
  const STATUS_PRIORITY: Record<string, number> = {
    critical: 4,
    error: 3,
    warn: 2,
    ok: 1,
  };

  let worstStatus = fallback;
  let worstPriority = STATUS_PRIORITY[fallback] ?? 0;

  for (const item of data) {
    for (const cond of conditions) {
      if (evaluatePredicate(cond.predicate, item as Record<string, unknown>)) {
        const p = STATUS_PRIORITY[cond.status] ?? 0;
        if (p > worstPriority) {
          worstStatus = cond.status;
          worstPriority = p;
        }
      }
    }
  }

  // Normalize to PanelStatus
  const valid: PanelStatus[] = ['ok', 'warn', 'error', 'critical'];
  return valid.includes(worstStatus as PanelStatus)
    ? (worstStatus as PanelStatus)
    : 'ok';
}

// ---------------------------------------------------------------------------
// Engine singleton
// ---------------------------------------------------------------------------

class HealthBindingEngineImpl {
  private bindings = new Map<string, HealthBinding>();
  private state: HealthState = {
    panelHealth: new Map(),
    nodeHealth: new Map(),
  };
  private listeners = new Set<HealthListener>();
  private pollTimers = new Map<string, ReturnType<typeof setInterval>>();
  private graphContext?: GraphContext;

  // Register a BIND HEALTH command
  register(binding: HealthBinding): void {
    // Clean up existing binding with same id
    this.unregister(binding.id);
    this.bindings.set(binding.id, binding);
    // Immediate evaluation + start polling
    this.evaluate(binding);
    const timer = setInterval(() => this.evaluate(binding), 60_000);
    this.pollTimers.set(binding.id, timer);
  }

  // Remove a health binding
  unregister(id: string): void {
    this.bindings.delete(id);
    const timer = this.pollTimers.get(id);
    if (timer) {
      clearInterval(timer);
      this.pollTimers.delete(id);
    }
    // Clean up state
    this.state.panelHealth.delete(id);
    this.state.nodeHealth.delete(id);
    this.notify();
  }

  // Update graph context for graph:// sources
  setGraphContext(ctx: GraphContext): void {
    this.graphContext = ctx;
  }

  // Get current health state
  getState(): HealthState {
    return this.state;
  }

  // Get panel health as a plain record (for IconRail)
  getPanelHealthRecord(): Partial<Record<string, PanelStatus>> {
    const record: Partial<Record<string, PanelStatus>> = {};
    for (const [id, status] of this.state.panelHealth) {
      record[id] = status;
    }
    return record;
  }

  // Subscribe to health state changes
  subscribe(listener: HealthListener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  // Evaluate a single binding
  private async evaluate(binding: HealthBinding): Promise<void> {
    const { command } = binding;
    const data = await resolve(command.source, [], this.graphContext);

    if (command.targetKind === 'panel') {
      const status = evaluateHealthStatus(data, command.conditions, command.fallback);
      this.state.panelHealth.set(command.targetId, status);
    } else if (command.targetKind === 'node') {
      // For node targeting, apply selector predicates to find matching nodes
      // then set health for each matching node
      const graphNodes = this.graphContext?.nodes ?? [];
      for (const node of graphNodes) {
        const nodeId = String((node as Record<string, unknown>).id ?? '');
        const matches = command.targetSelector.length === 0 ||
          command.targetSelector.every(p => evaluatePredicate(p, node as Record<string, unknown>));
        if (matches && nodeId) {
          const status = evaluateHealthStatus(data, command.conditions, command.fallback);
          this.state.nodeHealth.set(nodeId, status);
        }
      }
    }

    this.notify();
  }

  private notify(): void {
    for (const fn of this.listeners) fn(this.state);
  }

  // Clean up all bindings (for component unmount)
  dispose(): void {
    for (const timer of this.pollTimers.values()) clearInterval(timer);
    this.pollTimers.clear();
    this.bindings.clear();
    this.listeners.clear();
  }
}

export const healthBindingEngine = new HealthBindingEngineImpl();

// ---------------------------------------------------------------------------
// Built-in health bindings — replaces hardcoded useLLMHealth/useCICDHealth
// These auto-register on module load, providing declarative health rules
// that drive the IconRail status dots.
// ---------------------------------------------------------------------------

// LLM health: poll /api/llm/status, escalate on depleted/limited providers
healthBindingEngine.register({
  id: 'llm',
  command: {
    type: 'bind-health',
    targetKind: 'panel',
    targetId: 'llm',
    targetSelector: [],
    source: '/api/llm/status',
    conditions: [
      { predicate: { field: 'status', operator: '=', value: 'depleted' }, status: 'error' },
      { predicate: { field: 'status', operator: '=', value: 'limited' }, status: 'warn' },
    ],
    fallback: 'ok',
  },
});

// CI/CD health: poll GitHub Actions status, escalate on failures
healthBindingEngine.register({
  id: 'cicd',
  command: {
    type: 'bind-health',
    targetKind: 'panel',
    targetId: 'cicd',
    targetSelector: [],
    source: '/api/cicd/status',
    conditions: [
      { predicate: { field: 'status', operator: '=', value: 'failure' }, status: 'error' },
      { predicate: { field: 'status', operator: '=', value: 'in_progress' }, status: 'warn' },
    ],
    fallback: 'ok',
  },
});
