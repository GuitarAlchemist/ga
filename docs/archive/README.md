# Archived Documentation

This directory contains historical artifacts that have been superseded but are preserved for reference. **Do not edit these files** — they are read-only historical records.

## Contents

### conductor/ (archived March 2026)

The `conductor/` directory contains work tracking and context files from the Gemini CLI [Conductor](https://developers.googleblog.com/conductor-introducing-context-driven-development-for-gemini-cli/) extension, which was the primary planning tool prior to March 2026.

**Why archived**: Conductor was fully replaced by Claude Code with the compound-engineering plugin. Active planning moved to `docs/plans/` (per-feature plans) and `BACKLOG.md` (future ideas list). Conductor tracks were either completed or migrated.

**Historical value**: Track specs, sprint retrospectives, epic definitions, and the tech-stack/product context from the Gemini CLI era.

Key files:
- `conductor/tracks.md` — track registry (modernization, k8s-deployment, etc.)
- `conductor/product.md` — product definition from Nov 2025
- `conductor/tech-stack.md` — tech stack snapshot from Nov 2025
- `conductor/tracks/modernization/` — agent coordination and modernization track artifacts

### Roadmap/ (archived March 2026)

The `Roadmap/` directory contains manual roadmap documents from November 2025.

**Why archived**: Last updated November 2025; entirely stale by March 2026. Replaced by `BACKLOG.md` at the repo root for future ideas and `docs/plans/` for in-flight work.

**Historical value**: `QUARTERLY_ROADMAP_2026.md` and `GUITAR_ALCHEMIST_ROADMAP_EPICS_AND_STORIES.md` contain the original epic/story definitions used to bootstrap the 2026 development cycle.

### Modular Restructuring (November 2025)

- `MODULAR_RESTRUCTURING_PROGRESS_2025-11.md.archived` — Progress report from Nov 2025
- `MODULAR_RESTRUCTURING_PLAN_2025-11.md.archived` — Original restructuring plan

**Why archived**: Plan proposed creating `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard`, etc. Architecture evolved differently with more pragmatic naming.

## Active Planning (go here instead)

- `BACKLOG.md` at repo root — future ideas, one bullet per idea
- `docs/plans/` — per-feature plans (authoritative, one file per feature)
- `docs/brainstorms/` — per-feature brainstorms
- `CLAUDE.md` Planning & Backlog section — explains the full workflow
