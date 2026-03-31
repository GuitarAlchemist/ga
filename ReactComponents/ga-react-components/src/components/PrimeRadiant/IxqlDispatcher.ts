// src/components/PrimeRadiant/IxqlDispatcher.ts
// Extracted IXQL command dispatch from ForceRadiant.tsx (was ~277 lines in a God Component).
// Provides a CommandHandler interface for testable, modular command dispatch.
// Each command type registers a handler. ForceRadiant provides the context.

import type { IxqlCommand, IxqlParseResult, IxqlPredicate } from './IxqlControlParser';
import { evaluatePredicate } from './IxqlControlParser';
import { compileGridPanel, compileViz, compileForm } from './IxqlWidgetSpec';
import type { PanelSpec, VizSpec, FormSpec } from './IxqlWidgetSpec';
import { violationMonitor } from './ViolationMonitor';
import { savedQueryStore } from './SavedQueryStore';
import { recordInvocation } from './IxqlTelemetry';
import { signalBus } from './DashboardSignalBus';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Context provided by the host component (ForceRadiant) */
export interface DispatchContext {
  // Graph access (for SELECT/RESET/SHOW EPISTEMIC visual overrides)
  getGraphNodes: () => Array<Record<string, unknown>> | null;
  getGraphLinks: () => Array<Record<string, unknown>> | null;
  applyNodeOverrides: (node: Record<string, unknown>, overrides: Record<string, unknown>) => void;
  applyLinkOverrides: (link: Record<string, unknown>, overrides: Record<string, unknown>) => void;
  clearAllOverrides: () => void;

  // Panel registration
  registerPanel: (id: string, type: 'grid' | 'viz' | 'form' | 'custom', label: string, icon: string, group?: string) => void;
  unregisterPanel: (id: string) => void;
  setActivePanel: (id: string | null) => void;

  // Spec storage
  storeGridSpec: (id: string, spec: PanelSpec) => void;
  storeVizSpec: (id: string, spec: VizSpec) => void;
  storeFormSpec: (id: string, spec: FormSpec) => void;
  storeDynamicPanelDef: (id: string, def: Record<string, unknown>) => void;
  deleteSpecs: (id: string) => void;

  // Health/reactive engines (optional, provided by ForceRadiant)
  registerHealthBinding?: (id: string, command: unknown) => void;
  registerReactiveTrigger?: (id: string, command: unknown, dispatch: (result: IxqlParseResult) => void) => void;

  // Self-reference for recursive dispatch (ON...THEN)
  dispatch: (result: IxqlParseResult) => void;
}

/** Result of dispatching a command */
export interface DispatchResult {
  handled: boolean;
  commandType: string;
  error?: string;
}

// ---------------------------------------------------------------------------
// Dispatch function — the extracted heart of ForceRadiant.handleIxqlCommand
// ---------------------------------------------------------------------------

export function dispatchIxqlCommand(
  result: IxqlParseResult,
  ctx: DispatchContext,
): DispatchResult {
  if (!result.ok) {
    recordInvocation('parse-error', false);
    return { handled: false, commandType: 'parse-error', error: result.error };
  }

  const cmd = result.command;
  recordInvocation(cmd.type, true);

  switch (cmd.type) {
    case 'reset': {
      ctx.clearAllOverrides();
      return { handled: true, commandType: 'reset' };
    }

    case 'select': {
      if (cmd.target === 'nodes') {
        const nodes = ctx.getGraphNodes();
        if (!nodes) return { handled: false, commandType: 'select', error: 'No graph available' };
        for (const node of nodes) {
          const matches = cmd.predicates.every(p => evaluatePredicate(p, node));
          if (matches) {
            const overrides: Record<string, unknown> = {};
            for (const a of cmd.assignments) overrides[a.property] = a.value;
            ctx.applyNodeOverrides(node, overrides);
          }
        }
      } else if (cmd.target === 'edges') {
        const links = ctx.getGraphLinks();
        if (!links) return { handled: false, commandType: 'select', error: 'No graph available' };
        for (const link of links) {
          const matches = cmd.predicates.length === 0 ||
            cmd.predicates.every(p => evaluatePredicate(p, link));
          if (matches) {
            const overrides: Record<string, unknown> = {};
            for (const a of cmd.assignments) overrides[a.property] = a.value;
            ctx.applyLinkOverrides(link, overrides);
          }
        }
      }
      return { handled: true, commandType: 'select' };
    }

    case 'create-panel': {
      ctx.storeDynamicPanelDef(cmd.id, {
        id: cmd.id,
        label: formatLabel(cmd.id),
        source: cmd.source,
        layout: cmd.layout,
        wherePredicates: cmd.wherePredicates,
        showFields: cmd.showFields,
        filter: cmd.filter,
      });
      ctx.registerPanel(cmd.id, 'custom', formatLabel(cmd.id), cmd.icon || 'detail');
      ctx.setActivePanel(cmd.id);
      return { handled: true, commandType: 'create-panel' };
    }

    case 'create-grid-panel': {
      const spec = compileGridPanel(cmd);
      ctx.storeGridSpec(cmd.id, spec);
      ctx.registerPanel(cmd.id, 'grid', formatLabel(cmd.id), 'grid', 'governance');
      ctx.setActivePanel(cmd.id);
      return { handled: true, commandType: 'create-grid-panel' };
    }

    case 'create-viz': {
      const vizSpec = compileViz(cmd);
      ctx.storeVizSpec(cmd.id, vizSpec);
      ctx.registerPanel(cmd.id, 'viz', formatLabel(cmd.id), 'activity', 'viz');
      ctx.setActivePanel(cmd.id);
      return { handled: true, commandType: 'create-viz' };
    }

    case 'create-form': {
      const formSpec = compileForm(cmd);
      ctx.storeFormSpec(cmd.id, formSpec);
      ctx.registerPanel(cmd.id, 'form', formatLabel(cmd.id), 'detail', 'governance');
      ctx.setActivePanel(cmd.id);
      return { handled: true, commandType: 'create-form' };
    }

    case 'drop': {
      ctx.deleteSpecs(cmd.id);
      ctx.unregisterPanel(cmd.id);
      return { handled: true, commandType: 'drop' };
    }

    case 'bind-health': {
      if (ctx.registerHealthBinding) {
        const bindingId = cmd.targetKind === 'panel'
          ? cmd.targetId
          : `node:${cmd.targetSelector.map(p => p.field).join(',')}`;
        ctx.registerHealthBinding(bindingId, cmd);
      }
      return { handled: true, commandType: 'bind-health' };
    }

    case 'on-changed': {
      if (ctx.registerReactiveTrigger) {
        const ruleId = `on:${cmd.source}`;
        ctx.registerReactiveTrigger(ruleId, cmd, ctx.dispatch);
      }
      return { handled: true, commandType: 'on-changed' };
    }

    case 'on-violation': {
      const ruleId = `violation:${cmd.source}:${cmd.severity}`;
      violationMonitor.registerViolation({
        id: ruleId,
        source: cmd.source,
        predicates: cmd.condition,
        severity: cmd.severity,
        actions: cmd.actions,
        notify: cmd.notify,
      });
      return { handled: true, commandType: 'on-violation' };
    }

    case 'save': {
      const cmdText = `SAVE QUERY "${cmd.id}"${cmd.asArtifact ? ' AS artifact' : ''}${cmd.rationale ? ` RATIONALE "${cmd.rationale}"` : ''}`;
      savedQueryStore.save(cmd.id, cmdText, cmd.asArtifact, cmd.rationale);
      return { handled: true, commandType: 'save' };
    }

    case 'show-epistemic': {
      if (cmd.visualize) {
        const nodes = ctx.getGraphNodes();
        if (nodes) {
          const TENSOR_COLORS: Record<string, string> = {
            'C_T': '#FFD700', 'T_C': '#CE93D8', 'U_F': '#4FC3F7',
            'U_U': '#6b7280', 'C_C': '#FF4444',
          };
          for (const node of nodes) {
            const matches = cmd.predicates.length === 0 ||
              cmd.predicates.every(p => evaluatePredicate(p, node));
            if (matches) {
              const tensor = String(node['tensorConfig'] ?? 'U_U');
              const overrides: Record<string, unknown> = {
                glow: TENSOR_COLORS[tensor] ?? '#8b949e',
                pulse: tensor === 'C_T' ? 1.5 : tensor.startsWith('C') ? 2.0 : 0,
                opacity: tensor === 'U_U' ? 0.4 : 1.0,
              };
              const viscosity = Number(node['viscosity'] ?? 0);
              if (viscosity > 0.8) {
                overrides['speed'] = 0;
                overrides['color'] = '#88ccdd';
              }
              ctx.applyNodeOverrides(node, overrides);
            }
          }
        }
      }

      // Non-visual SHOW EPISTEMIC: auto-create a transient grid panel with the epistemic source
      if (!cmd.visualize) {
        const panelId = `epistemic-${cmd.target}`;
        const source = `governance.epistemic.${cmd.target}`;
        // Create a grid panel to display the epistemic data
        signalBus.publish('ixql:show-epistemic', {
          target: cmd.target,
          predicates: cmd.predicates,
          orderBy: cmd.orderBy,
          limit: cmd.limit,
          panelId,
          source,
        }, '__ixqlDispatcher__');
      }

      return { handled: true, commandType: 'show-epistemic' };
    }

    case 'methylate': {
      const key = `epistemic-methylation-${cmd.strategyId}`;
      try {
        localStorage.setItem(key, JSON.stringify({
          methylated: true,
          reason: cmd.reason,
          methylatedAt: new Date().toISOString(),
        }));
      } catch { /* SSR safe */ }
      return { handled: true, commandType: 'methylate' };
    }

    case 'demethylate': {
      try {
        localStorage.removeItem(`epistemic-methylation-${cmd.strategyId}`);
      } catch { /* SSR safe */ }
      return { handled: true, commandType: 'demethylate' };
    }

    case 'amnesia': {
      const scheduledFor = new Date(Date.now() + cmd.scheduleDays * 86400000).toISOString();
      try {
        const schedule = JSON.parse(localStorage.getItem('epistemic-amnesia-schedule') ?? '[]');
        schedule.push({ beliefId: cmd.beliefId, scheduledFor, executed: false });
        localStorage.setItem('epistemic-amnesia-schedule', JSON.stringify(schedule));
      } catch { /* SSR safe */ }
      return { handled: true, commandType: 'amnesia' };
    }

    case 'broadcast': {
      signalBus.publish('ixql:broadcast', {
        target: cmd.target,
        predicates: cmd.predicates,
      }, '__ixqlDispatcher__');
      return { handled: true, commandType: 'broadcast' };
    }

    default: {
      // Unhandled command type — log but don't crash
      const unhandled = (cmd as IxqlCommand).type;
      return { handled: false, commandType: unhandled, error: `Unhandled command type: ${unhandled}` };
    }
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatLabel(id: string): string {
  return id.split('-').join(' ').split('_').join(' ')
    .split(' ').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}
