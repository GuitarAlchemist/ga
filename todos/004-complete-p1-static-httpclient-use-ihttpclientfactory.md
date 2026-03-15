---
status: pending
priority: p1
issue_id: "004"
tags: [code-review, architecture, performance, security, di, chatbot]
dependencies: []
---

# P1: Static HttpClient in singleton services — use IHttpClientFactory

## Problem Statement

Both `QueryUnderstandingService` and `OllamaGroundedNarrator` declare `private static readonly HttpClient _httpClient = new()`. This bypasses `IHttpClientFactory`'s connection pool management. Under concurrent load, connections to Ollama will not be reused efficiently, DNS TTL is never refreshed, and `TIME_WAIT` socket accumulation can cause exhaustion. Additionally, the `OllamaUrl` is hardcoded (addressed separately in the plan), but even the configurable fix requires `IHttpClientFactory` to work correctly in a Singleton.

This is flagged by security-sentinel (SSRF/connection risk), performance-oracle (P1-B socket exhaustion), and architecture-strategist (P1-B) — highest agreement of all findings.

## Findings

- **`QueryUnderstandingService.cs` line 11**: `private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };`
- **`OllamaGroundedNarrator.cs` line 22**: `private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };`
- `docs/Configuration/SERVICE_REGISTRATION_GUIDELINES.md` explicitly documents the `AddHttpClient` pattern with retry and circuit breaker (institutional knowledge confirmed)
- Both services are registered as Singleton — `static` fields are shared process-global state
- DNS changes after deployment are not reflected; connections cannot be recycled by the pool

## Proposed Solutions

### Option A: Register named HttpClient in AddChatbotOrchestration + inject IHttpClientFactory (Recommended)
```csharp
// In OrchestrationServiceExtensions.AddChatbotOrchestration():
services.AddHttpClient("ollama", client => {
    client.BaseAddress = new Uri(configuration["Ollama:Endpoint"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(30);
});
// In each service constructor:
// IHttpClientFactory httpClientFactory → _httpClientFactory = httpClientFactory
// Per-call: var client = _httpClientFactory.CreateClient("ollama");
```
- **Pros**: Correct pooling, DNS TTL respected, configurable base address, retry-friendly
- **Cons**: Each service call creates a new `HttpClient` wrapper (lightweight — the socket pool is shared)
- **Effort**: Small (1-2h total for both services)
- **Risk**: Low

### Option B: Replace static field with instance field initialized from DI
Inject the `HttpClient` directly (single named instance) from DI rather than factory. Simpler but slightly less flexible for Polly retry policies.
- **Effort**: Small
- **Risk**: Low

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**:
  - `Apps/GaChatbot/Services/QueryUnderstandingService.cs` (remove static field, add IHttpClientFactory ctor param)
  - `Apps/GaChatbot/Services/OllamaGroundedNarrator.cs` (same)
  - `Common/GA.Business.Core.Orchestration/Extensions/OrchestrationServiceExtensions.cs` (add AddHttpClient call)
- **Must happen before Phase 3** (before moving these services to shared library)

## Acceptance Criteria
- [ ] No `static readonly HttpClient` in any orchestration service
- [ ] Named `"ollama"` HttpClient registered in `AddChatbotOrchestration()`
- [ ] Base address reads from `configuration["Ollama:Endpoint"]`
- [ ] Both services inject `IHttpClientFactory` in constructor
- [ ] Load test: 10 concurrent requests complete without `SocketException`

## Work Log
- 2026-03-03: Flagged by security-sentinel, performance-oracle, architecture-strategist (highest agreement)

## Resources
- Plan: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md` Phase 2.5 Task 2
- Institutional guide: `docs/Configuration/SERVICE_REGISTRATION_GUIDELINES.md`
- Source: `Apps/GaChatbot/Services/QueryUnderstandingService.cs:11`, `OllamaGroundedNarrator.cs:22`
