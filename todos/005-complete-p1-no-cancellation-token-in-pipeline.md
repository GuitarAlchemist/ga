---
status: pending
priority: p1
issue_id: "005"
tags: [code-review, performance, architecture, chatbot, sse]
dependencies: []
---

# P1: No CancellationToken through orchestration pipeline — client disconnect leaks Ollama connections

## Problem Statement

The plan's Phase 4 code calls `ProductionOrchestrator.AnswerAsync(request)` without a `CancellationToken`. ASP.NET Core binds a cancellation token to each HTTP request's lifecycle — when a client disconnects mid-stream, this token is signalled. Without propagation, the server continues holding Ollama connections and computing the full response for up to 30 seconds (the timeout) even after the client is gone. Under moderate load, this creates a resource leak that degrades performance for all users.

## Findings

- Plan Phase 4 Task 3 shows: `var chatResponse = await ProductionOrchestrator.AnswerAsync(request)` — no CT
- `IHarmonicChatOrchestrator.AnswerAsync` signature does not include `CancellationToken`
- `OllamaGroundedNarrator._httpClient.Timeout = TimeSpan.FromSeconds(30)` — only per-call timeout, not client-disconnect-aware
- The plan's failure table says "SSE client disconnects mid-stream — Ignored (streaming continues) — Same — no change needed" — this is incorrect; resource leak is real
- Each request holds: 1 Ollama connection for `QueryUnderstandingService`, 1 for `OllamaGroundedNarrator`, potentially 1 for LLM routing = 3 Ollama connections per abandoned request

## Proposed Solutions

### Option A: Add CancellationToken to IHarmonicChatOrchestrator.AnswerAsync and propagate (Recommended)
```csharp
// Interface change:
Task<ChatResponse> AnswerAsync(ChatRequest request, CancellationToken cancellationToken = default);

// Controller usage:
var chatResponse = await _orchestrator.AnswerAsync(request, HttpContext.RequestAborted);

// Propagate to all Ollama calls:
await _httpClient.PostAsync(url, content, cancellationToken);
```
- **Pros**: Eliminates connection leak; correct ASP.NET Core pattern
- **Cons**: Interface change touches all implementations
- **Effort**: Small (2-3h to thread CT through all service methods)
- **Risk**: Low — additive change; `default` value preserves backward compat

### Option B: Apply per-pipeline budget via CancellationTokenSource
Add a composite token: `using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted); cts.CancelAfter(TimeSpan.FromSeconds(25));`. Enforces budget even if client doesn't disconnect.
- **Pros**: Also adds overall pipeline timeout
- **Effort**: Small
- **Risk**: Low

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**:
  - `Apps/GaChatbot/Abstractions/IHarmonicChatOrchestrator.cs` (add CT to interface)
  - `Apps/GaChatbot/Services/ProductionOrchestrator.cs`
  - `Apps/GaChatbot/Services/SpectralRagOrchestrator.cs`
  - `Apps/GaChatbot/Services/OllamaGroundedNarrator.cs`
  - `Apps/GaChatbot/Services/QueryUnderstandingService.cs`
  - `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` (pass `HttpContext.RequestAborted`)

## Acceptance Criteria
- [ ] `IHarmonicChatOrchestrator.AnswerAsync` accepts `CancellationToken`
- [ ] `ChatbotController` passes `HttpContext.RequestAborted` to `AnswerAsync`
- [ ] Ollama HTTP calls in narrator and query understanding accept and observe the token
- [ ] Client disconnect within 2s of request cancels all downstream Ollama calls within 500ms (verify via log)

## Work Log
- 2026-03-03: Identified by performance-oracle (P3-C), architecture-strategist (P1-C)

## Resources
- Plan: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md` Phase 4 Task 3
- Institutional guide: `docs/API/STREAMING_IMPLEMENTATION_GUIDE.md`
