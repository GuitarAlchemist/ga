---
generated: 2026-03-07
branch: feat/chatbot-orchestration-extraction
commit: 0c67d26c
note: This file is overwritten at the end of each compound engineering cycle. Do not hand-edit.
---

# Service Inventory

Living register of every named service, agent, MCP tool, hub, controller, and GA closure in the Guitar Alchemist system.

---

## DI-Registered Services (`GaApi`)

Sources: `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs`, `Apps/ga-server/GaApi/Program.cs`

| Name | Type | Layer | File | Registered As | Notes |
|---|---|---|---|---|---|
| `IChatService` → `OllamaChatService` | Service | 5 | `Apps/ga-server/GaApi/Services/OllamaChatService.cs` | `AddSingleton<IChatService, OllamaChatService>` | Default when `AI:ChatProvider != "claude"` |
| `IChatService` → `ClaudeChatService` | Service | 5 | `Apps/ga-server/GaApi/Services/ClaudeChatService.cs` | `AddSingleton<IChatService, ClaudeChatService>` | Active when `AI:ChatProvider = "claude"` |
| `IChatClient` → `OllamaChatClientAdapter` | Service | 5 | `Apps/ga-server/GaApi/Services/OllamaChatClientAdapter.cs` | `AddSingleton<IChatClient, OllamaChatClientAdapter>` | MEAI adapter used by `SemanticRouter` |
| `IEmbeddingGenerator<string, Embedding<float>>` | Service | 4 | `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs` | `AddSingleton` | Created via `OllamaProvider.CreateEmbeddingGeneratorFromConfig` |
| `ITabCorpusRepository` → `InMemoryTabCorpusRepository` | Repository | 2 | `GA.Infrastructure.Persistence` | `AddSingleton` | In-memory tab corpus |
| `IProgressionCorpusRepository` → `InMemoryProgressionCorpusRepository` | Repository | 2 | `GA.Infrastructure.Persistence` | `AddSingleton` | In-memory progression corpus |
| `LocalEmbeddingService` | Service | 4 | `Apps/ga-server/GaApi/Services/LocalEmbeddingService.cs` | `AddSingleton` | Used by vector search |
| `VectorSearchService` | Service | 4 | `Apps/ga-server/GaApi/Services/VectorSearchService.cs` | `AddSingleton` | Standard vector search facade |
| `IBatchEmbeddingService` → `BatchOllamaEmbeddingService` | Service | 4 | `GA.Data.SemanticKernel.Embeddings` | `AddTransient` | Used by `VoicingIndexInitializationService` |
| `OllamaEmbeddingService` | Service | 4 | `Apps/ga-server/GaApi/Services/OllamaEmbeddingService.cs` | `AddSingleton` | Used by `SemanticKnowledgeSource` |
| `SemanticKnowledgeSource` | Service | 5 | `Apps/ga-server/GaApi/Services/SemanticKnowledgeSource.cs` | `AddSingleton` | Bridges voicing search to chatbot |
| `ISemanticKnowledgeSource` → `SemanticKnowledgeSource` | Service | 5 | `Apps/ga-server/GaApi/Services/ISemanticKnowledgeSource.cs` | `AddSingleton` (forwarded) | |
| `ChatbotSessionOrchestrator` | Service | 5 | `Apps/ga-server/GaApi/Services/ChatbotSessionOrchestrator.cs` | `AddScoped` | Per-request conversation flow manager |
| `IGroundedNarrator` → `OllamaGroundedNarrator` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/OllamaGroundedNarrator.cs` | `AddScoped` | LLM narrative generation |
| `ContextualChordService` | Service | 3 | `Apps/ga-server/GaApi/Services/ContextualChordService.cs` | `AddSingleton` | |
| `VoicingFilterService` | Service | 3 | `Apps/ga-server/GaApi/Services/VoicingFilterService.cs` | `AddSingleton` | |
| `MonadicHealthCheckService` | Service | 5 | `Apps/ga-server/GaApi/Services/MonadicHealthCheckService.cs` | `AddMonadicHealthCheckService()` | ROP-wrapped health check |
| `MonadicChordService` | Service | 3 | `Apps/ga-server/GaApi/Services/MonadicChordService.cs` | `AddMonadicChordService()` | ROP-wrapped chord service |

Orchestration services registered via `AddChatbotOrchestration()` extension (`ChatbotOrchestrationExtensions.cs`):

| Name | Type | Layer | File | Notes |
|---|---|---|---|---|
| `ProductionOrchestrator` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs` | Top-level chatbot orchestrator |
| `SpectralRagOrchestrator` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/SpectralRagOrchestrator.cs` | OPTIC-K RAG pipeline |
| `TabAwareOrchestrator` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/TabAwareOrchestrator.cs` | Tab-aware response path |
| `GroundedPromptBuilder` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/GroundedPromptBuilder.cs` | Prompt construction + sanitization |
| `QueryUnderstandingService` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/QueryUnderstandingService.cs` | LLM query filter extraction |
| `DomainMetadataPrompter` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/DomainMetadataPrompter.cs` | Domain concept injection |
| `ResponseValidator` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/ResponseValidator.cs` | Response quality validation |
| `TabPresentationService` | Service | 5 | `Common/GA.Business.Core.Orchestration/Services/TabPresentationService.cs` | Tab formatting for responses |

---

## SignalR Hubs

Source: `Apps/ga-server/GaApi/Hubs/`, `Apps/ga-server/GaApi/Program.cs`

| Name | Type | Layer | File | Route | Notes |
|---|---|---|---|---|---|
| `ChatbotHub` | Hub | 5 | `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs` | `/hubs/chatbot` | WebSocket chatbot streaming |

---

## Controllers

Source: `Apps/ga-server/GaApi/Controllers/`

| Name | Type | Layer | File | Route | Notes |
|---|---|---|---|---|---|
| `ChatbotController` | Controller | 5 | `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` | `api/chatbot` | REST chatbot endpoint |
| `GaEvalController` | Controller | 2 | `Apps/ga-server/GaApi/Controllers/GaEvalController.cs` | `api/ga` | FSI eval + closure listing; eval guarded to `IsDevelopment()` only |
| `ContextualChordsController` | Controller | 3 | `Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs` | — | Contextual chord queries |
| `MonadicChordsController` | Controller | 3 | `Apps/ga-server/GaApi/Controllers/MonadicChordsController.cs` | — | ROP-wrapped chord endpoint |
| `MonadicHealthController` | Controller | 5 | `Apps/ga-server/GaApi/Controllers/MonadicHealthController.cs` | — | ROP-wrapped health endpoint |
| `HealthController` | Controller | 5 | `Apps/ga-server/GaApi/Controllers/HealthController.cs` | — | Standard health endpoint |
| `SearchController` | Controller | 4 | `Apps/ga-server/GaApi/Controllers/SearchController.cs` | — | Vector search endpoint |

---

## GraphQL Query Types

Source: `Apps/ga-server/GaApi/GraphQL/Queries/`, `Apps/ga-server/GaApi/Program.cs`

| Name | Type | Layer | Notes |
|---|---|---|---|
| `Query` | GraphQL Root | 5 | Root query type |
| `ChordNamingQuery` | GraphQL Extension | 3 | Chord naming queries |
| `MusicTheoryQuery` | GraphQL Extension | 2 | Music theory queries |
| `DomainSchemaQuery` | GraphQL Extension | 2 | Domain schema introspection |
| `MongoCollectionsQuery` | GraphQL Extension | 4 | MongoDB collection queries |

---

## MCP Tools (`GaMcpServer`)

Source: `GaMcpServer/Tools/`

| Name | Type | Layer | File | Method | Notes |
|---|---|---|---|---|---|
| `AskChatbot` | MCP Tool | 5 | `GaMcpServer/Tools/ChatTool.cs` | POST `/api/chatbot/chat` | Routes question to chatbot; grounded answer + agent metadata |
| `EvalGaScript` | MCP Tool | 2 | `GaMcpServer/Tools/GaScriptTool.cs` | POST `/api/ga/eval` | Evaluates `ga { }` computation expression via FSI; dev-only |
| `ListGaClosures` (via GaScriptTool) | MCP Tool | 2 | `GaMcpServer/Tools/GaScriptTool.cs` | GET `/api/ga/closures` | Lists registered closures with category filter |
| `GaParseChord` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.parseChord`) | Parse chord symbol → JSON structure |
| `GaChordIntervals` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.chordIntervals`) | Return interval names for a chord |
| `GaTransposeChord` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.transposeChord`) | Transpose chord by N semitones |
| `GaDiatonicChords` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.diatonicChords`) | 7 diatonic triads for a key |
| `GaRelativeKey` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.relativeKey`) | Relative major/minor key |
| `GaAnalyzeProgression` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.analyzeProgression`) | Infer key + Roman numeral labels |
| `GaCommonTones` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.commonTones`) | Shared notes between two chords |
| `GaChordSubstitutions` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`domain.chordSubstitutions`) | Diatonic subs + tritone sub |
| `GaSearchTabs` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (`tab.fetch`) | Search Archive.org + GitHub for tabs |
| `GaInvokeClosure` | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process (any permitted closure) | Generic closure invocation; `io.*` and `agent.*` blocked |
| `GaListClosures` (via GaDslTool) | MCP Tool | 2 | `GaMcpServer/Tools/GaDslTool.cs` | In-process registry | Lists all closures with input schema |
| `GetAllKeys` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | All key names |
| `GetMajorKeys` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Major keys |
| `GetMinorKeys` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Minor keys |
| `GetKeyByRootAndMode` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Key lookup by root + mode |
| `GetKeyNotes` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Notes in a key |
| `GetKeyPitchClasses` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Pitch classes for a key |
| `GetKeyAccidentals` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Accidentals in a key |
| `GetKeySignatures` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Key signatures |
| `GetKeySignatureInfo` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Detailed key signature info |
| `GetKeysByAccidentalCount` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Keys by accidental count |
| `GetKeysByAccidentalKind` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Keys by sharp/flat |
| `GetRelativeKey` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Relative key |
| `GetParallelKey` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Parallel key |
| `GetNeighboringKeys` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Circle-of-fifths neighbors |
| `GetCircleOfFifthsPosition` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Position on circle of fifths |
| `GetScaleDegrees` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Scale degree names |
| `IsNoteInKey` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Note membership test |
| `CompareKeys` | MCP Tool | 1 | `GaMcpServer/Tools/KeyTools.cs` | In-process | Compare two keys |
| `GetSetClasses` | MCP Tool | 1 | `GaMcpServer/Tools/AtonalTool.cs` | In-process | All pitch-class set classes |
| `GetModalSetClasses` | MCP Tool | 1 | `GaMcpServer/Tools/AtonalTool.cs` | In-process | Modal set classes |
| `GetSetClassSubs` (GaSetClassSubs) | MCP Tool | 1 | `GaMcpServer/Tools/AtonalTool.cs` | In-process | Set class substitutions |
| `GetCardinalities` | MCP Tool | 1 | `GaMcpServer/Tools/AtonalTool.cs` | In-process | Set class cardinalities |
| `GetIcvNeighbors` (GaIcvNeighbors) | MCP Tool | 1 | `GaMcpServer/Tools/AtonalTool.cs` | In-process | ICV-based neighbors |
| `GetModalFamilyInfo` | MCP Tool | 1 | `GaMcpServer/Tools/AtonalTool.cs` | In-process | Modal family info |
| `GaChordToSet` | MCP Tool | 1 | `GaMcpServer/Tools/ChordAtonalTool.cs` | In-process | Chord → pitch-class set |
| `GaPolychord` | MCP Tool | 1 | `GaMcpServer/Tools/ChordAtonalTool.cs` | In-process | Polychord analysis |
| `GetAvailableModes` | MCP Tool | 2 | `GaMcpServer/Tools/ModeTool.cs` | In-process | All mode names from config |
| `GetModeInfo` | MCP Tool | 2 | `GaMcpServer/Tools/ModeTool.cs` | In-process | Mode details |
| `GetAvailableInstruments` | MCP Tool | 2 | `GaMcpServer/Tools/InstrumentTool.cs` | In-process | Instruments from config |
| `GetAvailableTunings` | MCP Tool | 2 | `GaMcpServer/Tools/InstrumentTool.cs` | In-process | Tunings from config |
| `GetTuning` | MCP Tool | 2 | `GaMcpServer/Tools/InstrumentTool.cs` | In-process | Specific tuning details |
| `Echo` | MCP Tool | — | `GaMcpServer/Tools/EchoTool.cs` | In-process | Diagnostic echo |
| `ReverseEcho` | MCP Tool | — | `GaMcpServer/Tools/EchoTool.cs` | In-process | Diagnostic reverse echo |

---

## GA DSL Closures (`GA.Business.DSL`)

Source: `Common/GA.Business.DSL/Closures/BuiltinClosures/`

### domain.* (DomainClosures.fs)

| Closure Name | Category | Notes |
|---|---|---|
| `domain.parseChord` | Domain | Parse chord symbol → JSON structure |
| `domain.transposeChord` | Domain | Transpose chord by N semitones |
| `domain.diatonicChords` | Domain | 7 diatonic triads for a root + scale |
| `domain.chordIntervals` | Domain | Interval names for a chord symbol |
| `domain.relativeKey` | Domain | Relative major/minor key |
| `domain.analyzeProgression` | Domain | Key inference + Roman numeral labels |
| `domain.queryChords` | Domain | Filter diatonic chords by quality/interval/degree |
| `domain.projectChord` | Domain | Project selected fields from a chord |
| `domain.commonTones` | Domain | Common tones between two chords |
| `domain.chordSubstitutions` | Domain | Diatonic substitutions + tritone sub |

### tab.* (TabClosures.fs)

| Closure Name | Category | Notes |
|---|---|---|
| `tab.parseAscii` | Domain | Parse ASCII tab → JSON summary |
| `tab.parseVexTab` | Domain | Parse + validate + re-emit canonical VexTab |
| `tab.generateChord` | Domain | Generate minimal VexTab scaffold for a chord |
| `tab.sources` | Domain | List configured free tab data sources |
| `tab.fetch` | Domain | Search Archive.org + GitHub for tabs |
| `tab.fetchUrl` | Domain | Fetch raw tab content from a URL |

### agent.* (AgentClosures.fs)

| Closure Name | Category | Notes |
|---|---|---|
| `agent.theoryAgent` | Agent | Route music theory question → GA chatbot API (SemanticRouter → TheoryAgent) |
| `agent.tabAgent` | Agent | Route tab request → GA chatbot API (SemanticRouter → TabAgent) |
| `agent.criticAgent` | Agent | Route critique request → GA chatbot API (SemanticRouter → CriticAgent) |
| `agent.fanOut` | Agent | Route same question to multiple agents in parallel |

> Layer note: `AgentClosures.fs` lives in `GA.Business.DSL` (Layer 2) but makes outbound HTTP calls to the Layer 5 GA API. This is deferred architectural violation A-1 full.

### io.* (IoClosures.fs)

| Closure Name | Category | Notes |
|---|---|---|
| `io.readFile` | Io | Read file from disk |
| `io.writeFile` | Io | Write string to file (create/overwrite) |
| `io.httpPost` | Io | POST JSON to an HTTP endpoint |
| `io.httpGet` | Io | GET an HTTP endpoint |

> `io.*` and `agent.*` closures are blocked from unauthenticated MCP access via `GaDslTool.IsPermittedForMcp`.

### pipeline.* / surface.* (PipelineClosures.fs)

| Closure Name | Category | Notes |
|---|---|---|
| `pipeline.pullBspRooms` | Pipeline | Fetch BSP room descriptors from GA API |
| `pipeline.embedOpticK` | Pipeline | Compute 216-dim OPTIC-K embeddings |
| `pipeline.storeQdrant` | Pipeline | Upsert documents into Qdrant |
| `pipeline.reportFailures` | Pipeline | Log pipeline failures to structured sink |
| `surface.transpile` | Pipeline | Parse GA surface-syntax script → F# `ga { }` computation expression |

---

## Agents (`GA.Business.ML`)

Source: `Common/GA.Business.ML/Agents/`

| Name | Type | Layer | Notes |
|---|---|---|---|
| `GuitarAlchemistAgentBase` | Agent Base | 4 | Base class with `IChatClient`, `ProcessAsync`, `ParseStructuredResponse` |
| `SemanticRouter` | Agent | 4 | Routes to best agent via embeddings → LLM → keyword fallback; supports `AggregateAsync` fan-out |
| `TheoryAgent` | Agent | 4 | Music theory questions |
| `TabAgent` | Agent | 4 | Tab generation requests |
| `TechniqueAgent` | Agent | 4 | Guitar technique questions |
| `ComposerAgent` | Agent | 4 | Composition and arrangement |
| `CriticAgent` | Agent | 4 | Harmonic critique and quality pass |

---

## Compound Engineering Agents (`.agent/agents/`)

| Name | File | Purpose |
|---|---|---|
| `codebase-documenter` | `.agent/agents/codebase-documenter.md` | End-of-cycle documentation: snapshots, service inventory, cycle summary |
| `compound-researcher` | `.agent/agents/compound-researcher.md` | Research and analysis subagent |
| `fsharp-architect` | `.agent/agents/fsharp-architect.md` | F# architecture review |
| `grammar-governor` | `.agent/agents/grammar-governor.md` | Code quality and pattern enforcement |
