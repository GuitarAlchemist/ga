---
status: complete
priority: p1
issue_id: "033"
tags: [architecture, abstraction, layer-violation, code-review]
dependencies: []
---

# 033 — `AgUiChatController` Injects Concrete `ProductionOrchestrator` Instead of Interface

## Problem Statement

`AgUiChatController` takes `ProductionOrchestrator` as a constructor parameter (concrete class) rather than the registered `IHarmonicChatOrchestrator` abstraction. `IHarmonicChatOrchestrator` exists precisely to decouple callers from orchestrator internals. This forces the controller to import `GA.Business.Core.Orchestration.Services`, prevents testability (no mock/stub substitution), and means future orchestrator refactors will break the controller's compile-time contract.

The `ChatbotOrchestrationExtensions` already registers `ProductionOrchestrator` as `IHarmonicChatOrchestrator` — the fix is a one-line change.

## Findings

`Apps/ga-server/GaApi/Controllers/AgUiChatController.cs` lines 18–22:
```csharp
public AgUiChatController(
    ProductionOrchestrator orchestrator,   // ← concrete type
    ILogger<AgUiChatController> logger)
```

Correct pattern used by other controllers:
```csharp
public SomeController(IHarmonicChatOrchestrator orchestrator, ...)
```

## Proposed Solutions

### Option A — Change injection to interface (Recommended)
```csharp
public AgUiChatController(
    IHarmonicChatOrchestrator orchestrator,
    ILogger<AgUiChatController> logger)
```
- **Effort:** Trivial (1 line change + remove `using` directive).
- **Risk:** Zero — DI already registers concrete as the interface.

### Option B — Accept as-is for now
Works at runtime; only a testability and coupling concern.
- **Risk:** Low short-term; grows as orchestrator evolves.

## Recommended Action
Option A — trivial fix, follows established pattern.

## Acceptance Criteria

- [ ] `AgUiChatController` constructor parameter is `IHarmonicChatOrchestrator`, not `ProductionOrchestrator`
- [ ] No direct `using` import of `GA.Business.Core.Orchestration.Services` in the controller file
- [ ] Existing tests pass unchanged

## Work Log

- 2026-03-10: Identified during architecture review agent for PR #8
