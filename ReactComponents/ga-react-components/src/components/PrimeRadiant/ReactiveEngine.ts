// src/components/PrimeRadiant/ReactiveEngine.ts
// Event-driven trigger execution engine for IXQL ON...THEN rules.
// Watches health, panel, and API sources for changes, then dispatches actions.

import type { OnChangedCommand, IxqlCommand } from './IxqlControlParser';
import { evaluatePredicate } from './IxqlControlParser';
import { healthBindingEngine } from './HealthBindingEngine';
import { resolve } from './DataFetcher';
import { panelRegistry } from './PanelRegistry';
import { recordInvocation } from './IxqlTelemetry';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type DispatchFn = (result: { ok: true; command: IxqlCommand }) => void;

interface ReactiveRule {
  id: string;
  command: OnChangedCommand;
  dispatch: DispatchFn;
  lastSnapshot: string | null;  // JSON.stringify of last seen data, for change detection
  cleanup?: () => void;         // unsubscribe function
}

// ---------------------------------------------------------------------------
// Engine singleton
// ---------------------------------------------------------------------------

class ReactiveEngineImpl {
  private rules = new Map<string, ReactiveRule>();

  register(id: string, command: OnChangedCommand, dispatch: DispatchFn): void {
    // Clean up existing rule with same id
    this.unregister(id);

    const rule: ReactiveRule = { id, command, dispatch, lastSnapshot: null };
    this.rules.set(id, rule);

    const source = command.source;

    if (source.startsWith('health.')) {
      // Health source — subscribe to healthBindingEngine
      const panelId = source.replace('health.', '');
      const unsub = healthBindingEngine.subscribe((state) => {
        const currentStatus = state.panelHealth.get(panelId) ?? null;
        const current = JSON.stringify(currentStatus);
        if (rule.lastSnapshot !== null && rule.lastSnapshot !== current) {
          this.tryDispatch(rule, { status: currentStatus });
        }
        rule.lastSnapshot = current;
      });
      rule.cleanup = unsub;

    } else if (source.startsWith('panel://')) {
      // Panel registry source — subscribe to panelRegistry
      const panelId = source.replace('panel://', '');
      const unsub = panelRegistry.subscribe(() => {
        const reg = panelRegistry.get(panelId);
        const snapshot = JSON.stringify(reg?.definition ?? null);
        if (rule.lastSnapshot !== null && rule.lastSnapshot !== snapshot) {
          this.tryDispatch(rule, reg?.definition ?? null);
        }
        rule.lastSnapshot = snapshot;
      });
      rule.cleanup = unsub;

    } else {
      // Poll-based source (API, governance, HTTP) — poll every 15s
      const tick = async () => {
        const data = await resolve(source);
        const snapshot = JSON.stringify(data);
        if (rule.lastSnapshot !== null && rule.lastSnapshot !== snapshot) {
          this.tryDispatch(rule, data);
        }
        rule.lastSnapshot = snapshot;
      };
      tick(); // initial fetch
      const timer = setInterval(tick, 15_000);
      rule.cleanup = () => clearInterval(timer);
    }
  }

  unregister(id: string): void {
    const rule = this.rules.get(id);
    if (rule) {
      rule.cleanup?.();
      this.rules.delete(id);
    }
  }

  dispose(): void {
    for (const id of [...this.rules.keys()]) {
      this.unregister(id);
    }
  }

  // Check WHERE predicates and dispatch action if conditions are met
  private tryDispatch(rule: ReactiveRule, data: unknown): void {
    const { command, dispatch } = rule;

    // If there are WHERE predicates, check them against the changed data
    if (command.wherePredicates.length > 0) {
      const obj = (data && typeof data === 'object' && !Array.isArray(data))
        ? data as Record<string, unknown>
        : { value: data };
      const allMatch = command.wherePredicates.every(p => evaluatePredicate(p, obj));
      if (!allMatch) return;
    }

    recordInvocation('on-changed:trigger', true);
    dispatch({ ok: true, command: command.action });
  }
}

export const reactiveEngine = new ReactiveEngineImpl();
