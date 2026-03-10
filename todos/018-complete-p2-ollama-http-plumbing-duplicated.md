---
status: complete
priority: p2
issue_id: "018"
tags: [quality, duplication, performance, code-review]
dependencies: []
---

# 018 — Ollama HTTP Plumbing Duplicated Across Two Services + Hot JsonSerializerOptions Alloc

## Problem Statement

`OllamaGroundedNarrator.cs` and `QueryUnderstandingService.cs` both independently contain an identical ~20-line block of Ollama HTTP plumbing:

- `private const string DefaultModel = "llama3.2"`
- `private string OllamaBaseUrl => configuration["Ollama:Endpoint"] ?? "http://localhost:11434"`
- Manual `JsonSerializer.Serialize` + `StringContent` + `client.PostAsync($"{OllamaBaseUrl}/api/generate")` + `JsonDocument.Parse` + `.GetProperty("response").GetString()`

This duplication means bug fixes and model changes must be applied in two places. Any future Ollama caller will copy-paste a third time.

Additionally, `QueryUnderstandingService.cs:54` allocates `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` on every deserialization call. `JsonSerializerOptions` is an expensive object that caches reflection metadata; allocating it per-call defeats that caching and creates GC pressure.

## Findings

- Identical HTTP plumbing block exists in both `OllamaGroundedNarrator.cs` and `QueryUnderstandingService.cs`.
- `QueryUnderstandingService.cs` line 54: `new JsonSerializerOptions(...)` inside a hot deserialization path.

## Proposed Solutions

### Option A — Extract a shared internal OllamaGenerateClient class
Create `Common/GA.Business.ML/Clients/OllamaGenerateClient.cs` (or similar path within the ML layer):
```csharp
internal sealed class OllamaGenerateClient(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions CaseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    private string BaseUrl =>
        configuration["Ollama:Endpoint"] ?? "http://localhost:11434";

    public async Task<string> GenerateAsync(
        string model, string prompt,
        string? format = null, float temperature = 0.7f,
        CancellationToken ct = default)
    { ... }
}
```
Both services take it via constructor injection. The `static readonly` options instance fixes the allocation issue as a side effect.
**Pros:** Single implementation; easy to unit-test; fixes allocation in one move.
**Cons:** New file; existing callers need minor refactor.
**Effort:** Medium.
**Risk:** Low.

### Option B — Inject a typed IOllamaGenerateClient interface registered in DI
Same as Option A but expose an interface for testability and mocking.
**Pros:** Mockable in unit tests; follows the project's DI conventions.
**Cons:** Slightly more boilerplate (interface + implementation).
**Effort:** Medium.
**Risk:** Low.

### Micro-fix (independent) — Fix the hot JsonSerializerOptions alloc
Move the options object to a `private static readonly` field in `QueryUnderstandingService`:
```csharp
private static readonly JsonSerializerOptions CaseInsensitiveOptions =
    new() { PropertyNameCaseInsensitive = true };
```
**Effort:** Trivial.
**Risk:** None.

## Recommended Action

## Technical Details

- **Affected files:**
  - `Common/GA.Business.ML/Services/OllamaGroundedNarrator.cs`
  - `Common/GA.Business.ML/Services/QueryUnderstandingService.cs` (line 54)

## Acceptance Criteria

- [ ] Ollama HTTP plumbing exists in exactly one place; both services delegate to it.
- [ ] `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true` is allocated once (`static readonly`), not per-call.
- [ ] All existing ML service tests pass.
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings.

## Work Log

- 2026-03-07 — Identified in /ce:review of PR #2

## Resources

- PR: https://github.com/GuitarAlchemist/ga/pull/2
