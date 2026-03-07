---
title: "Compound Engineering Flywheel — Subagents, Master Skill, Hooks, /ga evolve"
date: 2026-03-07
category: "tooling"
tags: [compound-engineering, subagents, fsharp, dsl, claude-code, hooks, notebooklm]
symptoms: "No automated step asks 'should any of this become a higher-level abstraction?' after shipping a feature; repeated patterns accumulate unnoticed across DSL files"
components:
  - .agent/agents/compound-researcher.md
  - .agent/agents/fsharp-architect.md
  - .agent/agents/grammar-governor.md
  - .agent/skills/compound/SKILL.md
  - .claude/settings.json
  - .claude/commands/ga.md
  - docs/compound/
severity: "architectural"
---

# Compound Engineering Flywheel

## Problem

After shipping Phases 3 and 4 of the GA Language DSL (LSP ga-blocks, FParsec surface syntax,
`surface.transpile` closure, `TranspileGaScript` MCP tool), there was no automated step to ask:
*"Should any of this become a higher-level abstraction?"*

Patterns were accumulating silently:
- 50+ `Map.ofList [ "key", "desc" ]` schema declarations across all `BuiltinClosures/` files
- 110+ identical completion-item record literals in `CompletionProvider.fs`
- 12+ repetitions of F# Result unwrapping boilerplate in C# (`if (result.IsOk) { ... }`)
- LSP offset logic copy-pasted into 4 separate message handlers

The missing piece was a **recursive improvement loop** — a mechanism that inspects work after it
ships and promotes repeated patterns into permanent language-level abstractions.

## Solution

Built the full **Compound Engineering Flywheel** in one session:

### 1. Three Specialist Subagents (`.agent/agents/`)

| Agent | Model | Role | Tools |
|---|---|---|---|
| `compound-researcher` | Haiku | Read-only pattern miner | Glob, Grep, Read |
| `fsharp-architect` | Sonnet | F# promotion tier designer | Read, Glob |
| `grammar-governor` | Haiku | Anti-bloat gatekeeper, audits closure registry + surface syntax | Read, Glob, Grep |

Each agent has explicit **rules** (e.g., `compound-researcher` and `grammar-governor` must NEVER
edit files), a defined output format, and a promotion tier vocabulary.

**Promotion tiers** (fsharp-architect):
| Tier | Construct | Use when |
|---|---|---|
| 0 | `let` helper function | Pure transformation, no sequencing |
| 1 | Discriminated Union case | New vocabulary / sum type |
| 2 | Computation Expression operator | Sequenced computation with context |
| 3 | DSL surface syntax clause | User-facing keyword (requires human sign-off) |
| 4 | MCP tool | External agent access needed |

### 2. Master `/compound` Skill (`.agent/skills/compound/SKILL.md`)

Single entrypoint that orchestrates the full loop:
```
scope (git log + diff) → compound-researcher → fsharp-architect → grammar-governor → Compound Report
```

Escalation rules:
- Grammar governor returns `BLOCK PROMOTION` → stop and list what must be resolved
- fsharp-architect proposes Tier 3 DSL clause → require human sign-off before implementing
- 5+ occurrences of same pattern → treat as P0, promote immediately

Reports saved to `docs/compound/<YYYY-MM-DD>-<feature>.md`.

### 3. Claude Code Hooks (`.claude/settings.json`)

Three hooks added:

```json
"hooks": {
  "SessionStart": [
    // Injects branch, last 3 commits, dirty state, available skills at session open
  ],
  "PostToolUse": [
    // DSL files changed → auto-runs GA.Business.DSL.Tests (fast, --no-build)
    // Agent config files changed → alerts to prevent stealth mutation
  ]
}
```

### 4. `/ga evolve` Command (`.claude/commands/ga.md`)

Added `evolve` sub-command to the existing `/ga` Claude Code command. Maps natural language
("improve the language", "find patterns", "compound") to the flywheel. Equivalent to `/compound`.

## First Run Results

Ran the flywheel immediately on Phase 3+4 DSL work. All three subagents executed in parallel:

**compound-researcher** found 5 patterns (50+ `Map.ofList`, 110+ completion items, 12+ Result
unwrapping, 6+ `Invoke` wrapping, 4× LSP offset logic).

**grammar-governor** audited 31 closures + 12 MCP tools. Verdict: **STABLE / GO**.
Zero blocking issues. Four non-blocking warnings (Work/Reason node kinds undocumented,
`reportFailures` low usage, `tab.*` category ambiguity, shared HTTP client).

**fsharp-architect** designed three promotions (all Tier 0–1, low risk):
- `schemaOf |> param |> build` + `invokeWith` in `GaClosureRegistry.fs`
- `FSharpResultExtensions.Match(onOk, onErr)` in `GA.Core/Functional/FSharpInterop.cs`
- `mkKeywordCompletion` / `mkFunctionCompletion` helpers in `CompletionProvider.fs`

Full report: `docs/compound/2026-03-07-phase-3-4-dsl.md`

## Key Learnings

### Subagent design
- **Read-only agents (researcher, governor) must state "NEVER edit files" explicitly** — without
  this constraint, agents drift toward fixing things rather than reporting them.
- **Haiku for audit/mining, Sonnet for design** — Haiku is fast enough for Glob/Grep sweeps;
  Sonnet's stronger reasoning improves promotion proposals.
- Running researcher and governor **in parallel** works well — they target different files.
  Architect runs after researcher (depends on its output).

### Hook design
- `PostToolUse` with `Edit|Write` matcher fires on every file edit — DSL test hook is fast
  (`--no-build`) so it doesn't slow editing; triggers rebuild signal only.
- `SessionStart` hook is the highest-value hook: injects branch context that otherwise requires
  manual `git log` every session.
- **Protect agent config files** with a hook that alerts on mutation — without this, agent
  definitions can be accidentally edited mid-session.

### Grammar governor value
- Running a dedicated "rejection agent" before promotions surfaces issues that optimistic
  agents miss (e.g., a `reportFailures` closure that exists but is never actually called by any
  live pipeline).
- The governor's **cardinality check** on `NodeKind` is a useful proxy for grammar health.
  6 cases = healthy. 10+ = smell.

### NotebookLM MCP integration
- `list_notebooks` is a **local library** — it only shows notebooks you've explicitly registered
  via `add_notebook`. It does NOT auto-discover from your Google account.
- Must call `re_auth` with `show_browser: true` so you can pick the correct Google account
  (the MCP uses its own Chrome profile, not your system Chrome).
- Notebook URL from the browser address bar can be used directly with `add_notebook`.
- Library file: `%LOCALAPPDATA%\notebooklm-mcp\Data\library.json`

## Reuse

Run `/ga evolve` (or `/compound`) after any feature branch to trigger the full loop.
The three subagents in `.agent/agents/` can also be invoked individually for focused work:

```
# Just mine patterns in a specific file
Agent: compound-researcher — "Mine DomainClosures.fs for repeated patterns"

# Just audit the grammar
Agent: grammar-governor — "Audit GaSurfaceSyntaxParser.fs for redundant syntax"

# Just design a promotion
Agent: fsharp-architect — "Should this pattern become a CE operator or a helper?"
```
