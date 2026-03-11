---
status: pending
priority: p2
issue_id: "049"
tags: [code-review, architecture, layering, music-theory, duplication]
---

# ScaleNoteService in Wrong Layer — Duplicated RootPc Maps

## Problem Statement
`ScaleNoteService` lives in `GaApi/Services` (application layer) but performs pure music theory computation — determining scale notes from a root pitch class and scale type. This duplicates pitch-class knowledge already present in the domain layer, and three separate `RootPc` maps now exist across the codebase, creating a maintenance hazard for enharmonic equivalents and transposition bugs.

## Proposed Solution
- Move `ScaleNoteService` to `GA.Business.Core.Harmony` or consolidate it with existing domain `Key` types in `GA.Business.Core`
- Delete the duplicate `RootPc` maps; use the canonical domain representation
- Register the consolidated service via DI so `GaApi` and `ChordSubstitutionSkill` both consume the same implementation
- Any remaining app-layer code in `GaApi/Services` should delegate to the domain service

**Files:**
- `Apps/ga-server/GaApi/Services/ScaleNoteService.cs` (to be moved/removed)
- `Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs` (consumes duplicate map — update to use domain service)

## Acceptance Criteria
- [ ] Exactly one `RootPc` / pitch-class-to-note mapping exists in the codebase
- [ ] `ScaleNoteService` logic lives in `GA.Business.Core.Harmony` or equivalent domain layer
- [ ] `GaApi` and `ChordSubstitutionSkill` both use the canonical domain service via DI
- [ ] No duplicate pitch-class maps remain in app or ML layers
- [ ] All existing scale-related tests pass after consolidation
