---
date: 2026-03-07
branch: feat/chatbot-orchestration-extraction
cycle: chatbot-orchestration-extraction
---

# System Snapshot — 2026-03-07

## Branch & Commit

- **Branch**: `feat/chatbot-orchestration-extraction`
- **Commit**: `0c67d26c` — refactor(chatbot): address review findings — rename, rate limit, prompt sanitization, docs subagent

---

## Layer Inventory

| Layer | Project(s) | Primary Responsibility |
|---|---|---|
| **1 — Core** | `GA.Core`, `GA.Domain.Core` | Pure domain primitives: Note, Interval, PitchClass, Fretboard types |
| **2 — Domain** | `GA.Business.Core`, `GA.Business.Config` (F#), `GA.BSP.Core`, `GA.Business.DSL` (F#) | Business logic, YAML configuration, BSP geometry, music theory DSL (parsers, closures, LSP, FSI pool) |
| **3 — Analysis** | `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard`, `GA.Business.Analytics` | Chord/scale analysis, voice leading, spectral/topological analysis |
| **4 — AI/ML** | `GA.Business.ML`, `GA.Business.AI` | Semantic indexing, Ollama/ONNX embeddings, vector search, Spectral RAG, tab solving, agent base classes |
| **5 — Orchestration** | `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence` | High-level workflows, `IntelligentBSPGenerator`, `ChatbotSessionOrchestrator`, `ProductionOrchestrator`, `GroundedNarrator` |

> Note: `GA.Business.DSL` formally resides at Layer 2 by project structure. `AgentClosures.fs` within it makes outbound HTTP calls to the GA API — a Layer 5 concern. Full migration deferred as A-1 full (see Open Architectural Questions).

---

## Key Interfaces Changed This Cycle

### `IChatService` (renamed from `IOllamaChatService`)
- **File**: `Apps/ga-server/GaApi/Services/IChatService.cs`
- **Change**: Interface renamed across 7 files (interface, `OllamaChatService`, `ClaudeChatService`, `OllamaChatClientAdapter`, `ChatbotController`, `AIServiceExtensions`, orchestrator).
- **Before**: `IOllamaChatService` — implementation name leaked into the abstraction contract.
- **After**: `IChatService` — provider-neutral. `AI:ChatProvider` config key selects `OllamaChatService` (default) or `ClaudeChatService` at startup.

### `VoicingExplanationDto` (new)
- **File**: `Common/GA.Business.Core.Orchestration/Models/ChatModels.cs`
- **Change**: New record introduced to mirror `VoicingExplanation` from `GA.Business.ML` without importing the ML layer into Orchestration contracts.
- **Before**: `CandidateVoicing` carried `VoicingExplanation` directly (Layer 4 type in Layer 5 contract — violation A-2).
- **After**: `CandidateVoicing` carries `VoicingExplanationDto`. `SpectralRagOrchestrator` maps via `ToDto(VoicingExplanation e)` at the boundary.

### `GroundedPromptBuilder` (sanitization methods added)
- **File**: `Common/GA.Business.Core.Orchestration/Services/GroundedPromptBuilder.cs`
- **Change**: Added `SanitizeQuery(string)` and `SanitizeField(string?)` — NFKD normalization + compiled injection regex stripping `SYSTEM:`, `USER:`, `ASSISTANT:`, `\nHuman:`, `\nAssistant:`, `###`, and triple-backtick sequences. All user query and DB-sourced fields now pass through sanitization before prompt construction.
- **Before**: Raw string interpolation of user input and DB candidate fields into LLM prompt.
- **After**: All values sanitized; query capped at 500 characters.

### `GaClosureBootstrap` double-registration removed
- **Files**: `AgentClosures.fs`, `DomainClosures.fs`, `IoClosures.fs`, `PipelineClosures.fs`, `TabClosures.fs` (all under `Common/GA.Business.DSL/Closures/BuiltinClosures/`)
- **Change**: Module-level `do register ()` bindings removed from all 5 files. `GaClosureBootstrap.init()` in `Library.fs` is now the sole registration driver.
- **Before**: Each module registered itself via `do` at load time AND was called again by bootstrap — double registration on every startup.
- **After**: Single registration path. Bootstrap is idempotent.

---

## Active Config Keys

Source: `Apps/ga-server/GaApi/appsettings.json`

| Key Path | Value / Default | Notes |
|---|---|---|
| `ConnectionStrings:MusicalKnowledge` | `Data Source=musical-knowledge.db` | SQLite local knowledge store |
| `OpenAI:ApiKey` | `""` | Not used by default; placeholder for future OpenAI embedding |
| `OpenAI:Model` | `text-embedding-3-small` | |
| `VoicingSearch:MaxVoicingsToIndex` | `2147483647` | Effectively unbounded |
| `VoicingSearch:MinPlayedNotes` | `2` | Minimum fretted strings for indexing |
| `VoicingSearch:NoteCountFilter` | `All` | |
| `VoicingSearch:EnableIndexing` | `true` | |
| `VoicingSearch:LazyLoading` | `false` | |
| `VectorSearch:PreferredStrategies` | `["CUDA","InMemory","MongoDB"]` | Ordered fallback chain |
| `VectorSearch:EnableAutoSwitching` | `false` | |
| `VectorSearch:AutoSwitchThreshold` | `20.0` | Latency threshold (ms) for auto-switch |
| `VectorSearch:PreloadStrategies` | `false` | |
| `VectorSearch:MaxMemoryUsageMB` | `2048` | ILGPU/in-memory cap |
| `Graphiti:BaseUrl` | `http://localhost:8000` | Knowledge graph service |
| `Graphiti:Timeout` | `00:00:30` | |
| `Graphiti:MaxRetries` | `3` | |
| `Caching:Regular:ExpirationMinutes` | `15` | Standard cache TTL |
| `Caching:Regular:SizeLimit` | `1000` | |
| `Caching:Semantic:ExpirationMinutes` | `5` | Semantic search cache TTL |
| `Caching:Semantic:SizeLimit` | `100` | |
| `Chatbot:EnableSemanticSearch` | `true` | |
| `Chatbot:StreamTimeoutSeconds` | `60` | |
| `Chatbot:HistoryLimit` | `12` | Max turns retained in session |
| `Chatbot:SemanticSearchLimit` | `5` | Candidates fetched from OPTIC-K |
| `Chatbot:Model` | `llama3.2:3b` | Default Ollama model |
| `Chatbot:SemanticContextDocuments` | `3` | |
| `GuitarAgents:Temperature` | `0.6` | LLM sampling temperature for agents |
| `GuitarAgents:TopP` | `0.9` | |
| `GuitarAgents:MaxOutputTokens` | `600` | |
| `GuitarAgents:TimeoutSeconds` | `45` | |
| `GuitarAgents:IncludeRawOutput` | `true` | |
| `GuitarAgents:EnableQualityPass` | `true` | CriticAgent quality review pass |
| `TarsMcp:BaseUrl` | `http://localhost:9001` | TARS MCP sidecar |
| `TarsMcp:EnableGpuMonitoring` | `true` | |
| `TarsMcp:EnableSystemMonitoring` | `true` | |
| `AI:ChatProvider` | _(not in appsettings — resolved at runtime)_ | `"ollama"` (default) or `"claude"` — selects `OllamaChatService` vs `ClaudeChatService` |
| `Ollama:Endpoint` | _(resolved in AIServiceExtensions)_ | Defaults to `http://localhost:11434` |
| `Ollama:EmbeddingModel` | _(resolved in AIServiceExtensions)_ | Defaults to `nomic-embed-text` |
| `Ollama:BaseUrl` | _(resolved in AIServiceExtensions)_ | Defaults to `http://localhost:11434` |

---

## Known TODOs in Changed Files

Grep for `// TODO` in `GaEvalController.cs`, `GaDslTool.cs`, `Program.cs`, `GroundedPromptBuilder.cs`, and `AgentClosures.fs` returned no matches. All files are TODO-clean as of this snapshot.

---

## Open Architectural Questions

| ID | Question | Status |
|---|---|---|
| **A-1 full** | Full migration of `AgentClosures.fs` from `GA.Business.DSL` (Layer 2) to `GA.Business.Core.Orchestration` (Layer 5) with `IHttpClientFactory` DI wiring across the F#/C# boundary. Currently the module makes outbound HTTP calls to the GA API — an Orchestration concern living in the DSL layer. | Deferred — breaking change requiring DI wiring across F#/C# boundary |
| **L-1** | Anthropic SDK may emit the `ANTHROPIC_API_KEY` value through debug/trace-level logging depending on SDK version and log provider configuration. Needs SDK-level investigation to confirm and suppress. | Deferred — needs SDK investigation |
| **L-2** | `OllamaChatService.IsAvailableAsync` swallows exceptions silently on the health-check path, making Ollama connectivity failures invisible in structured logs. | Deferred — observability improvement |
