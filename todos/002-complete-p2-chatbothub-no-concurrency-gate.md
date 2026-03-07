---
status: complete
priority: p2
issue_id: "002"
tags: [code-review, chatbot, performance, reliability]
dependencies: []
---

# ChatbotHub has no concurrency gate or pipeline budget

## Problem Statement

`ChatbotController` protects Ollama with `SemaphoreSlim(3, 3)` and a 25-second pipeline budget (`CancellationTokenSource.CreateLinkedTokenSource`). `ChatbotHub` (SignalR) has neither. Multiple concurrent SignalR connections each calling `SendMessage` will hit Ollama in parallel without limit, causing queue saturation — the exact problem the gate was added to prevent.

## Findings

- `ChatbotHub.SendMessage` (line 22): calls `orchestrator.AnswerAsync` with only `Context.ConnectionAborted` as cancellation — no timeout budget
- No `SemaphoreSlim` or equivalent in `ChatbotHub`
- `ChatbotController` has `_concurrencyGate` and `_pipelineBudget`; the same logic is absent from the hub
- Under load, every SignalR connection that calls `SendMessage` concurrently will queue a full Ollama pipeline call

## Proposed Solutions

### Option A: Add SemaphoreSlim gate to ChatbotHub (Recommended)

```csharp
// In ChatbotHub class body
private static readonly SemaphoreSlim _concurrencyGate = new(3, 3);
private static readonly TimeSpan _pipelineBudget = TimeSpan.FromSeconds(25);

public async Task SendMessage(string message, bool useSemanticSearch = true)
{
    // ... validation ...

    if (!await _concurrencyGate.WaitAsync(TimeSpan.Zero, cancellationToken))
    {
        await Clients.Caller.SendAsync("Error", "Service is busy. Please try again in a few seconds.");
        return;
    }

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(_pipelineBudget);
    try
    {
        var response = await orchestrator.AnswerAsync(..., cts.Token);
        // ...
    }
    finally
    {
        _concurrencyGate.Release();
    }
}
```

**Pros:** Consistent with controller; prevents Ollama saturation via hub
**Cons:** Static gate is per-process — in multi-instance deployments, still no global limit
**Effort:** Small
**Risk:** Low

### Option B: Extract gate to a shared service

Move the `SemaphoreSlim` into a singleton `OllamaConcurrencyGate` service registered in DI, shared across both the controller and hub.

**Pros:** Single limit for both code paths; accurate accounting
**Cons:** More code; requires DI change
**Effort:** Medium
**Risk:** Low

## Technical Details

- **Affected files:** `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs`
- **Component:** SignalR Hub, Ollama pipeline

## Acceptance Criteria

- [ ] Hub `SendMessage` returns a user-friendly error when concurrency limit is reached
- [ ] Hub `SendMessage` times out after 25s if pipeline doesn't complete
- [ ] Controller and Hub share a consistent Ollama load limit

## Work Log

- 2026-03-06: Found during code review of PR #2
