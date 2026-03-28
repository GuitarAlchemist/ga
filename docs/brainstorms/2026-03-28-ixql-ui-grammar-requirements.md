---
date: 2026-03-28
topic: ixql-ui-grammar
---

# IXQL UI Grammar — Declarative Panel & Graph Pipeline

## Problem Frame

The Prime Radiant requires 7+ file touchpoints to add a new panel (component, PanelId type, icon, health hook, ForceRadiant wiring, CSS, index export). Every panel follows the same pattern (header/toggle/body/items) but is hand-coded in React. Health indicators, data bindings, and graph node relationships are all ad-hoc per-panel.

IXQL currently handles only node/edge visualization (`SELECT ... WHERE ... SET ...`). The language should expand to become the primary interface for the entire Prime Radiant — panels, health bindings, data sources, and graph topology — eliminating per-panel React code for standard layouts.

This is a three-layer evolution: developer velocity (no more boilerplate) → runtime extensibility (create panels via IXQL commands) → fractal information architecture (panels as graph views).

## Requirements

**IXQL Grammar Extension**

- R1. Extend the IXQL parser with DDL-like commands: `CREATE PANEL`, `ALTER PANEL`, `DROP PANEL`, `SAVE PANEL`
- R2. Add `BIND ... HEALTH FROM <source> WHEN <condition> SET <status>` syntax for declarative health status rules
- R3. Add `FROM <path|url> WHERE <predicate>` clause for querying JSON/YAML files, API endpoints, or graph subsets as data sources
- R4. Add graph structure commands: `CREATE NODE`, `LINK <source> TO <target>`, `GROUP <nodes> BY <field>` for runtime topology modification
- R5. The existing `SELECT ... WHERE ... SET ...` and `RESET` commands must continue to work unchanged

**Panel Lifecycle**

- R6. `CREATE PANEL <id> FROM <source> LAYOUT <type> ICON <name>` defines a new panel with data source, layout renderer, and icon
- R7. Support layout types: `list-detail` (expandable items), `dashboard` (metric cards + charts), `status` (provider status rows), `custom` (escape hatch to React component)
- R8. `SHOW <field>, <field>, ...` specifies which fields from the data source to display
- R9. `FILTER <field> AS chips|dropdown|search` adds interactive filters to the panel
- R10. `DROP PANEL <id>` removes a panel from the rail and renderer

**Health Bindings**

- R11. Health bindings are declarative rules that compute a panel's icon rail status dot from a data source
- R12. Rules use `WHEN <condition> SET <ok|warn|error|critical>` with fallback `ELSE SET <status>`
- R13. Health bindings can target panels (`BIND PANEL <id> HEALTH`) or graph nodes (`BIND NODE <selector> HEALTH`)
- R14. Health sources include: API endpoints, graph node health aggregates, external checks (GitHub Actions, Ollama), and computed expressions

**Persistence — Hybrid Model**

- R15. Base panel definitions live in `governance/state/panels/*.ixql` files — version-controlled, loaded on startup
- R16. Runtime IXQL commands create ephemeral panel overlays stored in localStorage
- R17. `SAVE PANEL <id>` promotes an ephemeral panel to a file in `governance/state/panels/`
- R18. On startup, file-backed panels load first, then localStorage overlays merge on top

**Generic Panel Renderer**

- R19. A single `DynamicPanel` React component renders any IXQL-defined panel based on its layout type and field mappings
- R20. The `DynamicPanel` resolves `FROM` sources — fetching from API, reading from governance files, or querying the graph
- R21. Custom panels (SeldonDashboard, CourseViewer, 3D graph) remain as hand-coded React components registered under `LAYOUT custom`
- R22. The IconRail dynamically includes IXQL-defined panels alongside hardcoded ones, with no PanelId type changes needed

**Graph Structure Commands**

- R23. `CREATE NODE <id> TYPE <type> IN <parent>` adds a node to the graph at runtime
- R24. `LINK <source> TO <target> TYPE <edge-type>` creates edges between nodes
- R25. `GROUP <selector> BY <field>` clusters matching nodes into a parent group node — enabling fractal drill-down
- R26. Graph mutations are ephemeral by default; `SAVE GRAPH` persists to governance state

## Success Criteria

- A new "ML Metrics" panel can be created entirely via an IXQL command — zero React code, zero file changes
- Health indicators on the icon rail are driven by IXQL `BIND` rules, not per-panel hooks
- The existing 12 panels continue to work (backwards compatible)
- Panel definitions are readable by non-developers ("I can see what the Library panel does by reading its .ixql file")
- IXQL commands entered in the command input (`backtick`) take effect immediately

## Scope Boundaries

- The F# IXQL parser (FParsec-based, in GA.Business.DSL) is NOT extended in this work — this is the TypeScript client-side parser only
- No backend changes for panel rendering — panels are client-side with API data fetching
- Authentication/authorization for panel CRUD is out of scope (deferred to #30)
- Migration of all 12 existing panels to IXQL is NOT required — only new panels must use IXQL; existing panels migrate opportunistically
- The `custom` layout type is the escape hatch — complex panels like SeldonDashboard or CourseViewer stay as React

## Key Decisions

- **IXQL-native from day one**: No intermediate YAML config layer. The IXQL grammar IS the config format. Avoids throwaway work.
- **Hybrid persistence**: File-backed base + localStorage overlay. Balances auditability with runtime experimentation.
- **Panel-graph relationship**: Deferred to planning. Needs codebase exploration to determine whether panels should BE node views or reference nodes.
- **TypeScript parser only**: The client-side parser extends independently of the F# FParsec parser. They may converge later but are separate for now.

## Dependencies / Assumptions

- The existing `IxqlControlParser.ts` hand-rolled tokenizer/parser can be extended without a parser generator (assumption — planning should validate complexity)
- The `FROM` clause needs a generic data fetcher that handles JSON files, YAML, and API endpoints (new infrastructure)
- The IconRail must become dynamic — currently a hardcoded `RAIL_ITEMS` array with static `PanelId` union type

## Outstanding Questions

### Resolve Before Planning

(none — all blocking questions resolved during brainstorm)

### Deferred to Planning

- [Affects R6, R22][Needs research] How should the IconRail transition from static `PanelId` union type to dynamic panel registration? What's the minimal React change?
- [Affects R23-R26][Technical] What's the interaction model between IXQL graph mutations and the force-directed layout? Do new nodes animate in?
- [Affects R19-R20][Technical] Should `DynamicPanel` use a template engine for field interpolation (e.g., `{{title}}`) or a simpler property mapping?
- [Affects R1-R4][Needs research] Is the hand-rolled tokenizer/parser in IxqlControlParser.ts extensible enough for DDL commands, or should we adopt a parser combinator library?
- [Affects R15][Technical] How do .ixql files get served to the client? Via the existing `/api/governance/file-content` endpoint or a dedicated panel endpoint?
- [Affects all][Architecture] Should panels BE graph node views (fractal) or reference graph nodes (linked)? Requires exploring the current graph data model constraints.

## Next Steps

`/ce:plan` for structured implementation planning
