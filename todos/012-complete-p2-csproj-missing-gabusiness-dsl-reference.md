---
status: pending
priority: p2
issue_id: "012"
tags: [code-review, architecture, build, orchestration]
dependencies: []
---

# P2: Plan's Phase 1 .csproj snippet missing GA.Business.DSL reference — build will fail

## Problem Statement

`ResponseValidator` uses `ChordDslService` from `GA.Business.DSL` (an F# project). The Phase 1 `.csproj` snippet in the plan does not include a `<ProjectReference>` to `GA.Business.DSL`. The new project will fail to build when `ResponseValidator` is moved in Phase 3.

## Findings

- `ResponseValidator.cs` calls `ChordDslService.Parse(symbol)` (architecture review confirmed)
- Plan's Phase 1 `.csproj` snippet includes references to `GA.Business.ML`, `GA.Domain.Services`, `GA.Domain.Core` — but NOT `GA.Business.DSL`
- The actual `GA.Business.Core.Orchestration.csproj` on disk already includes `GA.Business.DSL` (ahead of the plan)
- Architectural concern: `GA.Business.DSL` is an F#/FParsec parsing library; referencing it from Layer 5 orchestration just for chord symbol validation is a heavyweight dependency

## Proposed Solutions

### Option A: Add GA.Business.DSL reference to Phase 1 .csproj snippet (Quick fix)
Add `<ProjectReference Include="..\..\Common\GA.Business.DSL\GA.Business.DSL.fsproj" />` to the plan's csproj snippet.
- **Effort**: Trivial (update plan)
- **Risk**: Low — dependency already exists on disk

### Option B: Replace ChordDslService with ChordSymbolParser from GA.Domain.Core (Recommended for architecture)
`SpectralRagOrchestrator` already uses `new ChordSymbolParser()` from `GA.Domain.Core` directly. Replace the `ChordDslService` call in `ResponseValidator` with `ChordSymbolParser`, eliminating the F# DSL dependency from the Layer 5 library.
- **Effort**: Small (1-2h to verify ChordSymbolParser covers the same chord symbols)
- **Risk**: Low — ChordSymbolParser is the domain-native parser
- **Architectural benefit**: Removes F# project dependency from the orchestration layer

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md` Phase 1 .csproj snippet
- **Files to update (Option B)**: `Apps/GaChatbot/Services/ResponseValidator.cs`

## Acceptance Criteria
- [ ] `dotnet build Common/GA.Business.Core.Orchestration/` succeeds after `ResponseValidator.cs` is moved
- [ ] `ResponseValidator` can parse chord symbols correctly (existing tests pass)

## Work Log
- 2026-03-03: Identified by architecture-strategist (P2-E)

## Resources
- Plan: Phase 1 `.csproj` snippet
- Source: `Apps/GaChatbot/Services/ResponseValidator.cs`
