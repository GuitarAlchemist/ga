---
status: pending
priority: p1
issue_id: "003"
tags: [code-review, architecture, di, chatbot, orchestration]
dependencies: []
---

# P1: IVectorIndex not registered in GaApi DI — Phase 4 will crash at startup

## Problem Statement

The plan's `OrchestrationServiceExtensions.AddChatbotOrchestration()` registers `SpectralRagOrchestrator` as a Singleton, but `SpectralRagOrchestrator` depends on `IVectorIndex`. GaApi currently has no `IVectorIndex` registration — it uses a different vector search path (`VectorSearchService`, `ILGPUVectorSearchStrategy`). Phase 4 will produce a runtime DI resolution exception on startup, not a compile error.

## Findings

- `SpectralRagOrchestrator` constructor injects `IVectorIndex` (confirmed in source)
- `GaApi/Extensions/VectorSearchServiceExtensions.cs` registers `IVectorSearchStrategy` but NOT `IVectorIndex`
- `GaApi/Extensions/VoicingSearchServiceExtensions.cs` does not register `IVectorIndex`
- `InMemoryVectorIndex` lives in `Apps/GaChatbot/Services/` — not in the shared library
- `FileBasedVectorIndex` and `QdrantVectorIndex` exist in `Common/GA.Business.ML/Embeddings/`
- Plan's `AddChatbotOrchestration()` snippet does not include any `IVectorIndex` registration

## Proposed Solutions

### Option A: Register FileBasedVectorIndex in AddChatbotOrchestration (Recommended)
Add `services.AddSingleton<IVectorIndex, FileBasedVectorIndex>()` to `AddChatbotOrchestration()` with a configuration path for the index file:
```csharp
services.AddSingleton<IVectorIndex>(sp => {
    var path = configuration["VectorIndex:Path"] ?? "data/voicing-index.bin";
    return new FileBasedVectorIndex(path);
});
```
- **Pros**: Shared implementation works for both GaApi and GaChatbot
- **Cons**: Requires index file to exist at configured path; cold-start latency for loading
- **Effort**: Small
- **Risk**: Low

### Option B: Document as required pre-condition
Add an XML comment to `AddChatbotOrchestration()` documenting that the caller must register `IVectorIndex` before calling this method.
- **Pros**: Maximum flexibility for calling hosts
- **Cons**: Will still crash at startup if caller forgets; no compile-time enforcement
- **Effort**: Trivial
- **Risk**: Medium — easy to miss

### Option C: Register InMemoryVectorIndex with empty index as fallback
Register an empty `InMemoryVectorIndex` as a no-op fallback. SpectralRagOrchestrator will return empty results rather than crashing.
- **Pros**: No startup crash
- **Cons**: Silent degraded mode; hallucination risk from zero results
- **Effort**: Small
- **Risk**: Medium — misleading to operators

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `Common/GA.Business.Core.Orchestration/Extensions/OrchestrationServiceExtensions.cs`
- **Phase in plan**: Phase 3 (when writing OrchestrationServiceExtensions) or Phase 4 (when wiring GaApi)

## Acceptance Criteria
- [ ] `dotnet run` on GaApi after Phase 4 does not throw `InvalidOperationException: Unable to resolve service for type 'IVectorIndex'`
- [ ] Integration test `ChatStreamSseTests` can resolve all services
- [ ] Index implementation choice is documented in `AddChatbotOrchestration()` XML comment

## Work Log
- 2026-03-03: Identified by architecture-strategist review agent

## Resources
- Plan: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md` Phase 3/4
- Source: `Common/GA.Business.ML/Embeddings/IVectorIndex.cs`, `Common/GA.Business.ML/Embeddings/FileBasedVectorIndex.cs`
