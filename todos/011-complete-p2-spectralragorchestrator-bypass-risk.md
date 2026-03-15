---
status: pending
priority: p2
issue_id: "011"
tags: [code-review, architecture, chatbot, di]
dependencies: []
---

# P2: SpectralRagOrchestrator directly injectable — bypasses ProductionOrchestrator routing

## Problem Statement

`SpectralRagOrchestrator` implements `IHarmonicChatOrchestrator` and is registered in DI. Nothing prevents a future developer from injecting it directly into a controller, bypassing `ProductionOrchestrator`'s `SemanticRouter`, filter hoisting, tab detection, and routing metadata population. The plan does not propose any mitigation.

## Findings

- `SpectralRagOrchestrator` is registered as `services.AddSingleton<SpectralRagOrchestrator>()` in the proposed `AddChatbotOrchestration()`
- Also registered as `services.AddSingleton<IHarmonicChatOrchestrator, SpectralRagOrchestrator>()` — this registration makes it injectable via the shared interface
- `ProductionOrchestrator` is the intended public entry point; `SpectralRagOrchestrator` is an internal implementation detail

## Proposed Solutions

### Option A: Mark SpectralRagOrchestrator as internal in the shared library (Recommended)
```csharp
internal sealed class SpectralRagOrchestrator : IHarmonicChatOrchestrator { ... }
```
Register as concrete type internally; do not expose as `IHarmonicChatOrchestrator` in `AddChatbotOrchestration()`. External consumers can only resolve `IHarmonicChatOrchestrator` → `ProductionOrchestrator`.
- **Pros**: Strongest boundary; enforced by compiler
- **Cons**: Tests may need `InternalsVisibleTo`
- **Effort**: Small

### Option B: Register only ProductionOrchestrator as IHarmonicChatOrchestrator
Remove `services.AddSingleton<IHarmonicChatOrchestrator, SpectralRagOrchestrator>()` from `AddChatbotOrchestration()`. Keep `SpectralRagOrchestrator` as a concrete singleton (for `TabAwareOrchestrator` to inject) but do not expose it via the public interface.
- **Pros**: Simple; no `internal` keyword needed
- **Effort**: Trivial
- **Risk**: Low

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `Common/GA.Business.Core.Orchestration/Extensions/OrchestrationServiceExtensions.cs`
- **Phase in plan**: Phase 3

## Acceptance Criteria
- [ ] Only `ProductionOrchestrator` is registered as `IHarmonicChatOrchestrator`
- [ ] `SpectralRagOrchestrator` is either `internal` or not exposed via the public interface
- [ ] Test: resolving `IHarmonicChatOrchestrator` from DI returns a `ProductionOrchestrator` instance

## Work Log
- 2026-03-03: Identified by architecture-strategist (P2-C)

## Resources
- Plan: Phase 3 `OrchestrationServiceExtensions`
