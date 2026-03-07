---
status: complete
priority: p2
issue_id: "003"
tags: [code-review, chatbot, performance, cancellation]
dependencies: []
---

# CancellationToken not passed to ExtractFiltersAsync in ProductionOrchestrator

## Problem Statement

`ProductionOrchestrator.AnswerAsync` receives a `CancellationToken ct` (which is the 25s pipeline budget from `ChatbotController`), but passes it to `router.RouteAsync` while `queryUnderstandingService.ExtractFiltersAsync` is called without a token. If the pipeline budget fires or the client disconnects during filter extraction, the LLM call inside `ExtractFiltersAsync` will not be cancelled, wasting Ollama capacity.

## Findings

```csharp
// ProductionOrchestrator.cs:30 — token missing
var filters = await queryUnderstandingService.ExtractFiltersAsync(req.Message);

// ProductionOrchestrator.cs:31 — token correctly passed
var routing = await router.RouteAsync(req.Message, ct);
```

The `QueryUnderstandingService.ExtractFiltersAsync` signature likely accepts a `CancellationToken` — verify and add.

Additionally: `ExtractFiltersAsync` is called unconditionally before routing. If routing selects the `Tab` agent path, filters are computed but not used by the tab handlers (only `routing.SelectedAgent.AgentId` is checked). This is a minor efficiency issue secondary to the cancellation concern.

## Proposed Solutions

### Option A: Pass ct to ExtractFiltersAsync (Recommended)

```csharp
public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
{
    var filters = await queryUnderstandingService.ExtractFiltersAsync(req.Message, ct);
    var routing = await router.RouteAsync(req.Message, ct);
    // ...
}
```

**Pros:** Consistent cancellation propagation; honours 25s budget across all LLM calls
**Cons:** None — requires checking QueryUnderstandingService signature
**Effort:** Small
**Risk:** Low

### Option B: Move filter extraction after routing check

Call `ExtractFiltersAsync` only when the non-tab path is taken (since tab handlers don't consume `filters`):

```csharp
var routing = await router.RouteAsync(req.Message, ct);
QueryFilters? filters = null;

if (routing.SelectedAgent.AgentId != AgentIds.Tab)
    filters = await queryUnderstandingService.ExtractFiltersAsync(req.Message, ct);
```

**Pros:** Skips a full LLM round-trip for tab queries
**Cons:** Tab path loses filter context (probably not needed there anyway)
**Effort:** Small
**Risk:** Low

## Technical Details

- **Affected files:** `Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs:30`
- **Component:** Orchestration pipeline

## Acceptance Criteria

- [ ] `ExtractFiltersAsync` receives the cancellation token
- [ ] Cancelling a request (client disconnect or 25s budget) stops filter extraction
- [ ] No functional change to the happy path

## Work Log

- 2026-03-06: Found during code review of PR #2
