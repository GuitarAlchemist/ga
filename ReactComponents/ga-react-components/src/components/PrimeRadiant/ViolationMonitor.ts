// src/components/PrimeRadiant/ViolationMonitor.ts
// Agentic violation monitor — watches panel data and fires IXQL actions
// when governance violations are detected. Uses cooldowns to prevent storms.

import { useSyncExternalStore } from 'react';
import type { IxqlPredicate, ViolationSeverity } from './IxqlControlParser';
import { evaluatePredicate, parseIxqlCommand } from './IxqlControlParser';
import { signalBus } from './DashboardSignalBus';
import { recordInvocation } from './IxqlTelemetry';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ViolationRule {
  id: string;
  source: string;
  predicates: IxqlPredicate[];
  severity: ViolationSeverity;
  actions: string[];
  notify: string | null;
  lastFired: number | null;
  cooldownMs: number;
}

export interface ActiveViolation {
  ruleId: string;
  source: string;
  severity: ViolationSeverity;
  message: string;
  firedAt: number;
  actionsExecuted: string[];
}

type DispatchFn = (result: { ok: true; command: import('./IxqlControlParser').IxqlCommand }) => void;
type Listener = () => void;

// ---------------------------------------------------------------------------
// Severity colors for signal bus publishing
// ---------------------------------------------------------------------------

const SEVERITY_SIGNAL_PREFIX = 'violation:';

// ---------------------------------------------------------------------------
// ViolationMonitor singleton
// ---------------------------------------------------------------------------

class ViolationMonitorImpl {
  private rules = new Map<string, ViolationRule>();
  private active = new Map<string, ActiveViolation>();
  private listeners = new Set<Listener>();
  private snapshot: ActiveViolation[] = [];
  private dirty = true;
  private dispatch: DispatchFn | null = null;

  /** Set the IXQL command dispatcher (called once from ForceRadiant) */
  setDispatch(fn: DispatchFn): void {
    this.dispatch = fn;
  }

  /** Register a violation rule from ON VIOLATION command */
  registerViolation(rule: Omit<ViolationRule, 'lastFired' | 'cooldownMs'>): void {
    // Clean up existing rule with same id
    this.rules.delete(rule.id);

    this.rules.set(rule.id, {
      ...rule,
      lastFired: null,
      cooldownMs: 30_000, // 30s default cooldown
    });
  }

  /** Unregister a violation rule */
  unregisterViolation(id: string): void {
    this.rules.delete(id);
    if (this.active.delete(id)) {
      this.dirty = true;
      this.notify();
    }
  }

  /** Check all rules against incoming data for a given source */
  checkViolations(data: unknown, sourceId: string): void {
    const now = Date.now();

    for (const [ruleId, rule] of this.rules) {
      // Only check rules that match this source
      if (rule.source !== sourceId) continue;

      // Cooldown check
      if (rule.lastFired !== null && (now - rule.lastFired) < rule.cooldownMs) {
        continue;
      }

      // Evaluate predicates against data
      const violated = this.evaluateViolation(rule.predicates, data);

      if (violated) {
        this.fireViolation(rule, now);
      } else {
        // Clear active violation if predicates no longer match
        if (this.active.has(ruleId)) {
          this.active.delete(ruleId);
          this.dirty = true;
          this.notify();
        }
      }
    }
  }

  /** Get all active violations as a stable snapshot */
  getActiveViolations(): ActiveViolation[] {
    if (this.dirty) {
      this.snapshot = [...this.active.values()];
      this.dirty = false;
    }
    return this.snapshot;
  }

  /** Get count of violations by severity */
  getViolationCounts(): Record<ViolationSeverity, number> {
    const counts: Record<ViolationSeverity, number> = { info: 0, warning: 0, critical: 0 };
    for (const v of this.active.values()) {
      counts[v.severity]++;
    }
    return counts;
  }

  /** Subscribe to violation state changes */
  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  /** Clear all rules and active violations */
  dispose(): void {
    this.rules.clear();
    this.active.clear();
    this.dirty = true;
    this.notify();
  }

  // ── Internal ──

  private evaluateViolation(predicates: IxqlPredicate[], data: unknown): boolean {
    if (predicates.length === 0) return false;

    // If data is an array, check if any element violates
    if (Array.isArray(data)) {
      return data.some(item => {
        const obj = (item && typeof item === 'object')
          ? item as Record<string, unknown>
          : { value: item };
        return predicates.every(p => evaluatePredicate(p, obj));
      });
    }

    // Single object
    const obj = (data && typeof data === 'object')
      ? data as Record<string, unknown>
      : { value: data };
    return predicates.every(p => evaluatePredicate(p, obj));
  }

  private fireViolation(rule: ViolationRule, now: number): void {
    rule.lastFired = now;

    const executedActions: string[] = [];

    // Execute each IXQL action — isolated so one failure doesn't abort the rest
    for (const actionStr of rule.actions) {
      try {
        const parsed = parseIxqlCommand(actionStr);
        if (parsed.ok && this.dispatch) {
          this.dispatch(parsed);
          executedActions.push(actionStr);
          recordInvocation('on-violation:action', true);
        } else {
          recordInvocation('on-violation:action', false);
        }
      } catch {
        recordInvocation('on-violation:action', false);
      }
    }

    // Publish to signal bus for cross-panel notification
    const violationSignal = SEVERITY_SIGNAL_PREFIX + rule.severity;
    signalBus.publish(violationSignal, {
      ruleId: rule.id,
      source: rule.source,
      severity: rule.severity,
      firedAt: now,
    }, 'violation-monitor');

    // Notify via algedonic channel if specified
    if (rule.notify) {
      signalBus.publish(rule.notify, {
        type: 'violation',
        ruleId: rule.id,
        source: rule.source,
        severity: rule.severity,
        actions: executedActions,
        timestamp: now,
      }, 'violation-monitor');
    }

    // Track as active violation
    const violation: ActiveViolation = {
      ruleId: rule.id,
      source: rule.source,
      severity: rule.severity,
      message: `Violation in ${rule.source}: ${rule.predicates.map(p => `${p.field} ${p.operator} ${p.value}`).join(' AND ')}`,
      firedAt: now,
      actionsExecuted: executedActions,
    };
    this.active.set(rule.id, violation);

    this.dirty = true;
    recordInvocation('on-violation:trigger', true);
    this.notify();
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }
}

export const violationMonitor = new ViolationMonitorImpl();

// ---------------------------------------------------------------------------
// React hooks
// ---------------------------------------------------------------------------

/** Subscribe to active violations. Returns the current list. */
export function useViolationMonitor(): ActiveViolation[] {
  return useSyncExternalStore(
    (cb) => violationMonitor.subscribe(cb),
    () => violationMonitor.getActiveViolations(),
  );
}

/** Get violation counts by severity. */
export function useViolationCounts(): Record<ViolationSeverity, number> {
  useSyncExternalStore(
    (cb) => violationMonitor.subscribe(cb),
    () => violationMonitor.getActiveViolations(), // trigger re-render on change
  );
  return violationMonitor.getViolationCounts();
}
