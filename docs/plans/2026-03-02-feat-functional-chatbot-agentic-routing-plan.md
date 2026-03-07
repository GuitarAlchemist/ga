---
title: Functional Chatbot Agentic Routing
type: feat
status: completed
date: 2026-03-02
origin: docs/brainstorms/2026-03-02-functional-chatbot-agentic-routing-brainstorm.md
---

# feat: Functional Chatbot Agentic Routing

## Overview

Wire the React frontend's chatbot UI to the full agentic pipeline — semantic routing, 5 specialized agents, Spectral RAG narration, and anti-hallucination guardrails — by extracting orchestration infrastructure from the `GaChatbot` console app into the shared `GA.Business.Core.Orchestration` library. The `GaApi` `ChatbotController` (SSE) and `ChatbotHub` (SignalR) are then updated to use `ProductionOrchestrator` directly. Agent metadata (`agentId`, `confidence`, `routingMethod`) is surfaced to the React UI.

> See brainstorm: `docs/brainstorms/2026-03-02-functional-chatbot-agentic-routing-brainstorm.md`

## Problem Statement

All sophisticated orchestration lives **exclusively** in `Apps/GaChatbot` — an application project. `GaApi` injects `ChatbotSessionOrchestrator` which calls Ollama directly, bypassing the `SemanticRouter`, all 5 agents, and `SpectralRagOrchestrator` entirely. The 5 agent registrations in `GA.AI.Service/Program.cs` are present but never invoked. The `GA.AI.Service/Program.cs` itself has 4 commented-out TODO registrations proving this gap has been recognized.

**Root cause**: Application-to-application project dependency is not permitted. The orchestrators must live in a shared library.

## Proposed Solution

Extract all orchestrator + narrator infrastructure to `Common/GA.Business.Core.Orchestration/` (Layer 5, per CLAUDE.md architectural rule). Both `GaChatbot` (console) and `GaApi` then reference a single shared library. `GaChatbot` becomes a thin host. `GaApi` swaps `ChatbotSessionOrchestrator` for `ProductionOrchestrator`.

> Why not GA.Business.ML? (see brainstorm) — mixes orchestration and ML concerns, violates CLAUDE.md layering rules.
> Why not Semantic Kernel Agent replacement? — large scope risk, replaces a working system, out of scope.

## Technical Approach

### Architecture After Change

```
Common/GA.Business.Core.Orchestration/   ← NEW project (Layer 5)
  Abstractions/
    IHarmonicChatOrchestrator.cs         ← moved from GaChatbot.Abstractions
    IGroundedNarrator.cs                 ← moved from GaChatbot.Abstractions
  Models/
    ChatRequest.cs                       ← moved from GaChatbot.Models
    ChatResponse.cs                      ← moved (+ AgentId, Confidence, RoutingMethod fields)
    CandidateVoicing.cs                  ← moved from GaChatbot.Models
    AgentRoutingMetadata.cs              ← NEW
  Services/
    ProductionOrchestrator.cs            ← moved from GaChatbot.Services
    TabAwareOrchestrator.cs              ← moved from GaChatbot.Services
    SpectralRagOrchestrator.cs           ← moved from GaChatbot.Services
    OllamaGroundedNarrator.cs            ← moved from GaChatbot.Services
    GroundedPromptBuilder.cs             ← moved from GaChatbot.Services
    ResponseValidator.cs                 ← moved from GaChatbot.Services
    DomainMetadataPrompter.cs            ← moved from GaChatbot.Services
    QueryUnderstandingService.cs         ← moved from GaChatbot.Services
  Extensions/
    OrchestrationServiceExtensions.cs   ← NEW (DI registration)

Apps/GaChatbot/                          ← becomes thin host
  Program.cs                            ← updated: references shared library DI extension
  (all Services/ files removed)

Apps/ga-server/GaApi/
  Controllers/ChatbotController.cs      ← inject ProductionOrchestrator, stream agent metadata
  Hubs/ChatbotHub.cs                    ← inject ProductionOrchestrator, emit agentId/confidence
  Services/ChatbotSessionOrchestrator.cs← KEEP (for legacy compatibility, can be removed later)

Apps/ga-client/src/
  services/chatApi.ts                   ← extend ChatResponse to include agentMetadata
  components/ChatMessage.tsx (or equiv) ← display "Answered by TheoryAgent (0.91)"
```

### Agent Metadata Extension

`ChatResponse` gains routing metadata:

```csharp
// In GA.Business.Core.Orchestration/Models/
public sealed record AgentRoutingMetadata(
    string AgentId,
    double Confidence,
    string RoutingMethod  // "semantic" | "llm" | "keyword"
);

public sealed record ChatResponse(
    string NaturalLanguageAnswer,
    IReadOnlyList<CandidateVoicing> Candidates,
    Progression? Progression = null,
    object? DebugParams = null,
    AgentRoutingMetadata? Routing = null   // ← NEW
);
```

SSE chunk format extended:

```json
{ "chunk": "Here are some voicings...", "done": false }
// Final chunk:
{ "chunk": "", "done": true, "agentId": "TheoryAgent", "confidence": 0.91, "routingMethod": "semantic" }
```

SignalR `MessageComplete` event extended with the same fields.

### System-Wide Impact

| Layer | Impact |
|---|---|
| `GA.Business.Core.Orchestration` | New project — all orchestrators live here |
| `GaChatbot` .csproj | Adds `ProjectReference` to Orchestration; removes moved .cs files |
| `GaApi` .csproj | Adds `ProjectReference` to Orchestration; uncomments 4 DI registrations in `Program.cs` |
| `ChatbotController` SSE | Changes from raw string chunks to JSON chunk + final metadata event |
| `ChatbotHub` SignalR | Adds `AgentId`, `Confidence`, `RoutingMethod` to `MessageComplete` payload |
| React `chatApi.ts` | Parses agent metadata from final SSE event |
| React chat UI | Displays agent attribution below assistant message |
| `CoreSchema` (namespace) | `ChatRequest`/`ChatResponse`/`CandidateVoicing` change namespace from `GaChatbot.Models` → `GA.Business.Core.Orchestration.Models` |

**Namespace cascade**: All consumers of `GaChatbot.Models` and `GaChatbot.Abstractions` need `using` updates — primarily `SpectralRagOrchestrator`, `TabAwareOrchestrator`, `ProductionOrchestrator` (all moving together, so internal references self-heal).

## Acceptance Criteria

- [ ] `Common/GA.Business.Core.Orchestration/` project created and builds cleanly
- [ ] All 8 types from `GaChatbot.Services/` and `GaChatbot.Abstractions/` moved to shared project
- [ ] `GaChatbot` builds and runs as thin host — no duplicated orchestrator code
- [ ] `GaApi/Program.cs` uncomments and wires the 4 TODO registrations
- [ ] `ChatbotController` SSE endpoint uses `ProductionOrchestrator.AnswerAsync`; final SSE chunk includes `agentId`/`confidence`/`routingMethod`
- [ ] `ChatbotHub.SendMessage` uses `ProductionOrchestrator.AnswerAsync`; `MessageComplete` event includes routing metadata
- [ ] React `chatApi.ts` parses agent metadata from final SSE chunk
- [ ] React chat UI displays agent attribution (e.g., "Answered by TheoryAgent · 0.91 confidence")
- [ ] All existing backend tests pass
- [ ] `dotnet build AllProjects.slnx` produces 0 errors, 0 new warnings

## Implementation Phases

### Phase 1: Create `GA.Business.Core.Orchestration` project ✦ [~1h]

1. Create `Common/GA.Business.Core.Orchestration/` directory
2. Create `.csproj` with references to `GA.Business.ML` and `GA.Domain.Core`
3. Add to `AllProjects.slnx`
4. Create folder structure: `Abstractions/`, `Models/`, `Services/`, `Extensions/`

**Files to create:**
- `Common/GA.Business.Core.Orchestration/GA.Business.Core.Orchestration.csproj`

### Phase 2: Move Models and Abstractions ✦ [~30m]

Move with namespace change (`GaChatbot.*` → `GA.Business.Core.Orchestration.*`):

- `GaChatbot.Abstractions.IHarmonicChatOrchestrator` → `GA.Business.Core.Orchestration.Abstractions`
- `GaChatbot.Abstractions.IGroundedNarrator` → `GA.Business.Core.Orchestration.Abstractions`
- `GaChatbot.Models.ChatRequest` → `GA.Business.Core.Orchestration.Models`
- `GaChatbot.Models.ChatResponse` → `GA.Business.Core.Orchestration.Models` (+ `AgentRoutingMetadata?`)
- `GaChatbot.Models.CandidateVoicing` → `GA.Business.Core.Orchestration.Models`

**New file:**
- `Models/AgentRoutingMetadata.cs`

### Phase 3: Move Services ✦ [~1h]

Move with namespace change:

- `OllamaGroundedNarrator.cs`
- `GroundedPromptBuilder.cs`
- `ResponseValidator.cs`
- `DomainMetadataPrompter.cs`
- `QueryUnderstandingService.cs`
- `SpectralRagOrchestrator.cs`
- `TabAwareOrchestrator.cs`
- `ProductionOrchestrator.cs` — update `AnswerAsync` to populate `ChatResponse.Routing`

**New file:**
- `Extensions/OrchestrationServiceExtensions.cs` — `AddGuitarAlchemistOrchestration(this IServiceCollection)`

### Phase 4: Update `GaChatbot` to thin host ✦ [~20m]

- Add `ProjectReference` to `GA.Business.Core.Orchestration`
- Delete moved `.cs` files from `GaChatbot/Services/` and `GaChatbot/Abstractions/`
- Update `GaChatbot/Program.cs` to call `services.AddGuitarAlchemistOrchestration()`
- Fix any remaining `using` statements

### Phase 5: Wire `GaApi` ✦ [~45m]

- Add `ProjectReference` to `GA.Business.Core.Orchestration` in `GaApi.csproj`
- In `GaApi/Program.cs`: uncomment the 4 TODO registrations; call `AddGuitarAlchemistOrchestration()`
- Update `ChatbotController`:
  - Inject `ProductionOrchestrator` (primary) and keep `ChatbotSessionOrchestrator` as fallback
  - Stream `ProductionOrchestrator.AnswerAsync` result; emit routing metadata in final SSE chunk
- Update `ChatbotHub`:
  - Inject `ProductionOrchestrator`
  - Emit `AgentId`, `Confidence`, `RoutingMethod` with `MessageComplete`

### Phase 6: Update React Frontend ✦ [~30m]

- Update `chatApi.ts`: parse `agentId`, `confidence`, `routingMethod` from final SSE chunk
- Update `chatService.ts` `SendChatOptions.onComplete` or add `onMetadata` callback
- Update chat message component to display agent attribution badge

### Phase 7: Build + Test ✦ [~20m]

- `dotnet build AllProjects.slnx`
- `pwsh Scripts/run-all-tests.ps1 -BackendOnly`
- Manual smoke test: send a theory query → verify routing metadata in response

## Dependencies & Prerequisites

- `GA.Business.ML` (already exists) — contains `SemanticRouter`, 5 agents, `MusicalEmbeddingGenerator`
- `GA.Domain.Core` (already exists) — contains `Progression`
- `AllProjects.slnx` — needs new project entry
- Qdrant vector index must be running for `SpectralRagOrchestrator` (local dev: Docker)

## Risk Analysis

| Risk | Likelihood | Mitigation |
|---|---|---|
| Namespace cascade breaks build | Medium | Move all files in one commit; build before wiring GaApi |
| `ProductionOrchestrator` has GaChatbot-only transitive deps | Low | Already checked — all deps are in `GA.Business.ML` |
| SSE chunk format change breaks React | Low | React already handles raw string chunks; metadata added to final chunk only |
| SignalR `MessageComplete` contract change | Low | Frontend only uses subset of fields; additive change |

## Sources & References

### Origin

- **Brainstorm:** [docs/brainstorms/2026-03-02-functional-chatbot-agentic-routing-brainstorm.md](docs/brainstorms/2026-03-02-functional-chatbot-agentic-routing-brainstorm.md)
  - Key decisions carried forward: Extract to Layer 5, GaChatbot becomes thin host, expose routing metadata to frontend

### Internal References

- `Apps/GaChatbot/Services/ProductionOrchestrator.cs` — primary orchestrator to move
- `Apps/ga-server/GaApi/Program.cs:59-63` — 4 TODO registrations to uncomment
- `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` — SSE endpoint to update
- `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs` — SignalR hub to update
- `Apps/ga-client/src/services/chatApi.ts` — frontend API service to extend
- `CLAUDE.md:94-95` — "Orchestration code belongs in `GA.Business.Core.Orchestration`"
- `conductor/tracks/spectral-rag-chatbot/plan.md:Phase 5` — marks agentic routing as in-progress

### Existing Patterns to Follow

- Controller pattern: `GA.MusicTheory.Service/Controllers/` (ApiResponse<T>, ProducesResponseType)
- Service registration: `Common/GA.Business.ML/Extensions/AiServiceExtensions.cs` (`AddGuitarAlchemistAI()`)
- Orchestration project reference: `GA.Business.Core.Orchestration` (per CLAUDE.md, `IntelligentBSPGenerator` as prior art)
