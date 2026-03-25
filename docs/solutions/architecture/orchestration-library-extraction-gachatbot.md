---
title: "Extract chatbot orchestration into GA.Business.Core.Orchestration and wire GaApi to ProductionOrchestrator with agent metadata"
date: 2026-03-05
category: architecture
tags: [orchestration, dependency-architecture, signalr, sse, semantic-routing, agents, refactoring, layer-violation, dotnet]
problem_type: layer-violation
components: [GA.Business.Core.Orchestration, GaChatbot, GaApi, SemanticRouter, SpectralRagOrchestrator, ChatbotHub, ChatbotController, ga-client]
symptoms:
  - GaApi bypassing the full agentic pipeline (Ollama called directly)
  - Application-to-application project dependency blocked by architecture rules
  - SemanticRouter and all 5 specialized agents never invoked from GaApi
  - Agent routing metadata (agentId, confidence, routingMethod) not surfaced to frontend
  - Five-layer dependency model violated
resolution_time: long
difficulty: hard
---

# Extract Chatbot Orchestration to Shared Library (GaChatbot → GA.Business.Core.Orchestration)

## Problem Summary

All sophisticated chatbot orchestration — `SemanticRouter`, 5 specialized agents, `SpectralRagOrchestrator`, `TabAwareOrchestrator`, narrator infrastructure — lived exclusively in `Apps/GaChatbot`, an application project. `GaApi` could not reference it (application-to-application dependencies are forbidden by the five-layer architecture). As a result, `GaApi`'s `ChatbotController` and `ChatbotHub` called Ollama directly, bypassing the entire agentic pipeline. The React frontend never received routing metadata.

## Root Cause

CLAUDE.md enforces a strict five-layer dependency model: layers may only depend on layers below them. Both `GaChatbot` and `GaApi` sit at the application layer. Orchestration code (multi-agent workflows, narration, grounding) belongs in **Layer 5 — `GA.Business.Core.Orchestration`** — not in any application project. The shared library didn't exist, so the code had nowhere to live except inside the first app that needed it.

```
BEFORE (violation):
  GaApi (app) → Ollama directly (bypasses routing)
  GaChatbot (app) → ProductionOrchestrator (full pipeline — inaccessible to GaApi)

AFTER (correct):
  GaApi (app) → GA.Business.Core.Orchestration (Layer 5) ← shared
  GaChatbot (app) → GA.Business.Core.Orchestration (Layer 5) ← same
```

## Solution

### 1. Create `Common/GA.Business.Core.Orchestration/` (Layer 5)

New project with four folders:

```
Common/GA.Business.Core.Orchestration/
  Abstractions/
    IHarmonicChatOrchestrator.cs   ← moved from GaChatbot.Abstractions
    IGroundedNarrator.cs           ← moved from GaChatbot.Abstractions
  Models/
    ChatModels.cs                  ← ChatRequest, ChatResponse, AgentRoutingMetadata, CandidateVoicing
  Services/
    ProductionOrchestrator.cs      ← moved + updated to populate ChatResponse.Routing
    TabAwareOrchestrator.cs        ← moved
    SpectralRagOrchestrator.cs     ← moved
    OllamaGroundedNarrator.cs      ← moved
    GroundedPromptBuilder.cs       ← moved
    ResponseValidator.cs           ← moved
    QueryUnderstandingService.cs   ← moved
    DomainMetadataPrompter.cs      ← moved
    TabPresentationService.cs      ← moved
  Extensions/
    ChatbotOrchestrationExtensions.cs  ← NEW — DI registration
```

### 2. DI Extension — `ChatbotOrchestrationExtensions.cs`

Key design decisions:
- `IGroundedNarrator` is **not** registered here — each app provides its own narrator implementation
- Vector index uses `TryAddSingleton` so GaApi's `FileBasedVectorIndex` wins over the InMemory default
- Orchestrators are Scoped (they transitively depend on Scoped services)

```csharp
public static IServiceCollection AddChatbotOrchestration(this IServiceCollection services)
{
    services.TryAddSingleton<IVectorIndex, InMemoryVectorIndex>();

    services.AddSingleton<DomainMetadataPrompter>();
    services.AddSingleton<GroundedPromptBuilder>();
    services.AddSingleton<ResponseValidator>();
    services.AddSingleton<QueryUnderstandingService>();
    services.AddSingleton<TabPresentationService>();

    // IGroundedNarrator intentionally NOT registered here.
    // Each app registers its own: OllamaGroundedNarrator (GaApi) or ExtensionsAINarrator (GaChatbot).

    services.AddScoped<SpectralRagOrchestrator>();
    services.AddScoped<TabAwareOrchestrator>();
    services.AddScoped<ProductionOrchestrator>();
    services.AddScoped<IHarmonicChatOrchestrator>(sp =>
        sp.GetRequiredService<ProductionOrchestrator>());

    return services;
}
```

### 3. Agent Routing Metadata in `ChatResponse`

```csharp
public sealed record AgentRoutingMetadata(
    string AgentId,
    float Confidence,
    string RoutingMethod  // "semantic" | "llm" | "keyword"
);

public sealed record ChatResponse(
    string NaturalLanguageAnswer,
    IReadOnlyList<CandidateVoicing> Candidates,
    Progression? Progression = null,
    AgentRoutingMetadata? Routing = null,   // ← new
    QueryFilters? QueryFilters = null,
    object? DebugParams = null
);
```

### 4. GaApi wiring — `AIServiceExtensions.cs`

```csharp
// Register shared agentic orchestration stack
services.AddChatbotOrchestration();

// GaApi narrator (app-specific)
services.AddScoped<IGroundedNarrator, OllamaGroundedNarrator>();
```

`SemanticRouter` and the 5 agents are registered by `AddGuitarAlchemistAI()` in `GA.Business.ML`.

### 5. SSE streaming — `ChatbotController.cs`

Routing metadata is emitted as the **first** SSE event so the UI can show the agent attribution immediately, before the text arrives:

```csharp
var response = await orchestrator.AnswerAsync(new ChatRequest(message), ct);
var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");

// First event: routing metadata
await WriteSseAsync(JsonSerializer.Serialize(new {
    type = "routing",
    agentId = routing.AgentId,
    confidence = routing.Confidence,
    routingMethod = routing.RoutingMethod
}), ct);

// Subsequent events: text chunks
foreach (var chunk in SplitIntoChunks(response.NaturalLanguageAnswer))
    await WriteSseAsync(chunk, ct);

await Response.WriteAsync("data: [DONE]\n\n", ct);
```

### 6. SignalR — `ChatbotHub.cs`

Emits `MessageRoutingMetadata` before text chunks for feature parity with SSE:

```csharp
var response = await orchestrator.AnswerAsync(new ChatRequest(message), ct);
var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");

await Clients.Caller.SendAsync("MessageRoutingMetadata", new {
    agentId = routing.AgentId,
    confidence = routing.Confidence,
    routingMethod = routing.RoutingMethod
});

foreach (var chunk in SplitIntoChunks(response.NaturalLanguageAnswer))
    await Clients.Caller.SendAsync("ReceiveMessageChunk", chunk);
```

### 7. React frontend — `chatApi.ts` + `chatAtoms.ts` + `ChatMessage.tsx`

**Parse routing from SSE stream:**
```typescript
if (parsed.type === 'routing' && onRouting) {
    onRouting({
        agentId: parsed.agentId as string,
        confidence: parsed.confidence as number,
        routingMethod: parsed.routingMethod as string,
    });
}
```

**Capture in state atom:**
```typescript
let capturedRouting: AgentRouting | undefined;
for await (const chunk of apiService.streamChat(request, undefined,
    (routing) => { capturedRouting = routing; })) { ... }

// Attach to message
set(addMessageAtom, { role: 'assistant', content, routing: capturedRouting });
```

**Display as chip:**
```tsx
{!isUser && message.routing && (
    <Chip
        label={message.routing.agentId}
        size="small"
        title={`Confidence: ${(message.routing.confidence * 100).toFixed(0)}% · ${message.routing.routingMethod}`}
        sx={{ fontSize: '0.65rem', height: 18, bgcolor: 'rgba(255,255,255,0.12)', color: 'inherit' }}
    />
)}
```

### 8. GaChatbot becomes thin host

Only two GaChatbot-specific files remain in `Services/`:
- `ExtensionsAINarrator.cs` — app-specific `IGroundedNarrator` using `Microsoft.Extensions.AI`
- `Infrastructure/GaApiClientVectorIndex.cs` — app-specific `IVectorIndex` calling the GaApi HTTP endpoint

All orchestration code removed from `GaChatbot/Services/`.

## Data Flow After Change

```
User Query
  ↓
ChatbotController (SSE) / ChatbotHub (SignalR)
  ↓
orchestrator.AnswerAsync(ChatRequest)
  ↓  [GA.Business.Core.Orchestration — Layer 5]
ProductionOrchestrator
  ↓
SemanticRouter → selects agent → produces AgentRoutingMetadata
  ↓  [GA.Business.ML — Layer 4]
TheoryAgent / TabAgent / TechniqueAgent / ComposerAgent / CriticAgent
  ↓
ChatResponse { NaturalLanguageAnswer, Routing, Candidates }
  ↓
[SSE]      event 1: { type:"routing", agentId, confidence, routingMethod }
           events 2–N: text chunks
           final: [DONE]
[SignalR]  MessageRoutingMetadata → ReceiveMessageChunk (×N) → MessageComplete
  ↓
React: onRouting callback → state atom → ChatMessage Chip
```

## Verification

- **Build**: 0 errors, 0 new warnings in orchestration project (2 IDE0305 fixed post-move)
- **Tests**: 1228 passed, 0 failed (GaApi.Tests 101, GA.Business.Core.Tests 590, GA.Business.ML.Tests 433, GA.MusicTheory.Service.Tests 3)

## Prevention Strategies

### Enforce at build time — architecture tests

Add a `Tests/Architecture/` project using **NetArchTest** or **ArchUnitNET**:

```csharp
[Test]
public void AppProjects_ShouldNotReference_OtherAppProjects()
{
    var result = Types.InAssembly(typeof(GaApi.Program).Assembly)
        .ShouldNot()
        .HaveDependencyOn("GaChatbot")
        .GetResult();
    Assert.That(result.IsSuccessful, Is.True);
}
```

### CI script — detect app-to-app references before build

```powershell
# Scripts/check-app-dependencies.ps1
$appProjects = Get-ChildItem -Path "Apps/" -Filter "*.csproj" -Recurse
foreach ($proj in $appProjects) {
    $refs = Select-String -Path $proj.FullName -Pattern '<ProjectReference.*Apps/'
    if ($refs) { Write-Error "App-to-app reference in $($proj.Name)"; exit 1 }
}
```

### CLAUDE.md rule to add

> **App-to-app dependencies are forbidden.** `Apps/` projects must never reference other `Apps/` projects. If two apps share logic, that logic belongs in the lowest applicable `Common/` layer. Verify with `dotnet list reference` before adding any `<ProjectReference>` in an `Apps/` project.

## Detection Signals (When to Extract)

Apply "extract to shared library" when **any two** of these are true:

- [ ] The same class name or interface exists in two or more `Apps/` projects
- [ ] A new app project would also need this logic
- [ ] The logic has unit-testable behavior independent of HTTP / SignalR / UI concerns
- [ ] The logic references only Layers 1–4 (no ASP.NET framework types)
- [ ] A change requires editing files in more than one app
- [ ] Two agents/developers are working concurrently on apps that share this logic

**Code smells that signal this problem:**
- Copy-pasted service classes across `Apps/`
- Duplicate `using` directives for the same domain namespace in two apps
- A `Controller` or `Hub` containing multi-step workflow logic (not just request/response mapping)
- `internal` classes in an app project that contain domain logic

## Anti-Patterns to Avoid

| Anti-pattern | Why it fails |
|---|---|
| Extract the interface but leave the implementation in the app | The other app still can't use it |
| Create a new `Common/` project for every extraction | Build time and solution complexity explode; prefer adding a namespace to an existing layer project |
| Constructor-inject `IHubContext<T>` or `IHttpContextAccessor` into the shared library | Leaks ASP.NET concerns into a layer-agnostic library |
| Leave orchestration tests in `Tests/Apps/GaApi.Tests/` after extraction | Coverage gaps when `GaChatbot` diverges |
| Partial extraction — leave dead code in both apps | Creates confusion about what is authoritative |
| Skip the interface boundary (use concrete type directly, no DI) | Defeats testability, harder to re-extract later |

## When to Apply This Pattern

This pattern (extract to `GA.Business.Core.Orchestration`) applies specifically when:
- Logic coordinates multiple services into a multi-step workflow (not pure domain logic)
- Logic is stateless or depends only on injected interfaces
- Two or more application-layer consumers need the same workflow
- The workflow references AI/ML services (Layer 4) or domain analysis (Layer 3)

For lower-level shared logic (pure domain, algorithms): use `GA.Business.Core` or `GA.Domain.Core` instead.

## Related Documentation

- **Brainstorm**: `docs/brainstorms/2026-03-02-functional-chatbot-agentic-routing-brainstorm.md` — approach selection (A: extract to Layer 5 vs B: GA.Business.ML vs C: Semantic Kernel replacement)
- **Plan**: `docs/plans/2026-03-02-feat-functional-chatbot-agentic-routing-plan.md` — full implementation phases and acceptance criteria
- **Architecture rule**: `CLAUDE.md` § Five-Layer Dependency Model — "Orchestration code belongs in `GA.Business.Core.Orchestration`, not in low-level libraries"
- **Related solution**: `docs/solutions/refactoring/dotnet-solution-structure-cleanup.md` — parallel structural cleanup in the same modernization wave
- **Key files**:
  - `Common/GA.Business.Core.Orchestration/Extensions/ChatbotOrchestrationExtensions.cs`
  - `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs`
  - `Apps/ga-server/GaApi/Controllers/ChatbotController.cs`
  - `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs`
  - `Apps/ga-client/src/services/chatApi.ts`
  - `Apps/ga-client/src/components/Chat/ChatMessage.tsx`
