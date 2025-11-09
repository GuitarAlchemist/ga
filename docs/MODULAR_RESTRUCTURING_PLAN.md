# Modular Restructuring Plan

## Overview

The Guitar Alchemist project is transitioning from a monolithic `GA.Business.Core` library to a modular, layered architecture that separates concerns and enables independent development and testing.

## Current State (Monolithic)

```
GA.Business.Core/
├── Chords/
├── Scales/
├── Fretboard/
├── Analysis/
├── AI/
└── Orchestration/
```

**Problems**:
- All code in single assembly
- Circular dependencies possible
- Difficult to test individual domains
- Hard to maintain separation of concerns

## Target State (Modular)

```
Layer 1: Core
└── GA.Business.Core/
    ├── Note, Interval, PitchClass
    ├── Enums and primitives
    └── Fundamental types

Layer 2: Domain
├── GA.Business.Core.Harmony/
│   ├── Chords
│   ├── Scales
│   └── Progressions
└── GA.Business.Core.Fretboard/
    ├── Fretboard geometry
    ├── Fingering analysis
    └── Biomechanics

Layer 3: Analysis
└── GA.Business.Core.Analysis/
    ├── Spectral analysis
    ├── Dynamical systems
    └── Topological analysis

Layer 4: AI/ML
└── GA.Business.Core.AI/
    ├── Semantic indexing
    ├── Vector search
    ├── LLM integration
    └── Style learning

Layer 5: Orchestration
└── GA.Business.Core.Orchestration/
    ├── Workflows
    ├── Agent coordination
    └── High-level services
```

## Dependency Rules

**Golden Rule**: Each layer can ONLY depend on layers below it.

```
Layer 5 (Orchestration)
    ↓ depends on
Layer 4 (AI/ML)
    ↓ depends on
Layer 3 (Analysis)
    ↓ depends on
Layer 2 (Domain)
    ↓ depends on
Layer 1 (Core)
```

**Violations to Avoid**:
- ❌ Layer 1 depending on Layer 2+
- ❌ Layer 2 depending on Layer 3+
- ❌ Circular dependencies between layers
- ❌ Cross-layer dependencies (e.g., Layer 3 → Layer 5)

## Migration Strategy

### Phase 1: Create New Assemblies (Current)
- ✅ Create GA.Business.Core.Harmony
- ✅ Create GA.Business.Core.Fretboard
- ✅ Create GA.Business.Core.Analysis
- ✅ Create GA.Business.Core.AI
- ✅ Create GA.Business.Core.Orchestration

### Phase 2: Move Code (In Progress)
- Move chord-related code to GA.Business.Core.Harmony
- Move fretboard code to GA.Business.Core.Fretboard
- Move analysis code to GA.Business.Core.Analysis
- Move AI code to GA.Business.Core.AI
- Move orchestration code to GA.Business.Core.Orchestration

### Phase 3: Update References
- Update all project references
- Fix namespace imports
- Resolve circular dependencies
- Update tests

### Phase 4: Cleanup
- Remove moved code from GA.Business.Core
- Verify all tests pass
- Update documentation

## Code Location Guidelines

### AI Code
**Location**: `GA.Business.Core.AI`

**Includes**:
- Semantic indexing services
- Vector search implementations
- Ollama integration
- LLM services
- Embedding generation
- Style learning algorithms

**NOT in GA.Business.Core**:
- ❌ SemanticSearchService
- ❌ EmbeddingService
- ❌ VectorSearchStrategy

### Orchestration Code
**Location**: `GA.Business.Core.Orchestration`

**Includes**:
- IntelligentBSPGenerator
- ProgressionOptimizer
- High-level workflows
- Agent coordination
- Service orchestration

**NOT in GA.BSP.Core**:
- ❌ IntelligentBSPGenerator (belongs in Orchestration)
- ❌ Progression optimization (belongs in Orchestration)

## Testing Strategy

Each layer should have corresponding test projects:
- `Tests/Common/GA.Business.Core.Tests/`
- `Tests/Common/GA.Business.Core.Harmony.Tests/`
- `Tests/Common/GA.Business.Core.Fretboard.Tests/`
- `Tests/Common/GA.Business.Core.Analysis.Tests/`
- `Tests/Common/GA.Business.Core.AI.Tests/`
- `Tests/Common/GA.Business.Core.Orchestration.Tests/`

## Benefits

1. **Separation of Concerns**: Each layer has clear responsibility
2. **Testability**: Easier to unit test individual layers
3. **Reusability**: Layers can be used independently
4. **Maintainability**: Easier to understand and modify code
5. **Scalability**: Can add new layers without affecting existing ones
6. **Performance**: Can optimize each layer independently

