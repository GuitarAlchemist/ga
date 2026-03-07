---
status: pending
priority: p1
issue_id: "011"
tags: [performance, concurrency, httpclient, code-review]
dependencies: []
---

# 011 — `OllamaChatService` Singleton Mutates `HttpClient.BaseAddress` After First Request

## Problem Statement
`OllamaChatService` is registered as a **singleton** in `AIServiceExtensions.cs`. Its constructor calls `httpClientFactory.CreateClient("Ollama")` and then mutates `_httpClient.BaseAddress`. Setting `BaseAddress` after the first request has been dispatched is a documented violation of `HttpClient` contract — the behavior is undefined and can cause request routing errors, especially under concurrent load. `BatchOllamaEmbeddingService` has the same defect on the same named client, meaning both services race to mutate a shared property.

## Findings
- `Apps/ga-server/GaApi/Services/OllamaChatService.cs:30`: constructor assigns `_httpClient.BaseAddress = new Uri(...)` after `CreateClient` returns.
- `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs:69`: `OllamaChatService` registered as singleton.
- `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs:100`: `BatchOllamaEmbeddingService` also mutates `BaseAddress` on the same `"Ollama"` named client.
- Microsoft documentation explicitly states: "BaseAddress should be set before the first request is sent. Setting it after the first request may cause issues."
- In a singleton lifetime, the first request fires at startup and subsequent `BaseAddress` writes from other services create a data race.

## Proposed Solutions
### Option A — Configure `BaseAddress` at registration time (recommended)
Move `BaseAddress` configuration into the `AddHttpClient("Ollama", c => { c.BaseAddress = new Uri(...); })` call in `AIServiceExtensions.cs`. Remove the assignment from the constructors of `OllamaChatService` and `BatchOllamaEmbeddingService`.

**Pros:** Follows `IHttpClientFactory` best practices; `BaseAddress` is immutable after the handler is built; no race condition; clean separation of concerns.
**Cons:** None significant; configuration must be read from `IConfiguration` at registration time (already available in the extension method).
**Effort:** Small
**Risk:** Low

### Option B — Store base URL as `string`, construct `Uri` per request
Remove `BaseAddress` from the client entirely. Store the configured URL string as a field in each service and prepend it to the path when constructing each `HttpRequestMessage`.

**Pros:** Avoids shared mutable state; also avoids the `BaseAddress` + relative-URI subtleties (trailing slash rules).
**Cons:** More verbose per-request construction; slightly more boilerplate.
**Effort:** Small
**Risk:** Low

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/Services/OllamaChatService.cs:30`
  - `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs:69, 100`
- **Components:** `OllamaChatService`, `BatchOllamaEmbeddingService`, `IHttpClientFactory` named client `"Ollama"`

## Acceptance Criteria
- [ ] `BaseAddress` is set exactly once, at `AddHttpClient` registration time (Option A) or not set at all (Option B).
- [ ] Neither `OllamaChatService` nor `BatchOllamaEmbeddingService` mutate `HttpClient` properties in their constructors or methods.
- [ ] Concurrent requests to Ollama during integration tests produce no routing errors or `InvalidOperationException`.
- [ ] Zero new warnings.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
