---
title: "feat: IXQL UI Grammar — Declarative Panel & Graph Pipeline"
type: feat
status: active
date: 2026-03-28
origin: docs/brainstorms/2026-03-28-ixql-ui-grammar-requirements.md
deepened: 2026-03-28
---

# IXQL UI Grammar — Declarative Panel & Graph Pipeline

## Overview

Extend the IXQL language from visualization-only (`SELECT ... WHERE ... SET ...`) to a full declarative interface for the Prime Radiant — panels, health bindings, data sources, and graph topology. Replace per-panel React boilerplate with IXQL definitions rendered by a generic `DynamicPanel` component. Persistence is hybrid: file-backed `.ixql` scripts + localStorage overlay for runtime experimentation.

## Problem Frame

Adding a new panel requires 4-7 file touchpoints across `PanelId` type, `RAIL_ITEMS`, `ForceRadiant.tsx` conditionals, imports, CSS, and `index.ts` exports. Every panel follows the same header/toggle/body/items pattern but is hand-coded. Health indicators use ad-hoc per-panel hooks (`useLLMHealth`, `useCICDHealth`). The IXQL language already parses and applies commands at 60fps in the visualization loop — extending it to drive panels and health is a natural evolution. (see origin: docs/brainstorms/2026-03-28-ixql-ui-grammar-requirements.md)

## Requirements Trace

- R1. DDL commands: `CREATE PANEL`, `ALTER PANEL`, `DROP PANEL`, `SAVE PANEL`
- R2. Health binding syntax: `BIND ... HEALTH FROM <source> WHEN <condition> SET <status>`
- R3. `FROM <path|url> WHERE <predicate>` data source clause
- R4. Graph structure commands: `CREATE NODE`, `LINK`, `GROUP BY`
- R5. Existing `SELECT ... WHERE ... SET ...` and `RESET` unchanged
- R6-R10. Panel lifecycle: layout types, SHOW fields, FILTER clause
- R11-R14. Health bindings: declarative rules, panel/node targeting, multiple sources
- R15-R18. Hybrid persistence: .ixql files + localStorage + SAVE promotion
- R19-R22. Generic renderer: DynamicPanel, FROM resolution, custom escape hatch, dynamic IconRail
- R23-R26. Graph mutations: CREATE NODE, LINK, GROUP BY, ephemeral by default

## Scope Boundaries

- TypeScript client-side parser only — F# FParsec parser NOT extended (see origin)
- No backend changes for panel rendering — panels are client-side with API data fetching
- Auth/authz for panel CRUD deferred to #30
- Existing 12 panels NOT migrated — new panels use IXQL; existing migrate opportunistically
- `custom` layout type is the escape hatch for complex panels (SeldonDashboard, CourseViewer)

## Context & Research

### Relevant Code and Patterns

- **IxqlControlParser.ts** (209 lines): Hand-rolled tokenizer + recursive descent. Clean architecture — dispatch on first keyword (`RESET`/`SELECT`), add branches for `CREATE`/`BIND`/`DROP`. Tokenizer already handles all needed token types.
- **ForceRadiant.tsx handleIxqlCommand** (lines 555-584): Receives parsed `IxqlParseResult`, applies overrides to THREE.js `userData`. New command types will route through the same handler with additional dispatch branches.
- **Panel conditional chain** (ForceRadiant.tsx lines 2504-2537): 12-entry `{activePanel === 'xxx' && <Component />}` chain. Replace with registry lookup.
- **IconRail.tsx**: Static `PanelId` union type (line 6) and `RAIL_ITEMS` array (lines 22-156). Must become dynamic.
- **DataLoader.ts**: `loadGovernanceDataAsync` pattern (fetch → JSON → apply). Base for FROM clause generalization.
- **BacklogPanel/LibraryPanel**: Representative patterns — `fetch('/api/...') → setState → render` with hardcoded fallback.
- **DemerzelIxqlDriver.ts**: Already generates IXQL command strings from rules — validates that IXQL-as-config is a natural pattern.

### Institutional Learnings

- **DSL Promotion Tiers** (docs/solutions/tooling/compound-engineering-flywheel-2026-03-07.md): 5-tier promotion system (Tier 0-4). IXQL grammar extensions should follow the `grammar-governor` anti-bloat pattern — new commands require justification by usage frequency.
- **No TypeScript parser precedents** in this repo — this is greenfield. The F# side uses FParsec but there's no TS parser library in the project.

## Key Technical Decisions

- **Extend hand-rolled parser, no library**: The existing parser is ~90 lines of clean recursive descent. DDL adds ~5-8 command types. A parser combinator library (chevrotain) would add dependency overhead disproportionate to the grammar size. Revisit if parser exceeds **300 lines** (likely after Phase 1).
- **Panel registry pattern**: Replace `PanelId` union + conditional chain with a `Map<string, PanelRegistration>` registry. Hardcoded panels register at startup; IXQL `CREATE PANEL` registers at runtime. The registry is the single source of truth for what panels exist. Use **jotai atoms** (already in project) for reactivity — not a custom event emitter.
- **Preserve PanelId type safety**: Use `type PanelId = BuiltInPanelId | string` where `BuiltInPanelId` is the existing 12-member union. Hardcoded call sites retain compile-time narrowing; the registry accepts any `string` for dynamic panels.
- **IxqlParseResult as discriminated union**: Use `{ ok: true; command: IxqlCommand } | { ok: false; error: string }` — eliminates the `{ ok: true, command: undefined }` anti-pattern. Each command variant carries only its own fields (no optional grab-bag).
- **Separate health status from registry**: Health status lives in a dedicated `Map<string, PanelStatus>` owned by the HealthBindingEngine, not in PanelRegistration. IconRail reads from both registry (metadata) and health store (status dots).
- **Split PanelRegistration**: `PanelDefinition` (serializable: id, label, icon, layout, source, fields) for persistence + `PanelRegistration` (runtime: definition + component + healthStatus) for the registry.
- **Extract IxqlCommandDispatcher**: All new command dispatch goes through `IxqlCommandDispatcher.ts` — not inline in ForceRadiant's `handleIxqlCommand`. ForceRadiant only wires the dispatcher.
- **DynamicPanel layout renderers extracted**: `ListDetailLayout.tsx`, `DashboardLayout.tsx`, `StatusLayout.tsx` as separate modules. DynamicPanel is a thin orchestrator.
- **Clause ordering is strict**: `CREATE PANEL` clauses must appear in documented order (FROM, LAYOUT, ICON, SHOW, FILTER). Keeps parser simple.
- **IconRail becomes data-driven**: `RAIL_ITEMS` becomes computed from the panel registry. Icons are SVG name lookups from a small icon catalog.
- **FROM clause uses existing API proxy**: JSON/YAML files served via `/api/governance/file-content?path=...`. API endpoints fetched directly. No client-side YAML parser needed.
- **Panel-graph relationship**: Panels **reference** graph nodes via `BIND NODES WHERE <predicate>` (linked model, not fractal). This is simpler to implement and doesn't require restructuring the flat graph data model. Fractal drill-down can evolve later via `GROUP BY` commands.
- **Template interpolation**: Simple property path resolution (`{{field.nested}}`) resolving to React props/children — never HTML string interpolation (XSS risk). No template engine.
- **localStorage version reconciliation**: Entries carry a timestamp. On startup, if the file-backed `.ixql` is newer, the localStorage override is discarded.

## Open Questions

### Resolved During Planning

- **Parser extensibility**: Confirmed — the hand-rolled parser can handle DDL with simple keyword dispatch branches. No rewrite needed.
- **IconRail dynamism**: Replace `PanelId` string literal union with plain `string`. Replace `RAIL_ITEMS` constant with a computed array from the panel registry. Existing hardcoded panels pre-register at module load.
- **FROM clause delivery**: Use the existing `/api/governance/file-content` endpoint. It already serves JSON files from the governance directory. No new backend endpoint needed.
- **Panel-graph relationship**: Linked model (panels reference nodes) rather than fractal model (panels are nodes). Simpler to implement, preserves current graph data model.

### Deferred to Implementation

- **Graph mutation animation**: How should `CREATE NODE` animate into the force layout? Likely set initial position near center with velocity, let the physics engine settle. Needs runtime experimentation.
- **SAVE PANEL file write**: The `/api/governance/file-content` endpoint currently only reads. Writing .ixql files requires a new backend route or using the existing governance file-write mechanism. Defer until the core pipeline works.
- **Icon catalog completeness**: The icon catalog for `ICON <name>` needs a mapping from short names to SVG elements. Start with ~20 common icons; expand based on usage.

## High-Level Technical Design

> *This illustrates the intended approach and is directional guidance for review, not implementation specification. The implementing agent should treat it as context, not code to reproduce.*

```
IXQL Command Flow:

  User types backtick → IxqlCommandInput
       │
       ▼
  IxqlControlParser.parseIxqlCommand()
       │
       ├── SELECT/RESET → existing visual override path
       │
       ├── CREATE PANEL → PanelRegistry.register(definition)
       │                     → IconRail re-renders (computed from registry)
       │                     → Side panel renders DynamicPanel
       │
       ├── BIND HEALTH  → HealthBindingEngine.addRule(rule)
       │                     → Polls data source on interval
       │                     → Updates PanelRegistry status
       │                     → IconRail status dots update
       │
       ├── CREATE NODE  → GraphMutator.addNode(spec)
       │   LINK/GROUP      → Force layout re-renders with animation
       │
       ├── DROP PANEL   → PanelRegistry.unregister(id)
       │
       └── SAVE PANEL   → POST .ixql file to governance state

  Persistence:
  ┌──────────────────────────┐
  │ governance/state/panels/ │ ← file-backed (version-controlled)
  │   library.ixql           │
  │   cicd-health.ixql       │
  └──────────┬───────────────┘
             │ load on startup
             ▼
  ┌──────────────────────────┐
  │ PanelRegistry (runtime)  │ ← single source of truth
  │   hardcoded panels       │
  │   + file-backed panels   │
  │   + localStorage overlay │
  └──────────┬───────────────┘
             │ computed
             ▼
  ┌──────────────────────────┐
  │ IconRail (dynamic)       │
  │ DynamicPanel (generic)   │
  └──────────────────────────┘
```

```
IXQL Grammar Extension (pseudo-EBNF):

  command     = select_cmd | reset_cmd | create_cmd | bind_cmd | drop_cmd | save_cmd | graph_cmd

  create_cmd  = "CREATE" "PANEL" id from_clause layout_clause [icon_clause] [show_clause] [filter_clause]
  from_clause = "FROM" source_path [where_clause]
  layout_clause = "LAYOUT" ("list-detail" | "dashboard" | "status" | "custom")
  icon_clause = "ICON" icon_name
  show_clause = "SHOW" field_list
  filter_clause = "FILTER" field "AS" ("chips" | "dropdown" | "search")

  bind_cmd    = "BIND" ("PANEL" id | "NODE" selector) "HEALTH" "FROM" source when_clauses
  when_clauses = when_clause+ [else_clause]
  when_clause = "WHEN" predicate "SET" status
  else_clause = "ELSE" "SET" status

  drop_cmd    = "DROP" "PANEL" id
  save_cmd    = "SAVE" ("PANEL" id | "GRAPH")

  graph_cmd   = create_node | link_cmd | group_cmd
  create_node = "CREATE" "NODE" id "TYPE" type ["IN" parent_id]
  link_cmd    = "LINK" id "TO" id ["TYPE" edge_type]
  group_cmd   = "GROUP" selector "BY" field
```

## Implementation Units

### Phase 1: Foundation (Parser + Registry)

- [ ] **Unit 0: Verify existing panel wiring**

**Goal:** Ensure all 12+ existing panels render correctly before building the dynamic system.

**Requirements:** Prerequisite for all subsequent units.

**Dependencies:** None

**Files:**
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

**Approach:**
- Verify the `{activePanel === 'library' && <LibraryPanel />}` render case exists in the conditional chain
- Verify all PanelId values in IconRail have corresponding render cases

**Patterns to follow:**
- Existing conditional chain pattern at ForceRadiant.tsx lines 2504-2537

**Test scenarios:**
- Happy path: Each PanelId in the union has a corresponding render case in ForceRadiant
- Edge case: Clicking each icon rail button opens the expected panel content (manual verification)

**Verification:**
- All panel icons in the rail open their panels. No blank/empty panels on click.

---

- [ ] **Unit 1: Extend IXQL parser with command type discriminated union**

**Goal:** Restructure the parser's output type to support multiple command families (select, reset, create-panel, bind-health, drop, create-node, link, group, save) while keeping existing SELECT/RESET working.

**Requirements:** R1, R5

**Dependencies:** None

**Files:**
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IxqlControlParser.ts`
- Test: `ReactComponents/ga-react-components/src/__tests__/IxqlControlParser.test.ts` (create)

**Approach:**
- Replace the flat `IxqlCommand` interface with a discriminated union on `type` field
- Add new command type interfaces: `CreatePanelCommand`, `BindHealthCommand`, `DropCommand`, `CreateNodeCommand`, `LinkCommand`, `GroupCommand`, `SaveCommand`
- Add keyword dispatch branches in `parseIxqlCommand()`: `CREATE`, `BIND`, `DROP`, `SAVE`, `LINK`, `GROUP`
- Sub-dispatch `CREATE` into `CREATE PANEL` vs `CREATE NODE` based on second token
- Keep `parseIxqlCommand` return type as a union that includes all command types
- The tokenizer needs no changes — it already handles all needed token types

**Patterns to follow:**
- Existing keyword dispatch at IxqlControlParser.ts line 87 (`if (peek() === 'RESET')`)
- Existing predicate/assignment parsing for reuse in WHERE/WHEN clauses

**Test scenarios:**
- Happy path: `SELECT nodes WHERE type='policy' SET glow=true` parses identically to before
- Happy path: `RESET` still works
- Happy path: `CREATE PANEL test FROM /api/test LAYOUT list-detail` parses to `CreatePanelCommand` with correct fields
- Happy path: `CREATE PANEL test FROM /api/test LAYOUT dashboard ICON chart SHOW name, value FILTER category AS chips` parses all clauses
- Happy path: `BIND PANEL cicd HEALTH FROM /api/cicd WHEN failure > 0 SET error ELSE SET ok` parses with conditions
- Happy path: `DROP PANEL test` parses correctly
- Happy path: `CREATE NODE new-pol TYPE policy IN demerzel` parses to `CreateNodeCommand`
- Happy path: `LINK new-pol TO pol-seldon TYPE policy-persona` parses correctly
- Happy path: `GROUP nodes WHERE repo='demerzel' BY type` parses correctly
- Happy path: `SAVE PANEL test` and `SAVE GRAPH` parse correctly
- Error path: `CREATE` without subcommand returns parse error
- Error path: `CREATE PANEL` without required FROM clause returns error
- Edge case: Quoted paths with spaces: `FROM '/api/governance/file content'`
- Edge case: Case insensitivity for keywords: `create panel` works like `CREATE PANEL`

**Verification:**
- All existing IXQL tests pass unchanged
- New command types parse correctly with all clauses
- Parse errors are descriptive and point to the problem token

---

- [ ] **Unit 2: Panel registry and dynamic IconRail**

**Goal:** Replace the static `PanelId` union type and hardcoded `RAIL_ITEMS` with a runtime panel registry. Existing panels pre-register; IXQL-created panels register dynamically.

**Requirements:** R6, R10, R22

**Dependencies:** Unit 1

**Files:**
- Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/PanelRegistry.ts`
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IconRail.tsx`
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/index.ts`
- Test: `ReactComponents/ga-react-components/src/__tests__/PanelRegistry.test.ts` (create)

**Approach:**
- Create `PanelRegistry` as a reactive store (simple event emitter pattern or jotai atom) with `register()`, `unregister()`, `getAll()`, `get(id)`
- `PanelRegistration` type: `{ id: string, label: string, icon: string | ReactNode, renderMode: 'side' | 'overlay', component?: React.FC, definition?: IxqlPanelDefinition, healthStatus?: PanelStatus }`
- Pre-register all existing hardcoded panels at module load time (library, cicd, agent, etc.)
- Create an `ICON_CATALOG` map: short names (book, chart, gear, brain, etc.) → SVG ReactNode elements. Extract existing SVGs from RAIL_ITEMS.
- Change `PanelId` from string literal union to plain `string`
- `RAIL_ITEMS` becomes a computed getter from the registry
- IconRail accepts the computed items list as a prop or reads from registry context
- ForceRadiant replaces the conditional chain with: for hardcoded panels, a registry lookup to component; for IXQL panels, render `DynamicPanel`

**Patterns to follow:**
- Existing `RAIL_ITEMS` structure for registration shape
- jotai atoms already used in the project for state

**Test scenarios:**
- Happy path: All 12+ existing panels appear in the rail after pre-registration
- Happy path: `register()` adds a new panel that appears in the rail
- Happy path: `unregister()` removes a panel from the rail
- Happy path: Status updates propagate to the rail icon dots
- Edge case: Registering a panel with a duplicate ID replaces the existing one
- Edge case: Unregistering a non-existent ID is a no-op
- Integration: Clicking a dynamically registered panel opens the side panel area

**Verification:**
- All existing panels render identically to before
- The registry is the single source of truth — no more PanelId union or RAIL_ITEMS constant
- New panels can be added via `register()` without any file changes

---

### Phase 2: Data Pipeline (FROM + DynamicPanel)

- [ ] **Unit 3: Data source fetcher for FROM clause**

**Goal:** Build a generic data fetcher that resolves IXQL `FROM` sources — JSON files via governance API, REST endpoints, or in-memory graph subsets.

**Requirements:** R3, R20

**Dependencies:** Unit 1

**Files:**
- Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/DataFetcher.ts`
- Test: `ReactComponents/ga-react-components/src/__tests__/DataFetcher.test.ts` (create)

**Approach:**
- `DataFetcher.resolve(source: string, predicates?: IxqlPredicate[]): Promise<unknown[]>`
- Source URL patterns:
  - `/api/...` or `http...` → direct fetch + JSON parse
  - `governance/...` → rewrite to `/api/governance/file-content?path=...` + JSON parse
  - `graph://nodes` or `graph://edges` → return current graph data (passed as context)
- Apply WHERE predicates client-side using the existing `evaluatePredicate()` function
- Return data as `unknown[]` — the DynamicPanel handles field extraction
- Add polling option: `DataFetcher.poll(source, interval, callback)` for live health sources

**Patterns to follow:**
- Existing `fetch('/api/governance/backlog')` pattern in BacklogPanel
- Existing `evaluatePredicate()` in IxqlControlParser.ts for client-side filtering

**Test scenarios:**
- Happy path: Resolve an API URL returns fetched JSON data
- Happy path: Resolve a governance file path rewrites to file-content endpoint
- Happy path: Resolve `graph://nodes` returns current graph node array
- Happy path: WHERE predicates filter results correctly
- Error path: Network failure returns empty array (graceful degradation)
- Error path: Invalid JSON response returns empty array with console warning
- Edge case: Relative paths normalized correctly

**Verification:**
- DataFetcher can resolve all three source types
- Predicate filtering works identically to SELECT WHERE evaluation

---

- [ ] **Unit 4: DynamicPanel generic renderer**

**Goal:** Build a single React component that renders any IXQL-defined panel based on its layout type, field mappings, and data source.

**Requirements:** R7, R8, R9, R19, R20, R21

**Dependencies:** Unit 2, Unit 3

**Files:**
- Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/DynamicPanel.tsx`
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/styles.css`
- Test: `ReactComponents/ga-react-components/src/__tests__/DynamicPanel.test.ts` (create)

**Approach:**
- Props: `definition: IxqlPanelDefinition` (from parser output or registry)
- On mount: use DataFetcher to resolve FROM source, apply WHERE predicates
- Poll interval: re-fetch every 60s (configurable via definition)
- Layout renderers (internal to DynamicPanel):
  - `list-detail`: header/toggle/items with expandable detail — mirrors BacklogPanel/LibraryPanel pattern
  - `dashboard`: metric cards in a grid — mirrors LLMStatus pattern
  - `status`: provider rows with status dots — mirrors LLMStatus/AgentPanel pattern
  - `custom`: renders nothing (escape hatch — the registry lookup provides the hardcoded component instead)
- SHOW fields: resolve `{{field.path}}` from data items using dot-path resolution
- FILTER clause: render filter chips/dropdown above the list, filter data client-side
- CSS: reuse existing `.prime-radiant__` naming convention; add `.prime-radiant__dynamic-*` classes

**Patterns to follow:**
- BacklogPanel structure (header/toggle/body/items with expandable sections)
- LibraryPanel filter chips pattern
- LLMStatus status row pattern

**Test scenarios:**
- Happy path: `list-detail` layout renders items with title and expandable detail from SHOW fields
- Happy path: `dashboard` layout renders metric cards from data
- Happy path: `status` layout renders provider rows with status dots
- Happy path: FILTER chips render and filter data interactively
- Happy path: Data re-fetches on 60s interval
- Edge case: Empty data source renders "No data available" message
- Edge case: Missing SHOW fields in data items render gracefully (show field name as placeholder)
- Error path: DataFetcher failure shows error state in panel

**Verification:**
- A panel created via `CREATE PANEL test FROM /api/governance/backlog LAYOUT list-detail SHOW section, items` renders the backlog data without any React code changes

---

### Phase 3: Health Bindings

- [ ] **Unit 5: Health binding engine**

**Goal:** Implement the `BIND ... HEALTH` command — declarative rules that compute panel/node health status from polled data sources.

**Requirements:** R2, R11, R12, R13, R14

**Dependencies:** Unit 1, Unit 2, Unit 3

**Files:**
- Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/HealthBindingEngine.ts`
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`
- Test: `ReactComponents/ga-react-components/src/__tests__/HealthBindingEngine.test.ts` (create)

**Approach:**
- `HealthBindingEngine` manages a list of health binding rules
- Each rule: `{ target: { type: 'panel' | 'node', selector: string }, source: string, conditions: HealthCondition[], fallback: PanelStatus }`
- On startup + 60s interval: for each rule, fetch FROM source, evaluate WHEN conditions against the data, compute status
- Panel targets: update `PanelRegistry.setHealth(panelId, status)` → IconRail status dots
- Node targets: update matching graph nodes' `healthStatus` → 3D visualization
- Conditions reuse `evaluatePredicate()` from the parser — same comparison operators
- Replace existing `useLLMHealth` and `useCICDHealth` hooks with equivalent BIND rules in .ixql files

**Patterns to follow:**
- Existing `useCICDHealth` hook logic (fetch → compute status from latest runs)
- Existing `useLLMHealth` hook logic (fetch providers → map to ok/warn/error)
- DemerzelIxqlDriver rule evaluation pattern

**Test scenarios:**
- Happy path: `BIND PANEL cicd HEALTH FROM /api/cicd WHEN failure > 0 SET error WHEN running > 0 SET warn ELSE SET ok` — panel status updates based on fetched data
- Happy path: `BIND NODE WHERE repo='ga' HEALTH FROM /api/cicd/ga WHEN failure > 0 SET error ELSE SET ok` — graph nodes update
- Happy path: Multiple WHEN conditions evaluated in order, first match wins
- Happy path: ELSE clause used when no WHEN matches
- Edge case: Data source unavailable — status set to null (no dot shown)
- Edge case: Binding with no WHEN clauses and only ELSE — always returns fallback status
- Integration: Health status change propagates through PanelRegistry to IconRail status dot within one poll cycle

**Verification:**
- The hardcoded `useLLMHealth` and `useCICDHealth` hooks can be replaced by equivalent BIND HEALTH .ixql definitions with identical behavior

---

### Phase 4: Persistence + Graph Mutations

- [ ] **Unit 6: Hybrid persistence — .ixql files + localStorage**

**Goal:** Panel and health definitions persist across page reloads. File-backed definitions load from governance state; runtime definitions survive in localStorage.

**Requirements:** R15, R16, R17, R18

**Dependencies:** Unit 1, Unit 2, Unit 5

**Files:**
- Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IxqlPersistence.ts`
- Create: `governance/state/panels/` (directory)
- Create: `governance/state/panels/cicd-health.ixql` (example)
- Create: `governance/state/panels/llm-health.ixql` (example)
- Test: `ReactComponents/ga-react-components/src/__tests__/IxqlPersistence.test.ts` (create)

**Approach:**
- On startup: fetch `.ixql` files from `/api/governance/file-content?path=governance/state/panels/` (directory listing)
- Parse each `.ixql` file through the IXQL parser
- Execute the parsed commands against the PanelRegistry and HealthBindingEngine
- Then load localStorage overlay (`prime-radiant-ixql-overlay`) — parse and execute
- Runtime IXQL commands (from command input) auto-persist to localStorage
- `SAVE PANEL <id>` writes the definition to a governance state file (requires backend write endpoint — deferred; for now, outputs the .ixql content to clipboard/console)
- `DROP PANEL <id>` removes from both registry and localStorage

**Patterns to follow:**
- Existing localStorage usage in CourseViewer (queue persistence)
- Existing `/api/governance/file-content` endpoint for file reads

**Test scenarios:**
- Happy path: .ixql file with CREATE PANEL loads and registers panel on startup
- Happy path: .ixql file with BIND HEALTH loads and creates health binding on startup
- Happy path: Runtime CREATE PANEL persists to localStorage
- Happy path: Page reload restores localStorage panels
- Happy path: File-backed panels load before localStorage overlay
- Edge case: Malformed .ixql file logs warning and continues loading remaining files
- Edge case: localStorage panel with same ID as file-backed panel — localStorage wins (overlay)

**Verification:**
- Create a panel via IXQL command input, reload the page, panel reappears
- Example .ixql files parse and register correctly on startup

---

- [ ] **Unit 7: Graph structure commands**

**Goal:** Enable runtime graph mutations via IXQL: adding nodes, creating links, and grouping nodes.

**Requirements:** R4, R23, R24, R25, R26

**Dependencies:** Unit 1, Unit 2

**Files:**
- Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/GraphMutator.ts`
- Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`
- Test: `ReactComponents/ga-react-components/src/__tests__/GraphMutator.test.ts` (create)

**Approach:**
- `GraphMutator` receives a reference to the force graph instance and the current graph data
- `CREATE NODE`: add to graph data, set initial position near graph center, let physics settle. Apply default health metrics and colors based on TYPE.
- `LINK`: add edge between existing nodes. Validate both endpoints exist.
- `GROUP BY`: create a virtual parent node, re-parent matching child nodes. The parent node's health is the aggregate of children. This is the entry point for fractal drill-down.
- All mutations are ephemeral by default — stored in a mutation log. `SAVE GRAPH` is deferred (same as SAVE PANEL — requires backend write).
- After mutation: call `fg.graphData()` with updated data to trigger force layout re-render

**Patterns to follow:**
- Existing CI/CD → graph node health propagation effect in ForceRadiant.tsx
- Existing `updateNodeHealth()` in DataLoader.ts for in-place node updates

**Test scenarios:**
- Happy path: `CREATE NODE test-node TYPE policy` adds a visible node to the graph
- Happy path: `LINK test-node TO pol-seldon TYPE cross-repo` creates a visible edge
- Happy path: `GROUP nodes WHERE repo='demerzel' BY type` creates group nodes with children
- Edge case: CREATE NODE with duplicate ID replaces the existing node
- Error path: LINK with non-existent target returns error
- Edge case: GROUP with no matching nodes is a no-op

**Verification:**
- New nodes appear in the 3D visualization with correct colors and health
- Links render between the correct node pairs
- GROUP creates a visual cluster that can be conceptually drilled into

## System-Wide Impact

- **Interaction graph:** The IXQL handler in ForceRadiant.tsx becomes the central dispatch for all panel, health, and graph mutations. The DemerzelIxqlDriver and VisualCriticLoop continue to emit SELECT/RESET commands unchanged.
- **Error propagation:** Parse errors surface in the IxqlCommandInput error display. Runtime errors (fetch failure, missing nodes) log to console and show degraded panel states — never crash the 3D graph.
- **State lifecycle risks:** localStorage overlay grows unboundedly — add a size check and warn at >100KB. The panel registry is in-memory — page navigation resets all runtime state (by design).
- **API surface parity:** No new backend endpoints required for Phase 1-3. Phase 4 persistence needs a file-write endpoint (deferred).
- **Unchanged invariants:** The 3D force graph rendering, node health computation, algedonic signals, viewer presence, and all existing panels continue to work without modification.

## Risks & Dependencies

| Risk | Mitigation |
|------|------------|
| Parser complexity creeps beyond hand-rolled capacity | Grammar is bounded (~10 command types). Monitor line count — if parser exceeds 500 lines, evaluate chevrotain. |
| Dynamic panels lack visual polish vs. hardcoded ones | Start with list-detail layout (most common pattern). Iterate on CSS using existing panel styles as baseline. |
| Performance with many health bindings polling | Cap at 20 concurrent health poll sources. Health bindings share a single poll timer. |
| localStorage size growth | Add size warning at 100KB. SAVE PANEL promotes to file-backed, clearing localStorage entry. |

## Sources & References

- **Origin document:** [docs/brainstorms/2026-03-28-ixql-ui-grammar-requirements.md](docs/brainstorms/2026-03-28-ixql-ui-grammar-requirements.md)
- Related code: `IxqlControlParser.ts`, `ForceRadiant.tsx`, `IconRail.tsx`, `DataLoader.ts`
- Related issues: #38 (Library panel), #39 (System health → graph visualization)
- Institutional: DSL promotion tiers (docs/solutions/tooling/compound-engineering-flywheel-2026-03-07.md)
