---
status: pending
priority: p2
issue_id: "016"
tags: [performance, async, code-review]
dependencies: []
---

# 016 — ProductionOrchestrator: Sequential Independent LLM Calls + Unhandled Embedding Failures

## Problem Statement

`ProductionOrchestrator.AnswerAsync` calls `ExtractFiltersAsync` and `RouteAsync` sequentially (lines 30–31), but both consume only `req.Message` with no data dependency on each other. Each is an LLM call costing 50–500 ms. This introduces unnecessary sequential latency on every chatbot request.

Additionally, `Task.WhenAll(embeddingTasks)` at line 144 propagates any embedding failure as an unhandled exception, taking down the entire request instead of degrading gracefully.

## Findings

- Lines 30–31: two sequential `await` calls with the same input and no mutual dependency.
- Line 144: `Task.WhenAll(embeddingTasks)` has no try/catch or fallback; a single embedding provider failure crashes the orchestration pipeline.

## Proposed Solutions

### Option A — Parallelize with Task.WhenAll (primary fix)
Replace the two sequential awaits with a single parallel call:
```csharp
var filtersTask = queryUnderstandingService.ExtractFiltersAsync(req.Message, ct);
var routingTask = router.RouteAsync(req.Message, ct);
await Task.WhenAll(filtersTask, routingTask);
var filters = filtersTask.Result;
var routing = routingTask.Result;
```
**Pros:** Eliminates 50–500 ms of dead wait on every request; no semantic change.
**Cons:** Both LLM calls consume a thread/connection simultaneously — acceptable at expected load.
**Effort:** Low — mechanical refactor.
**Risk:** Low.

### Option B — Cache routing decisions per message hash
For high-repeat queries, cache the routing result by `SHA256(req.Message)` with a short TTL. Combine with Option A.
**Pros:** Eliminates both LLM calls on cache hit.
**Cons:** Adds cache invalidation complexity; routing decisions may be context-sensitive.
**Effort:** Medium.
**Risk:** Medium (stale routing on model/config changes).

### Option C — Graceful embedding fallback (secondary fix, independent)
Wrap line 144:
```csharp
try { await Task.WhenAll(embeddingTasks); }
catch (Exception ex)
{
    logger.LogWarning(ex, "One or more embedding tasks failed; continuing without embeddings.");
}
```
**Pros:** Prevents a single embedding failure from killing the full request.
**Cons:** Silent partial failure; caller gets a degraded but non-empty answer.
**Effort:** Low.
**Risk:** Low.

## Recommended Action

## Technical Details

- **Affected files:**
  - `Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs` (lines 30–31, 144)

## Acceptance Criteria

- [ ] `ExtractFiltersAsync` and `RouteAsync` execute concurrently, not sequentially.
- [ ] An embedding provider failure at line 144 does not propagate as an unhandled exception; a warning is logged and the request continues.
- [ ] All existing orchestration tests remain green.
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings.

## Work Log

- 2026-03-07 — Identified in /ce:review of PR #2

## Resources

- PR: https://github.com/GuitarAlchemist/ga/pull/2
