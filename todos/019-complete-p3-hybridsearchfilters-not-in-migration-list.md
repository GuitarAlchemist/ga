---
status: pending
priority: p3
issue_id: "019"
tags: [code-review, architecture, migration, chatbot]
dependencies: []
---

# P3: HybridSearchFilters not explicitly listed in Phase 2/3 migration

## Problem Statement

`HybridSearchFilters` is defined inside `QueryUnderstandingService.cs`. The plan moves `QueryUnderstandingService` to the shared library but does not explicitly mention `HybridSearchFilters`. This class straddles the boundary (produced by `QueryUnderstandingService`, consumed by `SpectralRagOrchestrator`) and must also move. Without an explicit mention, it may be left behind or duplicated.

Additionally, the agent-native reviewer recommends promoting `HybridSearchFilters` from the `object? DebugParams` field to a typed `QueryFilters?` field on `ChatResponse`, which would make it accessible to programmatic consumers.

## Proposed Solutions

### Option A: Move HybridSearchFilters to ChatModels.cs in Phase 2, promote to typed field
In Phase 2 `ChatModels.cs`, include `HybridSearchFilters` (or rename to `QueryUnderstandingFilters`). Add `QueryFilters? QueryFilters = null` to `ChatResponse`.
- **Effort**: Small (30 min — move class, add field, update usages)
- **Risk**: Low

### Option B: Keep in QueryUnderstandingService.cs file, just document the implicit move
The class moves with its file. Add a note to the Phase 3 migration checklist.
- **Effort**: Trivial
- **Risk**: Low — just needs documentation

## Acceptance Criteria
- [ ] `HybridSearchFilters` (or equivalent) is in `GA.Business.Core.Orchestration.Models` after Phase 3
- [ ] No duplicate definition remains in `GaChatbot`

## Work Log
- 2026-03-03: Identified by architecture-strategist (P3-D) and agent-native-reviewer (P2-2)
