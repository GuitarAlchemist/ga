---
date: 2026-03-06
topic: unified-product-planning-system
---

# Brainstorm: Unified Product Planning System

## Problem

Four overlapping planning systems coexist with status drift and no single source of truth:

| System | Tool | State |
|---|---|---|
| `conductor/` | Gemini CLI | Fully replaced by Claude Code — stale, orphaned |
| `docs/plans/` + `docs/brainstorms/` | Claude Code / compound-engineering | Active, authoritative in practice |
| `docs/Roadmap/` | Manual | Last updated Nov 2025 — entirely stale |
| `todos/` | Code review audit | All resolved — one-time artifact |

CLAUDE.md mentions the `/feature` skill but does not state which planning system governs new work, creating ambiguity.

## What We're Building

A single, minimal planning system with:

1. **`docs/plans/`** as the authoritative per-feature record (brainstorm → plan → PR)
2. **`BACKLOG.md`** at the repo root as the lightweight future-ideas list
3. **CLAUDE.md** updated to name `docs/plans/` + `BACKLOG.md` as the canonical planning entry points
4. **`docs/archive/`** to preserve historical artifacts without cluttering active navigation

## Key Decisions

### Decision 1: One system wins — `docs/plans/`

`docs/plans/` already contains the most detailed, up-to-date, and actionable information. It is produced by the `/feature` skill (which is already in CLAUDE.md) and uses conventional-commit types and status frontmatter. It will be the only active per-feature planning system going forward.

**Rejected alternatives:**
- Layered conductor + docs/plans: conductor is tied to Gemini CLI, which is no longer in use. Maintaining two systems adds overhead without benefit.
- New STATUS.md hub: adds a third artifact to keep in sync. Unnecessary when docs/plans/ frontmatter already carries status.

### Decision 2: BACKLOG.md at repo root

A single flat Markdown file. Format: a bulleted list of ideas with optional priority notes. When an idea is ready to build, run `/feature` and it gets a brainstorm + plan; at that point, remove it from the backlog. No other tooling needed.

### Decision 3: Archive, not delete

`conductor/` and `docs/Roadmap/` contain useful historical context (track specs, epic definitions, sprint retrospectives). Moving them to `docs/archive/` preserves this without polluting active navigation. Delete nothing.

### Decision 4: CLAUDE.md gets a Planning section

Add a short "Planning & Backlog" section to CLAUDE.md that:
- Names `BACKLOG.md` as the entry point for future ideas
- Names `docs/plans/` as the record of in-flight and completed features
- Explains the `/feature` skill as the workflow to move from backlog → plan → PR
- Notes that `docs/archive/` contains historical artifacts (conductor, Roadmap)

### Decision 5: todos/ stays as-is

The todos system is a code-review artifact, not a planning system. All current todos are resolved. No change needed — it's already parked.

## Scope

**In scope:**
- Move `conductor/` → `docs/archive/conductor/`
- Move `docs/Roadmap/` → `docs/archive/Roadmap/`
- Create `BACKLOG.md` at repo root with current known future ideas
- Update CLAUDE.md Planning section
- Update `docs/plans/` status fields for any plans that haven't been marked `completed` yet

**Out of scope:**
- Rewriting or restructuring existing plan documents
- Creating a new planning tool or script
- Changing the compound-engineering workflow itself
- Touching `todos/`

## Success Criteria

- A new contributor reads CLAUDE.md and knows exactly where to look for: (a) what's being built now, (b) what's planned next, (c) historical context
- No active planning content exists outside `docs/plans/` and `BACKLOG.md`
- `conductor/` and `docs/Roadmap/` are in `docs/archive/` and not referenced from CLAUDE.md

## Resolved Questions

- **Keep or delete conductor/?** → Archive to `docs/archive/conductor/`
- **Keep or delete docs/Roadmap/?** → Archive to `docs/archive/Roadmap/`
- **Where do future ideas live?** → `BACKLOG.md` at repo root
- **Is Gemini CLI / Conductor still in use?** → No. Fully replaced by Claude Code.
