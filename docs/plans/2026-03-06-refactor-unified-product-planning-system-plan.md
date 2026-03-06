---
title: "refactor: Unify product planning to docs/plans + BACKLOG.md"
type: refactor
status: completed
date: 2026-03-06
origin: docs/brainstorms/2026-03-06-unified-product-planning-system-brainstorm.md
---

# refactor: Unify Product Planning to docs/plans + BACKLOG.md

## Overview

Four overlapping planning systems coexist with status drift and no single source of truth (see brainstorm: `docs/brainstorms/2026-03-06-unified-product-planning-system-brainstorm.md`). This refactor collapses them into one:

- **`docs/plans/`** — authoritative per-feature record (brainstorm → plan → PR)
- **`BACKLOG.md`** at repo root — flat list of future ideas not yet in active planning
- **`docs/archive/`** — historical artifacts; read-only reference

All conductor/ and docs/Roadmap/ content is preserved via archiving, not deleted.

## Problem Statement

| System | Tool | State |
|---|---|---|
| `conductor/` | Gemini CLI (retired) | Orphaned; 3 missing tracks from registry; stale .NET 9 refs |
| `docs/plans/` | Claude Code `/feature` skill | Active and authoritative in practice |
| `docs/Roadmap/` | Manual | Last updated Nov 2025 — entirely stale |
| `todos/` | Code review audit | All 20 items resolved — static artifact |

`CLAUDE.md` has no planning section at all, so a new contributor has no guidance on where to find what's in flight or how to propose new work.

## Proposed Solution

See brainstorm: key decision "One system wins — docs/plans/" (see brainstorm § Key Decisions).

### After This Refactor

```
BACKLOG.md                     ← future ideas (root, easy to find)
docs/plans/                    ← per-feature plans (active + historical)
docs/brainstorms/              ← per-feature brainstorms (stays as-is)
docs/archive/
  conductor/                   ← moved from conductor/
  Roadmap/                     ← moved from docs/Roadmap/
  README.md                    ← explains what's archived and why
CLAUDE.md                      ← gains Planning & Backlog section
GEMINI.md                      ← conductor links removed / redirected
compound-engineering.local.md  ← conductor links removed / redirected
todos/                         ← unchanged (static audit artifact)
```

## Implementation Phases

### Phase 1: Prepare archive destination

- [x] Create `docs/archive/README.md` explaining the directory's purpose and contents
- [x] Create `docs/archive/` directory (the README creation accomplishes this)

### Phase 2: Update live links before moving files

**Critical — SpecFlow analysis found that `GEMINI.md` and `compound-engineering.local.md` both contain active links into `conductor/`. Moving conductor/ before updating these files creates broken links.**

- [x] Update `GEMINI.md`: remove or redirect all 6 references to `conductor/` paths; update framing from "Conductor = context layer" to reflect the new system
- [x] Update `compound-engineering.local.md`: file does not exist — skipped (no broken links to fix)
- [x] Update `docs/architecture/AGENT_MARKETPLACE_MVP_SPEC.md`: file does not exist on main — skipped
- [x] Update `docs/Integration/DOCKER_MCP_CANONICAL_PROFILE.md`: file does not exist on main — skipped

### Phase 3: Move directories to archive

- [x] Move `conductor/` → `docs/archive/conductor/` (preserves full git history via `git mv`)
- [x] Move `docs/Roadmap/` → `docs/archive/Roadmap/` (preserves full git history)

### Phase 4: Create BACKLOG.md

Create `BACKLOG.md` at repo root. Populate only with items that have been referenced in 2026 conductor tracks, `docs/plans/`, or `.agent/api-team/BACKLOG.md` — do not carry forward aspirational Nov 2025 Roadmap items without a 2026 reference.

Sections:
- **Active tracks** (work started, not yet a plan): spectral-rag-chatbot spikes, modernization items
- **API quality** (from `.agent/api-team/BACKLOG.md`): ~8 P1 items
- **Agent infrastructure** (from Epic 5 / AGENT_MARKETPLACE_MVP_SPEC): marketplace, semantic-event-routing
- **Future / not started**: k8s-deployment, voice/vision features (flagged as needing re-evaluation)

- [x] Write `BACKLOG.md` at repo root

### Phase 5: Update CLAUDE.md

Add a **Planning & Backlog** section between "Agent Skills" and "Testing Conventions" covering:

1. `BACKLOG.md` — entry point for future ideas; one bullet per idea; remove it when a plan is created
2. `docs/plans/` — authoritative record; one file per feature; `status: active | completed`; filename convention `YYYY-MM-DD-<type>-<name>-plan.md`
3. `/feature` skill — the workflow to move an idea from backlog → brainstorm → plan → PR
4. `docs/archive/` — historical artifacts (conductor, Roadmap); read-only reference

- [x] Add Planning & Backlog section to `CLAUDE.md`

### Phase 6: Verify

- [x] Run `grep -r "conductor/" . --include="*.md" | grep -v "docs/archive"` — remaining results are in plan/brainstorm docs (historical context) and BACKLOG.md (now points to docs/archive/)
- [x] Run `grep -r "docs/Roadmap" . --include="*.md" | grep -v "docs/archive"` — remaining results are in plan/brainstorm historical context only
- [x] Confirm `BACKLOG.md` exists at root and has content
- [x] Confirm `docs/archive/README.md` exists
- [x] Confirm `CLAUDE.md` has Planning section
- [ ] `dotnet build AllProjects.slnx -c Debug` — 0 errors (no code changes, but verify no accidental breakage)

## Acceptance Criteria

- [ ] A new contributor reads `CLAUDE.md` and knows: (a) where to see what's in flight, (b) where to add a new idea, (c) where historical context lives
- [ ] `conductor/` no longer exists at repo root — moved to `docs/archive/conductor/`
- [ ] `docs/Roadmap/` no longer exists — moved to `docs/archive/Roadmap/`
- [ ] `BACKLOG.md` exists at repo root with grouped future ideas
- [ ] `docs/archive/README.md` explains what the archive contains and why
- [ ] `GEMINI.md` and `compound-engineering.local.md` contain no broken links to conductor/
- [ ] `CLAUDE.md` has a Planning & Backlog section that names `docs/plans/` and `BACKLOG.md` as authoritative

## Scope Boundaries

**In scope:**
- Moving `conductor/` and `docs/Roadmap/` to `docs/archive/`
- Creating `BACKLOG.md` and `docs/archive/README.md`
- Updating `CLAUDE.md`, `GEMINI.md`, `compound-engineering.local.md`, and 2 in-text doc citations

**Out of scope:**
- Rewriting or restructuring existing `docs/plans/` documents
- Changing the compound-engineering workflow itself
- Migrating conductor track content into plan format
- Changing `todos/` in any way (static resolved artifact)
- Addressing the Nov 2025 Roadmap Epics aspirational content beyond archiving it

## Risk Analysis

| Risk | Severity | Mitigation |
|---|---|---|
| Broken links in GEMINI.md / compound-engineering.local.md | High | Phase 2 updates links before Phase 3 moves files |
| git history loss for conductor/ content | Low | Use `git mv` — git traces renames correctly |
| BACKLOG.md becomes stale like Roadmap | Low | CLAUDE.md instructs: remove item when plan is created |
| Missing a conductor/ reference somewhere | Low | Phase 6 grep catches any remaining references |

## Sources & References

### Origin

- **Brainstorm:** `docs/brainstorms/2026-03-06-unified-product-planning-system-brainstorm.md`
  - Key decisions carried forward: one system wins (docs/plans/), archive not delete, BACKLOG.md at root

### Internal References

- `CLAUDE.md` — planning section to add (currently has none)
- `GEMINI.md` — 6 conductor/ links to update
- `compound-engineering.local.md` — 2 conductor/ links to update
- `conductor/tracks.md` — source of track status for BACKLOG.md
- `.agent/api-team/BACKLOG.md` — source of API quality items for BACKLOG.md
- `docs/architecture/AGENT_MARKETPLACE_MVP_SPEC.md` — 1 conductor citation to update
- `docs/Integration/DOCKER_MCP_CANONICAL_PROFILE.md` — 1 conductor citation to update
- `docs/Roadmap/CURRENT_TRUTH_STATUS_2026-02-27.md` — most recent status snapshot (now going to archive)
