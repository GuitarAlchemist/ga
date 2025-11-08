# AI Code Reorganization Plan

## Problem Statement

AI-related functionality is currently scattered across multiple projects, causing:
1. **Namespace confusion**: AI code in `GA.Business.Core` instead of `GA.Business.Core.AI`
2. **Circular dependencies**: GA.Business.Core.AI references GA.Business.Core which contains AI code
3. **Poor cohesion**: Related AI features are separated
4. **Unclear boundaries**: Hard to determine what belongs where

## Current State Analysis

### AI Code Currently in GA.Business.Core (WRONG LOCATION)

**Fretboard/SemanticIndexing/** (Should move to GA.Business.Core.AI):
- `SemanticSearchService.cs` - Vector search and semantic indexing
- `SemanticFretboardService.cs` - Semantic fretboard analysis with LLM
- `SemanticDocumentGenerator.cs` - Document generation for indexing
- `OllamaLlmService.cs` - Ollama LLM integration
- `VoicingSemanticSearchService.cs` - Semantic search for voicings

**AI/** (Should move to GA.Business.Core.AI):
- `IRedisVectorService.cs` - Redis vector search interface
- `InvariantAIService.cs` - AI-powered invariant analysis
- `StyleLearningSystem.cs` - ML-based style learning
- `StyleLearningSystem.Optimized.cs` - Optimized version
- `AdaptiveDifficultySystem.cs` - AI-driven difficulty adaptation
- `AdaptiveDifficultySystem.Optimized.cs` - Optimized version
- `PatternRecognitionSystem.Optimized.cs` - Pattern recognition ML
- `AIModels.cs` - AI model definitions

### AI Code Already in GA.Business.Core.AI (CORRECT LOCATION)

**LmStudio/**:
- `LmStudioIntegrationService.cs` - LM Studio integration

**Existing structure** (needs verification):
- Other AI services and integrations

### AI Code in GaApi (Application Layer - CORRECT)

**Services/**:
- `OllamaEmbeddingService.cs` - Ollama embedding implementation
- `VectorSearchService.cs` - MongoDB vector search
- `SemanticSearchService.cs` - Application-level semantic search
- `SemanticKnowledgeSource.cs` - Knowledge source for semantic search

## Proposed Reorganization

### Phase 1: Move Semantic Indexing to GA.Business.Core.AI

**New Structure**:
```
Common/GA.Business.Core.AI/
├── SemanticIndexing/
│   ├── SemanticSearchService.cs          (from GA.Business.Core)
│   ├── SemanticFretboardService.cs       (from GA.Business.Core)
│   ├── SemanticDocumentGenerator.cs      (from GA.Business.Core)
│   ├── VoicingSemanticSearchService.cs   (from GA.Business.Core)
│   └── Interfaces/
│       ├── ISemanticSearchService.cs     (new)
│       └── IEmbeddingService.cs          (extract interface)
├── LLM/
│   ├── OllamaLlmService.cs               (from GA.Business.Core)
│   ├── LmStudioIntegrationService.cs     (already here)
│   └── Interfaces/
│       └── IOllamaLlmService.cs          (extract interface)
├── VectorSearch/
│   ├── IRedisVectorService.cs            (from GA.Business.Core/AI)
│   └── Interfaces/
│       └── IVectorSearchService.cs       (new)
├── MachineLearning/
│   ├── StyleLearningSystem.cs            (from GA.Business.Core/AI)
│   ├── StyleLearningSystem.Optimized.cs  (from GA.Business.Core/AI)
│   ├── AdaptiveDifficultySystem.cs       (from GA.Business.Core/AI)
│   ├── AdaptiveDifficultySystem.Optimized.cs (from GA.Business.Core/AI)
│   ├── PatternRecognitionSystem.Optimized.cs (from GA.Business.Core/AI)
│   └── InvariantAIService.cs             (from GA.Business.Core/AI)
└── Models/
    └── AIModels.cs                       (from GA.Business.Core/AI)
```

### Phase 2: Update Namespaces

**Old Namespaces** → **New Namespaces**:
- `GA.Business.Core.Fretboard.SemanticIndexing` → `GA.Business.Core.AI.SemanticIndexing`
- `GA.Business.Core.AI` → `GA.Business.Core.AI.MachineLearning` (for ML-specific code)
- `GA.Business.Core.AI` → `GA.Business.Core.AI.VectorSearch` (for vector search)
- `GA.Business.Core.AI` → `GA.Business.Core.AI.LLM` (for LLM services)

### Phase 3: Extract Interfaces

Create clean interfaces to avoid circular dependencies:

**IEmbeddingService.cs**:
```csharp
namespace GA.Business.Core.AI.SemanticIndexing.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
}
```

**IOllamaLlmService.cs**:
```csharp
namespace GA.Business.Core.AI.LLM.Interfaces;

public interface IOllamaLlmService
{
    Task<string> ProcessNaturalLanguageQueryAsync(string query, string context);
    Task<bool> EnsureBestModelAvailableAsync();
    Task<string> GetBestAvailableModelAsync();
}
```

**ISemanticSearchService.cs**:
```csharp
namespace GA.Business.Core.AI.SemanticIndexing.Interfaces;

public interface ISemanticSearchService
{
    Task IndexDocumentAsync(IndexedDocument document);
    Task<List<SearchResult>> SearchAsync(string query, int maxResults = 10);
    Task<IndexStatistics> GetStatisticsAsync();
}
```

### Phase 4: Update Dependencies

**GA.Business.Core** should:
- Remove all AI-related code
- Reference `GA.Business.Core.AI` for AI interfaces only
- Use dependency injection for AI services

**GA.Business.Core.AI** should:
- Contain all AI/ML functionality
- Reference `GA.Business.Core` for domain models only
- Expose clean interfaces

**GaApi** should:
- Implement concrete AI services (OllamaEmbeddingService, etc.)
- Register AI services via extension methods
- Reference both GA.Business.Core and GA.Business.Core.AI

## Breaking GA.Business.Core into Smaller Pieces

### Current Issues with GA.Business.Core

1. **Too large**: Contains fretboard, chords, scales, progressions, analysis, etc.
2. **Mixed concerns**: Domain models, business logic, and infrastructure
3. **Hard to navigate**: Difficult to find specific functionality
4. **Tight coupling**: Changes in one area affect others

### Proposed Modular Structure

```
Common/
├── GA.Business.Core/                    (Core domain models only)
│   ├── Primitives/                      (Note, Interval, Pitch, etc.)
│   ├── Atonal/                          (PitchClass, PitchClassSet, etc.)
│   └── Interfaces/                      (Core interfaces)
│
├── GA.Business.Core.Fretboard/          (Fretboard-specific logic)
│   ├── Engine/                          (Fretboard generation)
│   ├── Shapes/                          (Shape definitions)
│   ├── Analysis/                        (Fretboard analysis)
│   └── Primitives/                      (Fretboard primitives)
│
├── GA.Business.Core.Harmony/            (Harmony and chord logic)
│   ├── Chords/                          (Chord models and factories)
│   ├── Scales/                          (Scale models and generators)
│   ├── Progressions/                    (Progression analysis)
│   └── VoiceLeading/                    (Voice leading rules)
│
├── GA.Business.Core.Analysis/           (Analysis and theory)
│   ├── Harmonic/                        (Harmonic analysis)
│   ├── Spectral/                        (Spectral analysis)
│   ├── Invariants/                      (Invariant checking)
│   └── Metrics/                         (Musical metrics)
│
├── GA.Business.Core.AI/                 (AI and ML functionality)
│   ├── SemanticIndexing/                (Semantic search)
│   ├── LLM/                             (LLM integrations)
│   ├── VectorSearch/                    (Vector search)
│   └── MachineLearning/                 (ML systems)
│
└── GA.Business.Core.Graphiti/           (Already separated)
    └── ...
```

### Benefits of Modular Structure

1. **Clear boundaries**: Each project has a single responsibility
2. **Easier navigation**: Find code by domain area
3. **Better testing**: Test each module independently
4. **Reduced coupling**: Changes are isolated
5. **Faster builds**: Only rebuild affected modules
6. **Better documentation**: Each module can have focused docs

## Implementation Steps

### Step 1: Create New Project Structure (if breaking up GA.Business.Core)

1. Create new projects:
   - `GA.Business.Core.Fretboard.csproj`
   - `GA.Business.Core.Harmony.csproj`
   - `GA.Business.Core.Analysis.csproj`

2. Update project references:
   - All new projects reference `GA.Business.Core`
   - `GA.Business.Core.AI` references `GA.Business.Core`
   - Application projects reference specific modules

### Step 2: Move AI Code to GA.Business.Core.AI

1. Create directory structure in GA.Business.Core.AI
2. Move files from GA.Business.Core to GA.Business.Core.AI:
   - `Fretboard/SemanticIndexing/*` → `SemanticIndexing/`
   - `AI/*` → `MachineLearning/`, `VectorSearch/`, `Models/`
3. Update namespaces in moved files
4. Extract interfaces where needed
5. Update all references in consuming code

### Step 3: Update Service Registration

Update extension methods to reflect new structure:

**Before**:
```csharp
// In GA.Business.Core
using GA.Business.Core.Fretboard.SemanticIndexing;
```

**After**:
```csharp
// In GA.Business.Core.AI
using GA.Business.Core.AI.SemanticIndexing;
using GA.Business.Core.AI.SemanticIndexing.Interfaces;
```

### Step 4: Update Tests

1. Move AI-related tests to GA.Business.Core.AI.Tests
2. Update test namespaces
3. Update test project references
4. Verify all tests pass

### Step 5: Update Documentation

1. Update architecture diagrams
2. Update README files
3. Update developer guides
4. Document new project structure

## Migration Checklist

- [ ] Create backup branch
- [ ] Create new directory structure in GA.Business.Core.AI
- [ ] Move SemanticIndexing files
- [ ] Move AI files
- [ ] Move LLM files
- [ ] Update all namespaces
- [ ] Extract interfaces
- [ ] Update project references
- [ ] Update using statements in all consuming code
- [ ] Update service registration extension methods
- [ ] Move and update tests
- [ ] Run full build
- [ ] Run all tests
- [ ] Update documentation
- [ ] Create PR for review

## Risk Mitigation

1. **Namespace collisions**: Use fully qualified names during transition
2. **Breaking changes**: Update all references in single commit
3. **Test failures**: Run tests after each file move
4. **Build errors**: Fix incrementally, one project at a time
5. **Merge conflicts**: Coordinate with team, avoid parallel changes

## Success Criteria

- [ ] All AI code is in GA.Business.Core.AI
- [ ] No AI code remains in GA.Business.Core
- [ ] All namespaces follow new structure
- [ ] All tests pass
- [ ] No circular dependencies
- [ ] Clean build with no warnings
- [ ] Documentation updated

