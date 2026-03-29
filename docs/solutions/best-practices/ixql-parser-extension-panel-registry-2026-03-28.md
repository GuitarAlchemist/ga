---
module: PrimeRadiant / IXQL
date: 2026-03-28
problem_type: best_practice
component: tooling
symptoms:
  - Adding a new panel requires 4-7 file touchpoints
  - Health indicators use ad-hoc per-panel hooks
  - No runtime panel extensibility
root_cause: missing_tooling
resolution_type: code_fix
severity: medium
tags:
  - ixql
  - parser
  - panel-registry
  - discriminated-union
  - typescript
  - prime-radiant
  - usesyncexternalstore
  - recursive-descent
---

# IXQL Parser Extension & Panel Registry — Best Practices

## Problem

The Prime Radiant required 4-7 file touchpoints per new panel (PanelId type, RAIL_ITEMS array, ForceRadiant conditional chain, component import, CSS, index exports). Health indicators were ad-hoc hooks. The IXQL parser only handled SELECT/RESET visualization commands.

## Solution

### 1. Hand-rolled recursive descent handles DDL well up to ~10 command types

Extended `IxqlControlParser.ts` from 209→540 lines with 9 command variants. Key technique: **keyword dispatch on first token** with strict clause ordering.

**Before:**
```typescript
// Flat interface — optional fields that don't belong to all commands
interface IxqlCommand {
  type: 'select' | 'reset';
  target?: 'nodes' | 'edges';
  predicates?: IxqlPredicate[];
  assignments?: IxqlAssignment[];
}
```

**After:**
```typescript
// Proper discriminated union — each variant carries only its own fields
type IxqlCommand =
  | { type: 'select'; target: 'nodes' | 'edges'; predicates: IxqlPredicate[]; assignments: IxqlAssignment[] }
  | { type: 'reset' }
  | { type: 'create-panel'; id: string; source: string; layout: LayoutType; /* ... */ }
  | { type: 'bind-health'; targetKind: 'panel' | 'node'; /* ... */ }
  // ... 5 more variants

type IxqlParseResult =
  | { ok: true; command: IxqlCommand }
  | { ok: false; error: string };
```

**Key decision:** Strict clause ordering (FROM before LAYOUT before ICON before SHOW before FILTER) keeps the parser simple — no clause-collector loop needed. Revisit at 300 lines if grammar grows beyond ~15 command types.

### 2. useSyncExternalStore beats jotai/event-emitters for single reactive stores

For the PanelRegistry, `useSyncExternalStore` was simpler than jotai atoms or custom event emitters:

```typescript
class PanelRegistryStore {
  private panels = new Map<string, PanelRegistration>();
  private listeners = new Set<() => void>();

  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  getSnapshot = () => [...this.panels.values()];

  register(reg: PanelRegistration) {
    this.panels.set(reg.definition.id, reg);
    this.listeners.forEach(l => l());
  }
}

// React hook
export function usePanelRegistry() {
  return useSyncExternalStore(
    panelRegistry.subscribe,
    panelRegistry.getSnapshot
  );
}
```

**Why not jotai:** Adding jotai for a single store when the rest of PrimeRadiant uses plain React state is inconsistent. `useSyncExternalStore` is built-in, provides batching, and requires no dependency.

### 3. Preserve type safety when making unions dynamic

When expanding `PanelId` from a static union to accept dynamic strings:

```typescript
// Preserves compile-time narrowing for hardcoded panels
type BuiltInPanelId = 'activity' | 'backlog' | 'agent' | /* ... */;
type PanelId = BuiltInPanelId | (string & {});
```

The `(string & {})` trick allows any string while keeping IntelliSense autocomplete for known values. Plain `string` would lose all narrowing.

### 4. Code review before implementation catches architecture issues

Running architecture-strategist, code-simplicity-reviewer, and kieran-typescript-reviewer on the plan caught 11 issues before any code was written:
- Extract IxqlCommandDispatcher (ForceRadiant already 2,550 lines)
- Separate health status from PanelRegistry (avoid hot-path coupling)
- Split PanelRegistration into serializable definition + runtime wrapper
- IxqlParseResult must be a proper discriminated union
- DynamicPanel layout renderers should be separate modules

### 5. Claude Code JSONL files have metadata at the start, not the end

Session metadata (`sessionId`, `model`, `gitBranch`, `cwd`) is in the first few JSONL lines (type `user` with `isMeta: true` and type `assistant` with `model` field). The last line is often a `file-history-snapshot` or `progress` event with no useful metadata. Always scan from the beginning.

## Prevention

- **Parser size guardrail:** If the IXQL parser exceeds 300 lines, formally evaluate whether to switch to a parser combinator library (chevrotain)
- **Type safety guardrail:** When expanding string literal unions for extensibility, use the `UnionType | (string & {})` pattern to preserve IntelliSense
- **Review before build:** For Deep architectural plans, run reviewers on the plan document before implementing — catches structural issues that are expensive to fix in code
- **JSONL file reading:** When extracting metadata from JSONL files, scan from the head (first 16KB), not the tail
