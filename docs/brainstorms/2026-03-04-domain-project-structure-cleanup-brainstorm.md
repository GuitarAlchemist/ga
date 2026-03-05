# Brainstorm: Domain Classes & Project Structure Cleanup

**Date:** 2026-03-04
**Status:** Approved for planning
**Driver:** Onboarding/readability — the `Common/` layer is hard to navigate for new contributors

---

## What We're Building

A moderate cleanup pass across the Guitar Alchemist solution focused on:
1. Removing dead/excluded code (skeleton projects, excluded files, commented-out references)
2. Writing architecture documentation so the five-layer model is self-evident

This is **not** a layer restructure — existing code stays in place. The goal is to make what's already there readable and navigable.

---

## Why This Approach

**Approach A (chosen):** Dead code first, docs second.

- Dead projects and excluded files are the #1 source of confusion for new contributors
- Deleting them before writing docs ensures docs describe reality, not aspirational state
- All changes are in git — low risk, fully reversible
- Moderate scope: no cross-layer moves, no God object splits

---

## Key Decisions

### 1. What to Delete (Dead Projects / Excluded Files)
These projects have zero compiled output or are fully commented out:

| Project | Status | Action |
|---|---|---|
| `GA.Business.Core.AI` | Empty skeleton | **Delete** — no code, no intent |
| `GA.Business.Core.Generated` | Empty skeleton | **Delete** — no code, no intent |
| `GA.Business.Configuration` | All files excluded | **Keep, mark parked** — add `# PARKED` comment in solution file |
| `GA.Business.Analytics` | All service files excluded | **Keep, mark parked** — add `# PARKED` comment in solution file |
| `GA.Business.Personalization` | All files excluded | **Keep, mark parked** — add `# PARKED` comment in solution file |
| `GA.Business.Intelligence` (partial) | `IntelligentBspGenerator.cs` excluded | **Keep as-is** — leave excluded file, out of scope to move |

### 2. Commented-Out Project References
- `GA.Business.Fretboard` — referenced but commented out in `GaApi.csproj`
- `GA.Business.Harmony` — commented out in `GA.Business.Intelligence.csproj`
- `GA.Business.Analysis` — commented out in `GA.Business.Intelligence.csproj`

**Action:** Remove commented-out `<ProjectReference>` lines (they imply a half-done refactor).

### 3. GaApi Excluded Controllers (76+ files)
`GaApi` has a large number of excluded controllers/services.

**Action:** Audit. Delete excluded files that have no active counterpart. Leave `<Compile Remove="...">` only where a file is actively excluded for a known reason (document that reason inline).

### 4. GA.Domain.Core Internal Quality

The domain core project (196 files, ~16K LOC) has several specific issues worth addressing:

| Issue | Severity | Action |
|---|---|---|
| 13 unsealed public types (`Chord`, `Scale`, `ChordFormula`, etc.) | Medium | Add `sealed` keyword — no inheritance hierarchies exist |
| `Interval.cs` (784 lines) — 3 nested sealed variants in one file | Medium | Split into `DiatonicSimpleInterval.cs` + `DiatonicCompoundInterval.cs`; base stays in `Interval.cs` |
| `PitchClassSet.cs` (765 lines) — static catalog mixed with logic | Medium | Extract static catalog to `PitchClassSetCatalog.cs` |
| `Design/` folder types have no XML remarks (`DomainVocabulary`, `InvariantInfo`, etc.) | Low | Add 1-line `<remarks>` to each public type |
| `Instruments/Biomechanics/` — engineering types (IK, hand models) in domain core | Low | Add `// Note: This module models physical biomechanics, not music theory. Candidate for move to GA.Domain.Services.` comment to folder |

**Not in scope:** Moving Biomechanics to a different layer, renaming Design types.

### 5. Architecture Documentation
After cleanup, write:
- `Common/README.md` — five-layer map with project list, when to add to each layer
- `Common/GA.Domain.Core/README.md` — key domain model relationships, sealed-by-default policy, folder guide
- Per-layer short README in each layer's primary project (3–5 sentences)

---

## Scope Boundaries (YAGNI)

**In scope:**
- Delete clearly dead code (0 compiled files, fully excluded)
- Remove commented-out `<ProjectReference>` entries
- Seal 13 unsealed types in `GA.Domain.Core`
- Split `Interval.cs` and `PitchClassSet.cs` into smaller files
- Add XML remarks to `Design/` folder types
- Add Biomechanics location comment
- Write `Common/README.md`, `GA.Domain.Core/README.md`, per-layer short docs

**Out of scope (not now):**
- Moving `IntelligentBSPGenerator` to correct layer (Layer 5) — complex, no user request
- Splitting God objects (`GpuVoicingSearchStrategy`, `MusicalAnalyticsService`)
- Resolving circular dependency between `GA.Business.AI` and `GA.Business.Intelligence`
- Frontend structure cleanup

---

## Open Questions

*None — resolved in brainstorm session.*

## Resolved Questions

| Question | Resolution |
|---|---|
| What's the driver? | Onboarding/readability for new contributors |
| What's hardest to navigate? | All of Common/ equally — dead code, unclear layers, excluded files |
| How aggressive? | Moderate — remove dead weight, no cross-layer moves |
| Approach? | A — dead code first, then docs |

---

## Success Criteria

- Solution builds cleanly with fewer projects (skeletons deleted)
- Parked projects are labeled with `# PARKED` in the solution file; their excluded files remain
- `Common/README.md` and `GA.Domain.Core/README.md` exist
- 13 unsealed domain types are now sealed
- No file in GA.Domain.Core exceeds ~400 lines
- A new contributor can identify which project to add code to in < 5 minutes