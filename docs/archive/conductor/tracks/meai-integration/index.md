# MEAI Integration Track

## Overview

Adopt Microsoft Extensions for AI (MEAI) as the standard foundation for all LLM, embedding, and vector store functionality in Guitar Alchemist.

## Status: ⚠️ Needs Reconciliation

## Documents

| Document | Description |
|----------|-------------|
| [MEAI Migration Plan](../../Common/GA.Business.ML/Documentation/Architecture/MEAI_Migration_Plan.md) | Full architecture and implementation plan |

## Goals

1. **Provider Independence** - Switch between Ollama/GitHub Models/Foundry seamlessly
2. **Standard Interfaces** - Use `IChatClient`, `IEmbeddingGenerator<T>`, `IVectorStore`
3. **Agent Architecture** - Build TabAgent, TheoryAgent, TechniqueAgent, ComposerAgent, CriticAgent
4. **Partitioned RAG** - Segment knowledge by type (theory, technique, corpus, rules)

## Current Focus

Reconcile provider implementation status across this document and unblock deferred OpenAI/GitHub Models support.

## Progress

| Phase | Status |
|-------|--------|
| 1. Core Abstractions | ✅ Complete |
| 2. Provider Implementations | ⚠️ Partial (Ollama complete; OpenAI/GitHub deferred) |
| 3. Semantic Kernel Agents | ✅ Complete (5 agents + router) |
| 4. RAG Partitioning | ✅ Complete (4 domains + DSL) |
| 5. Evaluation & Benchmarks | ✅ Complete (Harness + Metrics) |

## Phase 1 Deliverables (Completed 2026-01-22)

### New Files Created

| File | Description |
|------|-------------|
| `GA.Business.ML/Embeddings/MusicalEmbeddingBridge.cs` | Bridges OPTIC-K generator to MEAI `IEmbeddingGenerator<VoicingDocument, Embedding<float>>` |
| `GA.Business.ML/Embeddings/InMemoryVectorIndex.cs` | In-memory vector index for dev/testing |
| `GA.Business.ML/Extensions/AiServiceExtensions.cs` | DI registration methods (`AddGuitarAlchemistAi()`, `AddMusicalEmbeddings()`, etc.) |

### Modified Files

| File | Change |
|------|--------|
| `GA.Business.ML/GA.Business.ML.csproj` | Added MEAI packages (v9.4.0-preview) |
| `GaChatbot/GaChatbot.csproj` | Aligned MEAI package versions |
| `GaChatbot/Services/ExtensionsAINarrator.cs` | Fixed API compatibility issues |

### Usage Example

```csharp
// In Program.cs or Startup.cs
services.AddGuitarAlchemistAi(configuration);

// Or configure individual components:
services.AddMusicalEmbeddings();
services.AddVectorIndex("qdrant", configuration);
services.AddGuitarAlchemistChatClient("ollama", configuration);
```

## Phase 2 Deliverables (Partially Complete - 2026-01-22)

### New Files Created

| File | Description |
|------|-------------|
| `GA.Business.ML/Providers/OllamaProvider.cs` | Factory methods for Ollama chat and text embedding clients |
| `GA.Business.ML/Providers/GitHubModelsProvider.cs` | Infrastructure for GitHub Models (deferred - SDK version mismatch) |
| `GA.Business.ML/Providers/HybridEmbeddingService.cs` | Combines musical (OPTIC-K) and text embeddings |

### Enhanced Extensions

| Method | Description |
|--------|-------------|
| `AddTextEmbeddings(provider, config)` | Register text embedding generator (ollama, github, openai) |
| `AddHybridEmbeddings(textProvider, config)` | Register both musical and text embedding services |

### Usage Example

```csharp
// For text embeddings (non-musical content)
services.AddTextEmbeddings("ollama", configuration);

// For hybrid (musical + text) embeddings
services.AddHybridEmbeddings("ollama", configuration);
```

### Known Issues

- **OpenAI/GitHub Models**: Deferred due to `Microsoft.Extensions.AI.OpenAI` package version mismatch with OpenAI SDK (2.2.0-beta.1 vs expected). Will re-enable when stable release is available.

## Phase 3 Deliverables (Completed - 2026-01-22)

### Agent Infrastructure

| File | Description |
|------|-------------|
| `GA.Business.ML/Agents/GuitarAlchemistAgentBase.cs` | Base class with `IChatClient` integration, structured request/response models |
| `GA.Business.ML/Agents/TabAgent.cs` | ASCII tab parsing and chord extraction |
| `GA.Business.ML/Agents/TheoryAgent.cs` | Pitch class analysis, harmonic functions (OPTIC-K) |
| `GA.Business.ML/Agents/SpecializedAgents.cs` | TechniqueAgent, ComposerAgent, CriticAgent |
| `GA.Business.ML/Agents/SemanticRouter.cs` | Embedding-based routing with multi-agent aggregation |

### Usage Example

```csharp
// Register all agents and the semantic router
services.AddGuitarAlchemistAgents();

// Or use the full stack (embeddings + agents)
services.AddGuitarAlchemistFullStack(configuration);

// Route and process a request
var router = serviceProvider.GetRequiredService<SemanticRouter>();
var response = await router.ProcessAsync(new AgentRequest { Query = "Analyze this C major chord" });
```


## Phase 4 Deliverables (Completed - 2026-01-22)

### Partitioned RAG System

| File | Description |
|------|-------------|
| GA.Business.ML/Rag/RagModels.cs | Models for StructuredMusicalQuery, RagResult, and PartitionedRagResponse |
| GA.Business.ML/Rag/PartitionedRagService.cs | Orchestration across Theory, Technique, Corpus, and Rules |
| GA.Business.ML/Extensions/AiServiceExtensions.cs | Added AddPartitionedRag() registration |

### Musical DSL & Query Parsing

- `ParseStructuredQuery(query)`: Extracts chords (e.g., Cmaj7), scales (Mixolydian), and techniques (Sweep)
- Automates partition selection based on detected musical intent.

## Phase 5 Deliverables (Completed - 2026-01-22)

### Evaluation & Infrastructure

| File | Description |
|------|-------------|
| GA.Business.ML/Rag/RagEvaluationModels.cs | Models for benchmark results, test cases, and aggregate metrics |
| GA.Business.ML/Rag/RagEvaluationService.cs | Performance benchmarking and retrieval quality scoring |
| GA.Business.ML.Tests/Integration/PartitionedRagServiceTests.cs | Automated benchmark suite for RAG performance |

### Metrics Tracking

- **Latency**: Average response time per partition
- **Recall**: Keyword-based result validation
- **Distribution**: Balance of knowledge types in results

