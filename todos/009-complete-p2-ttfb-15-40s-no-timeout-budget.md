---
status: pending
priority: p2
issue_id: "009"
tags: [code-review, performance, chatbot, latency, ollama]
dependencies: ["005"]
---

# P2: 15-40s TTFB with no pipeline timeout budget or concurrency gate

## Problem Statement

The plan does not quantify or constrain total pipeline latency. Sequential Ollama calls (QueryUnderstanding ~10s + optional LLM routing ~3s + narrator ~8-16s with 512 tokens) can exceed 30 seconds before any byte reaches the frontend. SSE connections emitting no data for 15+ seconds are closed by many proxies and load balancers. There is also no concurrency gate — concurrent users queue inside Ollama serially.

## Findings

- P50 estimated latency: 12-21s; P99: 20-32s (performance-oracle analysis)
- Ollama processes one generation at a time on a single GPU — user 5 waits 60+ seconds
- `OllamaGroundedNarrator._httpClient.Timeout = 30s` — only per-call timeout, not pipeline budget
- The plan increases `num_predict` from 256 → 512 (doubling narrator latency) without acknowledging the impact
- SSE idle timeout default in most proxies/CDNs: 30-60s; plan has no keep-alive or first-byte emission strategy

## Proposed Solutions

### Option A: Add pipeline budget CancellationTokenSource + emit routing event immediately (Recommended)
```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
cts.CancelAfter(TimeSpan.FromSeconds(25)); // pipeline budget

// Emit routing event BEFORE awaiting narrator (requires splitting AnswerAsync):
await Response.WriteAsync($"data: {routingJson}\n\n", ct);
await Response.FlushAsync(ct);

var answer = await _orchestrator.NarrateAsync(..., cts.Token);
```
- **Pros**: Client sees routing event immediately (~2s); proxy timeout reset on first emission
- **Effort**: Medium (requires interface change to split routing from narration)
- **Risk**: Medium

### Option B: Add 20s pipeline CancellationTokenSource, accept long TTFB for this milestone
Add CTS without splitting routing from narration. Document expected latency in PR.
- **Effort**: Small (1h — add CTS in controller)
- **Risk**: Low for this milestone

### Option C: Add concurrency semaphore to ProductionOrchestrator
```csharp
private static readonly SemaphoreSlim _concurrencyGate = new(3, 3); // max 3 concurrent Ollama pipelines
// In AnswerAsync: await _concurrencyGate.WaitAsync(ct); try { ... } finally { _concurrencyGate.Release(); }
```
Return 503 with `Retry-After: 5` header when gate is full.
- **Effort**: Small
- **Risk**: Low

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` (add CTS, add concurrency gate or 503)
- **New acceptance criterion**: Add to plan: "GET /health returns 503 when >3 concurrent chat requests"

## Acceptance Criteria
- [ ] Single-user happy path latency documented in PR (measured, not estimated)
- [ ] Pipeline CancellationTokenSource with 25s budget applied in controller
- [ ] SSE connection emits at least one event (routing) within 5s even for slow Ollama
- [ ] Concurrent 5-user test: users 4 and 5 receive 503 with Retry-After rather than waiting 60s

## Work Log
- 2026-03-03: Identified by performance-oracle (P1-A, P2-D)

## Resources
- Plan: Phase 4 Task 3, Phase 2.5 Task 7 (num_predict increase)
- Institutional guide: `docs/API/STREAMING_IMPLEMENTATION_GUIDE.md`
