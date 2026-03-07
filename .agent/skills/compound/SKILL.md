---
name: "Compound"
description: "Master compound engineering skill. Inspects recent work, mines patterns, proposes F# promotions, audits grammar stability, and produces a structured Compound Report. Run after any feature branch to find abstraction opportunities."
---

# /compound

The **compound engineering flywheel** for Guitar Alchemist. Run this after shipping a feature to extract lessons and identify what should be promoted into a higher-level abstraction.

## When to Run

- After merging or completing a feature branch
- When you notice yourself writing similar code for the third time
- Before planning the next feature (to see if new vocabulary has emerged)
- On a cadence: weekly or after every 3–4 commits

## What It Does

Six-step loop — every completed feature feeds back into the language:

```
Work → Reflect → Compound → Promote → Encode → Govern
```

| Step | Who | What |
|---|---|---|
| **Work** | — | Feature is done; git diff is the input |
| **Reflect** | compound-researcher | Mine changed files for repeated patterns |
| **Compound** | compound-researcher | Rank patterns by recurrence + impact |
| **Promote** | fsharp-architect | Decide tier (helper / DU / CE / DSL / MCP tool) per pattern |
| **Encode** | fsharp-architect | Produce concrete F# design sketch with usage examples |
| **Govern** | grammar-governor | Audit stability; block or clear each promotion |

```
Recent work
    └─► compound-researcher  (Reflect + Compound)
            └─► fsharp-architect   (Promote + Encode)
                    └─► grammar-governor  (Govern)
                              └─► Compound Report
```

## How to Invoke

```
/compound
```

No arguments needed. The skill reads git log and recent file changes automatically.

---

## Execution Steps

### Step 1 — Scope the work

```bash
git log --oneline -10
git diff --stat HEAD~5..HEAD
```

Identify which files changed. Focus the rest of the analysis on those files and their immediate neighbours.

### Step 2 — Run compound-researcher

Delegate to the `compound-researcher` agent:

> "Mine these changed files for repeated patterns that are candidates for abstraction. Files: [list]. Report the top 3–5 patterns."

### Step 3 — Run fsharp-architect

For each pattern found, delegate to `fsharp-architect`:

> "Given these patterns, propose the ideal F# promotion tier (helper / DU / CE / DSL clause / MCP tool) with concrete design. Patterns: [paste researcher output]."

### Step 4 — Run grammar-governor

Delegate to `grammar-governor`:

> "Audit the current GA Language grammar and closure registry for bloat or instability. Produce a stability verdict."

### Step 5 — Produce Compound Report

Consolidate all subagent outputs into the report format below.

---

## Compound Report Format

```markdown
# Compound Report — <date> — <branch or feature name>

## Summary
<2-3 sentences: what was built, what was found>

## Patterns Found (compound-researcher)
<paste researcher output>

## Proposed Promotions (fsharp-architect)
<paste architect proposals>

## Grammar Audit (grammar-governor)
<paste governor verdict>

## Recommended Actions
| Priority | Action | Effort | Target file |
|---|---|---|---|
| P1 | Promote `<pattern>` to `<abstraction>` | S/M/L | `path/to/file.fs` |
| P2 | ... | | |

## Deferred
<patterns that are interesting but not ready — revisit in N cycles>
```

Save the report to `docs/compound/<YYYY-MM-DD>-<feature>.md`.

---

## Escalation Rules

- If grammar-governor returns **BLOCK PROMOTION** — stop. Fix the flagged issues before adding any new abstractions.
- If fsharp-architect proposes a **Tier 3 DSL clause** — require human sign-off before implementing. New keywords are high-cost.
- If compound-researcher finds **5+ occurrences** of the same pattern — treat as P0, promote immediately.
