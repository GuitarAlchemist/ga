---
title: "Extract shared OllamaGenerateClient to eliminate duplicated Ollama HTTP plumbing"
date: 2026-03-10
problem_type: "integration-issues"
component: "GA.Business.Core.Orchestration/Clients"
symptoms:
  - "Duplicated DefaultModel constant across two services"
  - "Duplicated CallOllamaAsync HTTP method"
  - "JsonSerializerOptions allocated per-call (hot path)"
tags:
  - "c-sharp"
  - "refactoring"
  - "http-client"
  - "performance"
  - "dependency-injection"
related_patterns:
  - "shared-http-client"
  - "static-readonly-json-options"
severity: "medium"
related_docs:
  - "docs/solutions/architecture/orchestration-library-extraction-gachatbot.md"
---

# Extract shared OllamaGenerateClient to eliminate duplicated Ollama HTTP plumbing

## Problem

Two services in `GA.Business.Core.Orchestration` each contained private copies of the same Ollama HTTP plumbing:

| Duplicated element | Location |
|---|---|
| `DefaultModel` constant | `OllamaGroundedNarrator`, `QueryUnderstandingService` |
| `OllamaBaseUrl` config lookup | Both services |
| `CallOllamaAsync` HTTP POST to `/api/generate` | Both services |
| `new JsonSerializerOptions` per call | `QueryUnderstandingService` |

The `JsonSerializerOptions` allocation is particularly problematic — `System.Text.Json` options objects are expensive to construct and should be created once and reused. Allocating inside a hot path causes unnecessary GC pressure.

## Root Cause

When the Ollama integration was first added, each service independently implemented its own HTTP POST to `/api/generate`. No shared client was created, so duplication accumulated silently over several commits.

## Solution

### 1. New shared client: `OllamaGenerateClient`

```csharp
// Common/GA.Business.Core.Orchestration/Clients/OllamaGenerateClient.cs
namespace GA.Business.Core.Orchestration.Clients;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public sealed class OllamaGenerateClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private const string DefaultModel = "mistral";

    // Static readonly — allocated once, reused forever. Fixes hot-alloc bug.
    private static readonly JsonSerializerOptions _caseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    private string OllamaBaseUrl =>
        configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

    public async Task<string> GenerateAsync(
        string prompt,
        string? format = null,
        float temperature = 0.7f,
        int? numPredict = null,
        CancellationToken ct = default)
    {
        using var http = httpClientFactory.CreateClient();
        var body = new
        {
            model = DefaultModel,
            prompt,
            format,
            stream = false,
            options = new { temperature, num_predict = numPredict },
        };
        var resp = await http.PostAsJsonAsync($"{OllamaBaseUrl}/api/generate", body, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<OllamaResponse>(ct);
        return result?.Response ?? string.Empty;
    }

    public async Task<T> GenerateStructuredAsync<T>(
        string prompt,
        float temperature,
        CancellationToken ct = default)
    {
        var raw = await GenerateAsync(prompt, format: "json", temperature, ct: ct);
        return JsonSerializer.Deserialize<T>(raw, _caseInsensitive)
            ?? throw new InvalidOperationException("Ollama returned null JSON for structured response");
    }

    private sealed record OllamaResponse(string Response);
}
```

### 2. Register as singleton in DI

```csharp
// Common/GA.Business.Core.Orchestration/Extensions/ChatbotOrchestrationExtensions.cs
services.AddSingleton<OllamaGenerateClient>();
```

**Must be `public`**: If `OllamaGenerateClient` is declared `internal`, services in other assemblies that take it as a constructor parameter will get CS0051 ("Inconsistent accessibility"). The class is infrastructure, not a published API, but it must be `public` to cross assembly boundaries in DI.

### 3. Update consumers

Before (`QueryUnderstandingService`):
```csharp
// ~40 lines of private HTTP code + new JsonSerializerOptions() per call
private async Task<QueryFilters?> CallOllamaAsync(string prompt, CancellationToken ct)
{
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };  // hot alloc!
    // ... HttpClient.PostAsJsonAsync ...
}
```

After:
```csharp
public QueryUnderstandingService(
    DomainMetadataPrompter prompter,
    OllamaGenerateClient ollamaClient,
    ILogger<QueryUnderstandingService> logger) { ... }

public async Task<QueryFilters?> UnderstandAsync(string query, CancellationToken ct)
{
    var prompt = _prompter.Build(query);
    return await _ollamaClient.GenerateStructuredAsync<QueryFilters>(prompt, temperature: 0.1f, ct: ct);
}
```

## Prevention

- When adding a second service that calls the same HTTP endpoint, extract the client immediately — don't copy-paste.
- `JsonSerializerOptions` should always be `static readonly` fields, never allocated inside methods.
- Run `dotnet format` with the `IDE0052` analyzer enabled to catch unused private members (often a sign of hidden duplication).

## Key Lesson

`JsonSerializerOptions` is an expensive object. The .NET runtime emits a warning when it's constructed in a hot path. Treat any `new JsonSerializerOptions(...)` inside a method body as a code smell requiring extraction to a `static readonly` field.

```csharp
// ❌ Hot allocation — runs on every request
var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { ... });

// ✅ Amortized — allocated once at class load
private static readonly JsonSerializerOptions _options = new() { ... };
var result = JsonSerializer.Deserialize<T>(json, _options);
```
