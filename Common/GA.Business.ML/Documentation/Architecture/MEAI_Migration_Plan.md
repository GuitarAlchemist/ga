# Microsoft Extensions for AI (MEAI) Migration Plan

> **Date**: 2026-01-22  
> **Status**: 🔄 In Progress  
> **Reference**: [Jeremy Likness - Generative AI with LLMs in .NET and C#](https://devblogs.microsoft.com/dotnet/generative-ai-with-large-language-models-in-dotnet-and-csharp/)

---

## Executive Summary

Guitar Alchemist will adopt **Microsoft's standard AI libraries** (MEAI) as the foundation for all LLM and RAG functionality. This provides:

1. **Provider Independence** - Switch between Ollama (local), GitHub Models, or Azure Foundry without code changes
2. **Standard Interfaces** - `IChatClient`, `IEmbeddingGenerator<T>`, `IVectorStore` from Microsoft.Extensions.*
3. **Ecosystem Compatibility** - Works seamlessly with Semantic Kernel for orchestration
4. **Future-Proof** - Positioned as the .NET 2026 foundation for GenAI

---

## Current State Analysis

### What We Have ✅

| Component | Current Implementation | MEAI Alignment |
|-----------|----------------------|----------------|
| Chat Client | `OllamaChatClientAdapter` implements `IChatClient` | ✅ Already aligned |
| Embeddings | Custom `IEmbeddingGenerator` (domain-specific) | ⚠️ Needs bridge |
| Vector Index | `IVectorIndex` (file-based, Qdrant) | ⚠️ Custom interface |
| Orchestration | `SpectralRagOrchestrator` | ⚠️ Manual orchestration |

### What Needs Migration 🔄

1. **Embedding Generator Interface** - Bridge our `IEmbeddingGenerator` to MEAI's `IEmbeddingGenerator<string, Embedding<float>>`
2. **Vector Store Abstraction** - Align `IVectorIndex` with `Microsoft.Extensions.VectorData`
3. **DI Registration** - Use MEAI's `AddChatClient()`, `AddEmbeddingGenerator()` patterns

---

## Architecture Design

### The Three Stable Contracts

```
┌─────────────────────────────────────────────────────────────────┐
│                    Guitar Alchemist AI Layer                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │   IChatClient   │  │IEmbeddingGenerator│ │   IVectorStore  │ │
│  │  (MEAI)         │  │     (MEAI)       │  │    (MEAI)       │ │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘ │
│           │                    │                    │          │
│  ┌────────▼────────┐  ┌────────▼────────┐  ┌────────▼────────┐ │
│  │ OllamaClient    │  │MusicalEmbedding │  │ FileBasedIndex  │ │
│  │ (local dev)     │  │   Generator     │  │ (dev)           │ │
│  │                 │  │ (OPTIC-K)       │  │                 │ │
│  │ GitHubModels    │  │                 │  │ QdrantIndex     │ │
│  │ (cloud)         │  │ OllamaEmbedding │  │ (prod)          │ │
│  │                 │  │ (fallback)      │  │                 │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Agent Architecture (Semantic Kernel Integration)

```
┌────────────────────────────────────────────────────────────┐
│                   Semantic Kernel Host                      │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  TabAgent    │  │ TheoryAgent  │  │TechniqueAgent│      │
│  │              │  │              │  │              │      │
│  │ • Parse tabs │  │ • Pitch clss │  │ • Fingerings │      │
│  │ • Timing     │  │ • Functions  │  │ • Ergonomics │      │
│  │ • Positions  │  │ • Tonality   │  │ • Difficulty │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                            │
│  ┌──────────────┐  ┌──────────────┐                        │
│  │ComposerAgent │  │ CriticAgent  │ ◄── Aggregator         │
│  │              │  │              │                        │
│  │ • Variations │  │ • Conflicts  │ • Confidence scoring   │
│  │ • Reharm     │  │ • Score      │ • Evidence pointers    │
│  │ • Style      │  │ • Verify     │ • Fuzzy arbitration    │
│  └──────────────┘  └──────────────┘                        │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

## Implementation Plan

### Phase 1: Core Abstractions (Week 1)

#### 1.1 Create MEAI Bridge for Embeddings

**File**: `GA.Business.ML/Embeddings/MEAIEmbeddingBridge.cs`

```csharp
/// <summary>
/// Bridges our domain-specific embedding generator to MEAI's interface
/// </summary>
public class MusicalEmbeddingBridge : IEmbeddingGenerator<VoicingDocument, Embedding<float>>
{
    private readonly MusicalEmbeddingGenerator _generator;
    
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(...) { }
}
```

**AC**:
- [ ] Implements `IEmbeddingGenerator<VoicingDocument, Embedding<float>>`
- [ ] Wraps existing `MusicalEmbeddingGenerator`
- [ ] Returns MEAI `Embedding<float>` type
- [ ] Unit tests pass

#### 1.2 Create Vector Store Adapter

**File**: `GA.Business.ML/Embeddings/MEAIVectorStoreAdapter.cs`

```csharp
/// <summary>
/// Adapts IVectorIndex to Microsoft.Extensions.VectorData patterns
/// </summary>
public class VectorStoreAdapter : IVectorStore
{
    private readonly IVectorIndex _index;
    
    public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(...) { }
}
```

**AC**:
- [ ] Implements `IVectorStore` from Microsoft.Extensions.VectorData
- [ ] Wraps existing `IVectorIndex` implementations
- [ ] Supports metadata filtering
- [ ] Unit tests pass

#### 1.3 Register MEAI Services in DI

**File**: `GaChatbot/Extensions/AiServiceExtensions.cs`

```csharp
public static class AiServiceExtensions
{
    public static IServiceCollection AddGuitarAlchemistAI(this IServiceCollection services, IConfiguration config)
    {
        // Chat client - interchangeable local/cloud
        services.AddChatClient(builder => builder
            .UseOllama(config["Ollama:BaseUrl"], config["Ollama:ChatModel"])
            .UseLogging()
            .UseRetry());
            
        // Embeddings - domain-specific
        services.AddSingleton<IEmbeddingGenerator<VoicingDocument, Embedding<float>>, MusicalEmbeddingBridge>();
        
        // Vector store - swappable backend
        services.AddSingleton<IVectorStore, VectorStoreAdapter>();
        
        return services;
    }
}
```

**AC**:
- [ ] Single extension method configures all AI services
- [ ] Respects configuration for provider selection
- [ ] Logging and retry policies applied
- [ ] Integration tests pass

---

### Phase 2: Provider Implementations (Week 2)

#### 2.1 Ollama Provider (Local Development)

**Already done**: `OllamaChatClientAdapter` implements `IChatClient`

**To add**:
- [ ] `OllamaEmbeddingClient` for text embeddings (fallback for non-musical content)
- [ ] Configuration in `appsettings.Development.json`

#### 2.2 GitHub Models Provider (Cloud/CI)

**File**: `GA.Business.ML/Providers/GitHubModelsProvider.cs`

```csharp
public static IChatClient CreateGitHubModelsChatClient(string model = "gpt-4o-mini")
{
    return new ChatClientBuilder()
        .Use(new AzureAIInferenceClient(
            new Uri("https://models.inference.ai.azure.com"),
            new AzureKeyCredential(Environment.GetEnvironmentVariable("GITHUB_TOKEN"))))
        .UseLogging()
        .Build();
}
```

**AC**:
- [ ] Works with `GITHUB_TOKEN` environment variable
- [ ] Supports model selection via configuration
- [ ] Falls back gracefully if unavailable

---

### Phase 3: Semantic Kernel Agents (Week 3-4)

#### 3.1 Create Agent Base Infrastructure

**File**: `GA.Business.ML/Agents/GuitarAlchemistAgentBase.cs`

```csharp
public abstract class GuitarAlchemistAgentBase
{
    protected readonly Kernel _kernel;
    protected readonly IChatClient _chatClient;
    
    public virtual AgentResponse ProcessRequest(AgentRequest request) { }
    
    // Structured output with confidence/evidence
    public record AgentResponse(
        object Result,
        float Confidence,
        IReadOnlyList<string> Evidence,
        IReadOnlyList<string> Assumptions);
}
```

#### 3.2 Implement Specialized Agents

| Agent | Plugins | Tools |
|-------|---------|-------|
| `TabAgent` | `ParseAsciiTab`, `ExtractChords` | ASCII parser |
| `TheoryAgent` | `AnalyzePitchClasses`, `IdentifyFunction` | OPTIC-K engine |
| `TechniqueAgent` | `ValidateFingerPosition`, `SuggestAlternative` | Fretboard mapper |
| `ComposerAgent` | `Reharmonize`, `GenerateVariation` | Phase sphere |
| `CriticAgent` | `DetectContradiction`, `ScoreResponse` | Validator |

#### 3.3 Create Aggregator/Router

**File**: `GA.Business.ML/Agents/SemanticRouter.cs`

```csharp
public class SemanticRouter
{
    private readonly IVectorStore _vectorStore;
    private readonly IReadOnlyList<GuitarAlchemistAgentBase> _agents;
    
    // Route by embedding similarity to agent descriptions
    public async Task<GuitarAlchemistAgentBase> RouteAsync(string userRequest) { }
    
    // Aggregate fuzzy responses
    public async Task<AggregatedResponse> AggregateAsync(IEnumerable<AgentResponse> responses) { }
}
```

---

### Phase 4: RAG Partitioning (Week 4)

#### 4.1 Define Knowledge Types

| Type | Content | Partition |
|------|---------|-----------|
| `Theory` | Harmonic functions, substitutions | High semantic weight |
| `Technique` | Voicings, ergonomics, doigtés | High morphology weight |
| `Corpus` | Riffs, tabs, MIDI, GuitarPro | Searchable examples |
| `Rules` | OPTIC-K invariants, heuristics | Hard constraints |

#### 4.2 Implement Partitioned RAG

```csharp
public interface IPartitionedRagService
{
    Task<RagResult> QueryAsync(string query, KnowledgeType[] partitions, int topK = 5);
}
```

---

## Migration Checklist

### Phase 1 - Core ✅ (Completed 2026-01-22)
- [x] Create `MusicalEmbeddingBridge.cs` - Bridges OPTIC-K generator to `IEmbeddingGenerator<VoicingDocument, Embedding<float>>`
- [x] Create `InMemoryVectorIndex.cs` - Development/testing vector index with cosine similarity
- [x] Create `AiServiceExtensions.cs` - DI registration for all AI services (`AddGuitarAlchemistAi()`)
- [x] Add MEAI packages to `GA.Business.ML.csproj` (v9.4.0-preview)
- [x] Align MEAI package versions in `GaChatbot.csproj`
- [x] Fix existing `ExtensionsAINarrator.cs` compatibility issues
- [x] Add unit tests for `MusicalEmbeddingBridge` (9 tests passing)

### Phase 2 - Providers (Completed - 2026-01-22)
- [x] Create `OllamaProvider.cs` - Factory methods for Ollama chat and embedding clients
- [x] Create `GitHubModelsProvider.cs` - Infrastructure ready (deferred due to SDK version mismatch)
- [x] Create `HybridEmbeddingService` - Combines musical (OPTIC-K) and text embeddings
- [x] Update `AiServiceExtensions.cs` - AddTextEmbeddings(), AddHybridEmbeddings() methods
- [x] Update configuration files (appsettings.Shared.json, appsettings.Development.json)
- [x] Add integration tests (OllamaProviderIntegrationTests, HybridEmbeddingServiceIntegrationTests)
- ⏸️ OpenAI/GitHub Models integration deferred until Microsoft.Extensions.AI.OpenAI stabilizes

### Phase 3 - Agents (Completed - 2026-01-22)
- [x] Create `GuitarAlchemistAgentBase.cs` - Base class with IChatClient, AgentRequest/Response models
- [x] Implement 5 specialized agents:
  - [x] TabAgent - ASCII tab parsing and chord extraction
  - [x] TheoryAgent - Pitch class and harmonic analysis (OPTIC-K)
  - [x] TechniqueAgent - Playability and ergonomic analysis
  - [x] ComposerAgent - Reharmonization and variation generation
  - [x] CriticAgent - Quality evaluation and consistency checking
- [x] Create `SemanticRouter.cs` - Embedding-based request routing with multi-agent aggregation
- [x] Add DI registration (`AddGuitarAlchemistAgents()`, `AddGuitarAlchemistFullStack()`)
- [x] Validate OPTIC-K Schema via In-Memory RAG:
  - [x] Created `PartitionAwareRagIndex.cs` for dimension-by-dimension testing
  - [x] Verified Structure/Morphology separation (Similarity: 1.0 vs 0.63 for same chord)
  - [x] Verified weighted balancing in semantic search
- [ ] Add agent tests

### Phase 4 - RAG Partitioning
- [x] Implement `IPartitionedRagService` (PartitionedRagService.cs) ✅
- [x] Integrated specialized RAG services (Theory, Technique, Corpus, Rules) ✅
- [x] Added `ParseStructuredQuery` for musical DSL/intent detection ✅
- [x] Registered RAG services in DI (`AiServiceExtensions.cs`) ✅
- [x] Validated with `PartitionedRagServiceTests.cs` ✅

### Phase 5 - Evaluation & Benchmarking
- [x] Implement `RagEvaluationService` (Evaluation harness) ✅
- [x] Create automated benchmark suites (`PartitionedRagServiceTests.cs`) ✅
- [ ] Evaluate different LLM backends (Ollama vs GitHub Models)
- [ ] Record performance metrics (latency, tokens/sec)
- [ ] Finalize configuration presets

---

## Testing Strategy

### Test Harness Requirements

```csharp
public class ModelComparisonHarness
{
    // Same inputs across providers
    public IReadOnlyList<(string Tab, string ExpectedKey, string ExpectedStyle)> TestCases { get; }
    
    // Score by dimensions
    public ModelScore Evaluate(ModelResponse response)
    {
        return new ModelScore(
            Coherence: EvaluateCoherence(response),
            Playability: EvaluatePlayability(response),
            TheoryCorrectness: EvaluateTheory(response));
    }
}
```

---

## References

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- [Microsoft.Extensions.AI.Ollama](https://www.nuget.org/packages/Microsoft.Extensions.AI.Ollama)
- [Microsoft.Extensions.VectorData](https://www.nuget.org/packages/Microsoft.Extensions.VectorData)
- [Semantic Kernel](https://learn.microsoft.com/semantic-kernel/)
- [GitHub Models](https://github.com/marketplace/models)

---

## Appendix: Package References

```xml
<!-- Core MEAI packages -->
<PackageReference Include="Microsoft.Extensions.AI" Version="9.1.*" />
<PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="9.1.*" />
<PackageReference Include="Microsoft.Extensions.AI.AzureAIInference" Version="9.1.*" />
<PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" Version="9.1.*" />

<!-- Semantic Kernel for orchestration -->
<PackageReference Include="Microsoft.SemanticKernel" Version="1.35.*" />
```
