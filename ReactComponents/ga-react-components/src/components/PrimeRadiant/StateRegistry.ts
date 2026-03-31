// src/components/PrimeRadiant/StateRegistry.ts
// IXQL State Management — eigengovernance of constitutive parameters.
// Four commands: GET STATE / SET STATE / ON STATE / EXPLAIN STATE
// Three classes: config (writable), derived (read-only), budget (envelopes)
// Constitutional floor: some parameters are immutable.

import { signalBus } from './DashboardSignalBus';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type StateClass = 'config' | 'derived' | 'budget';

export interface StateParam {
  path: string;
  class: StateClass;
  value: number;
  min: number;
  max: number;
  default: number;
  description: string;
  immutable: boolean;          // constitutional floor — cannot be SET
  refractoryMs: number;        // minimum ms between writes
  dampingAlpha: number;        // EMA damping (0=instant, 1=never)
  lastWriteAt: number;
  target: number | null;       // for damped params: the target value
  unit: string;                // 'fps', 'ms', 'ratio', 'px', 'bool'
}

export interface BudgetDef {
  name: string;
  path: string;
  ceiling: number;
  floor: number;
  target: number;
  current: number;
  mode: 'clamp' | 'shed' | 'throttle';
}

export interface StateChangeEvent {
  path: string;
  oldValue: number;
  newValue: number;
  source: 'user' | 'binding' | 'remediation' | 'system' | 'decay';
  clamped: boolean;
  timestamp: number;
}

export interface StateExplanation {
  path: string;
  class: StateClass;
  value: number;
  target: number | null;
  min: number;
  max: number;
  immutable: boolean;
  refractoryMs: number;
  dampingAlpha: number;
  lastWriteAt: number;
  timeSinceWrite: number;
  canWriteNow: boolean;
  description: string;
  unit: string;
}

// ---------------------------------------------------------------------------
// Default constitutive parameters
// ---------------------------------------------------------------------------

const DEFAULT_PARAMS: Omit<StateParam, 'lastWriteAt' | 'target'>[] = [
  // ── Rendering ──
  { path: 'rendering.quality.budget', class: 'config', value: 1.0, min: -1, max: 1, default: 1.0, description: 'Quality budget (-1=min, 1=max)', immutable: false, refractoryMs: 1000, dampingAlpha: 0.15, unit: 'ratio' },
  { path: 'rendering.fps.target', class: 'config', value: 45, min: 15, max: 144, default: 45, description: 'Target frame rate', immutable: false, refractoryMs: 5000, dampingAlpha: 0, unit: 'fps' },
  { path: 'rendering.bloom.strength', class: 'config', value: 0.5, min: 0, max: 1.5, default: 0.5, description: 'Bloom post-process intensity', immutable: false, refractoryMs: 500, dampingAlpha: 0.2, unit: 'ratio' },
  { path: 'rendering.pixelRatio', class: 'config', value: 1.5, min: 0.5, max: 3, default: 1.5, description: 'Render pixel ratio', immutable: false, refractoryMs: 2000, dampingAlpha: 0, unit: 'ratio' },
  { path: 'rendering.fps.current', class: 'derived', value: 60, min: 0, max: 999, default: 60, description: 'Current measured FPS (read-only)', immutable: true, refractoryMs: 0, dampingAlpha: 0, unit: 'fps' },

  // ── Temporal ──
  { path: 'temporal.decay.halfLifeMs', class: 'config', value: 120000, min: 5000, max: 600000, default: 120000, description: 'Hexavalent decay half-life', immutable: false, refractoryMs: 10000, dampingAlpha: 0, unit: 'ms' },
  { path: 'temporal.refresh.minMs', class: 'config', value: 1000, min: 500, max: 60000, default: 1000, description: 'Minimum adaptive refresh interval', immutable: false, refractoryMs: 5000, dampingAlpha: 0, unit: 'ms' },

  // ── Governance ──
  { path: 'governance.violation.cooldownMs', class: 'config', value: 30000, min: 1000, max: 300000, default: 30000, description: 'Violation monitor cooldown', immutable: false, refractoryMs: 10000, dampingAlpha: 0, unit: 'ms' },
  { path: 'governance.caseLaw.weight', class: 'config', value: 1.0, min: 0, max: 2, default: 1.0, description: 'Precedent influence weight', immutable: false, refractoryMs: 5000, dampingAlpha: 0, unit: 'ratio' },

  // ── Signal ──
  { path: 'signal.bus.throttleMs', class: 'config', value: 50, min: 16, max: 500, default: 50, description: 'Signal bus throttle interval', immutable: false, refractoryMs: 5000, dampingAlpha: 0, unit: 'ms' },

  // ── Safety — Constitutional Floor ──
  { path: 'safety.maxParseDepth', class: 'config', value: 10, min: 2, max: 10, default: 10, description: 'Max IXQL parse recursion depth', immutable: true, refractoryMs: 0, dampingAlpha: 0, unit: '' },
  { path: 'safety.maxFlattenRows', class: 'config', value: 10000, min: 100, max: 10000, default: 10000, description: 'Max rows from FLATTEN operation', immutable: true, refractoryMs: 0, dampingAlpha: 0, unit: '' },

  // ── Interaction ──
  { path: 'interaction.vitality.decayRate', class: 'config', value: 0.001, min: 0, max: 0.01, default: 0.001, description: 'Panel vitality decay per second of inactivity', immutable: false, refractoryMs: 5000, dampingAlpha: 0, unit: 'ratio' },
  { path: 'interaction.focal.radius', class: 'config', value: 150, min: 50, max: 500, default: 150, description: 'Focal crystallization radius in pixels', immutable: false, refractoryMs: 1000, dampingAlpha: 0.1, unit: 'px' },
  { path: 'interaction.focal.boost', class: 'config', value: 0.5, min: 0, max: 1, default: 0.5, description: 'Quality boost at cursor focal point', immutable: false, refractoryMs: 1000, dampingAlpha: 0.1, unit: 'ratio' },
];

// ---------------------------------------------------------------------------
// State Registry
// ---------------------------------------------------------------------------

class StateRegistryImpl {
  private params = new Map<string, StateParam>();
  private budgets = new Map<string, BudgetDef>();
  private causalDepth = 0;
  private maxCausalDepth = 3;

  constructor() {
    for (const p of DEFAULT_PARAMS) {
      this.params.set(p.path, { ...p, lastWriteAt: 0, target: null });
    }
  }

  /** GET STATE — read any parameter */
  get(path: string): number | undefined {
    const param = this.params.get(path);
    return param?.value;
  }

  /** SET STATE — write a config parameter (with validation, clamping, damping, refractory) */
  set(path: string, value: number, source: StateChangeEvent['source'] = 'user'): { ok: boolean; reason?: string } {
    const param = this.params.get(path);
    if (!param) return { ok: false, reason: `Unknown state path: ${path}` };
    if (param.immutable) return { ok: false, reason: `${path} is immutable (constitutional floor)` };
    if (param.class === 'derived') return { ok: false, reason: `${path} is derived (read-only)` };

    // Refractory period check
    const now = Date.now();
    if (param.refractoryMs > 0 && (now - param.lastWriteAt) < param.refractoryMs) {
      return { ok: false, reason: `${path} is in refractory period (${param.refractoryMs}ms)` };
    }

    // Causal depth check (prevent self-referential cascades)
    if (this.causalDepth > this.maxCausalDepth) {
      return { ok: false, reason: `Causal depth ${this.causalDepth} exceeds max ${this.maxCausalDepth}` };
    }

    // Clamp to bounds
    const clamped = Math.max(param.min, Math.min(param.max, value));
    const wasClamped = clamped !== value;
    const oldValue = param.value;

    // Apply damping or direct write
    if (param.dampingAlpha > 0) {
      param.target = clamped;
      // EMA: actual = actual + alpha * (target - actual)
      param.value = param.value + param.dampingAlpha * (clamped - param.value);
    } else {
      param.value = clamped;
      param.target = null;
    }

    param.lastWriteAt = now;

    // Emit signal
    const event: StateChangeEvent = {
      path, oldValue, newValue: param.value, source, clamped: wasClamped, timestamp: now,
    };
    this.causalDepth++;
    signalBus.publish('state.changed', event, '__stateRegistry__');
    if (wasClamped) {
      signalBus.publish('state.clamped', event, '__stateRegistry__');
    }
    this.causalDepth--;

    return { ok: true };
  }

  /** Update derived state (internal use — not exposed to IXQL) */
  updateDerived(path: string, value: number): void {
    const param = this.params.get(path);
    if (param && param.class === 'derived') {
      param.value = Math.max(param.min, Math.min(param.max, value));
    }
  }

  /** Apply damping tick — call once per second to advance damped params toward targets */
  tickDamping(): void {
    for (const param of this.params.values()) {
      if (param.target !== null && param.dampingAlpha > 0) {
        const diff = param.target - param.value;
        if (Math.abs(diff) < 0.001) {
          param.value = param.target;
          param.target = null;
        } else {
          param.value += param.dampingAlpha * diff;
        }
      }
    }
  }

  /** EXPLAIN STATE — full explanation of a parameter */
  explain(path: string): StateExplanation | null {
    const param = this.params.get(path);
    if (!param) return null;
    const now = Date.now();
    return {
      path: param.path,
      class: param.class,
      value: param.value,
      target: param.target,
      min: param.min,
      max: param.max,
      immutable: param.immutable,
      refractoryMs: param.refractoryMs,
      dampingAlpha: param.dampingAlpha,
      lastWriteAt: param.lastWriteAt,
      timeSinceWrite: now - param.lastWriteAt,
      canWriteNow: !param.immutable && param.class !== 'derived' &&
        (param.refractoryMs === 0 || (now - param.lastWriteAt) >= param.refractoryMs),
      description: param.description,
      unit: param.unit,
    };
  }

  /** List all state paths */
  listPaths(): string[] {
    return Array.from(this.params.keys()).sort();
  }

  /** List all paths by class */
  listByClass(cls: StateClass): string[] {
    return Array.from(this.params.values())
      .filter(p => p.class === cls)
      .map(p => p.path)
      .sort();
  }

  /** CREATE BUDGET — register a named constraint envelope */
  createBudget(name: string, path: string, ceiling: number, floor: number, target: number, mode: BudgetDef['mode'] = 'clamp'): void {
    this.budgets.set(name, { name, path, ceiling, floor, target, current: target, mode });
    signalBus.publish('budget.created', { name, path, ceiling, floor, target, mode }, '__stateRegistry__');
  }

  /** Get budget */
  getBudget(name: string): BudgetDef | undefined {
    return this.budgets.get(name);
  }

  /** Apply budget enforcement — clamp the associated parameter to budget bounds */
  enforceBudgets(): void {
    for (const budget of this.budgets.values()) {
      const param = this.params.get(budget.path);
      if (!param) continue;
      if (param.value > budget.ceiling) {
        this.set(budget.path, budget.ceiling, 'system');
        signalBus.publish('budget.exceeded', { name: budget.name, value: param.value, ceiling: budget.ceiling }, '__stateRegistry__');
      } else if (param.value < budget.floor) {
        this.set(budget.path, budget.floor, 'system');
        signalBus.publish('budget.exhausted', { name: budget.name, value: param.value, floor: budget.floor }, '__stateRegistry__');
      }
    }
  }

  /** Reset for test isolation */
  reset(): void {
    this.params.clear();
    this.budgets.clear();
    for (const p of DEFAULT_PARAMS) {
      this.params.set(p.path, { ...p, lastWriteAt: 0, target: null });
    }
  }
}

// ---------------------------------------------------------------------------
// Singleton
// ---------------------------------------------------------------------------

export const stateRegistry = new StateRegistryImpl();
