# GA.Business.Core Modular Restructuring Plan (Option B)

## Executive Summary

This plan breaks down the monolithic `GA.Business.Core` into focused, cohesive modules following Domain-Driven Design and Clean Architecture principles. This will improve maintainability, testability, and reduce build times.

## Current Problems

1. **Monolithic GA.Business.Core**: Contains everything from primitives to AI to BSP generation
2. **Mixed concerns**: Domain models, business logic, infrastructure, and orchestration all mixed
3. **AI code in wrong location**: Semantic indexing and ML in GA.Business.Core instead of GA.Business.Core.AI
4. **IntelligentBSPGenerator misplaced**: High-level orchestration in low-level BSP library
5. **Circular dependencies**: GA.Business.Core.AI references GA.Business.Core which contains AI code
6. **Hard to navigate**: Difficult to find specific functionality
7. **Slow builds**: Changes in one area rebuild everything

## Proposed Module Structure

### Layer 1: Core Domain (No Dependencies)

**GA.Business.Core** - Pure domain models and primitives
```
Common/GA.Business.Core/
├── Primitives/
│   ├── Note.cs
│   ├── Interval.cs
│   ├── Pitch.cs
│   ├── PitchClass.cs
│   └── Octave.cs
├── Atonal/
│   ├── PitchClassSet.cs
│   ├── IntervalVector.cs
│   └── SetClass.cs
├── Interfaces/
│   ├── INote.cs
│   ├── IInterval.cs
│   └── IChord.cs
└── Common/
    ├── Result.cs
    ├── Option.cs
    └── ValueObject.cs
```

**Dependencies**: None (pure domain)

---

### Layer 2: Domain Modules (Depend on Core)

**GA.Business.Core.Harmony** - Chords, scales, progressions
```
Common/GA.Business.Core.Harmony/
├── Chords/
│   ├── Chord.cs
│   ├── ChordFactory.cs
│   ├── ChordQuality.cs
│   └── ChordExtension.cs
├── Scales/
│   ├── Scale.cs
│   ├── ScaleFactory.cs
│   ├── ScaleDegree.cs
│   └── Mode.cs
├── Progressions/
│   ├── ChordProgression.cs
│   ├── ProgressionPattern.cs
│   └── RomanNumeral.cs
├── VoiceLeading/
│   ├── VoiceLeadingRules.cs
│   ├── VoiceLeadingAnalyzer.cs
│   └── VoiceMovement.cs
└── Interfaces/
    ├── IChordFactory.cs
    ├── IScaleFactory.cs
    └── IProgressionAnalyzer.cs
```

**Dependencies**: GA.Business.Core

---

**GA.Business.Core.Fretboard** - Fretboard-specific logic
```
Common/GA.Business.Core.Fretboard/
├── Engine/
│   ├── Fretboard.cs
│   ├── FretboardGenerator.cs
│   └── FretboardCalculator.cs
├── Shapes/
│   ├── FretboardShape.cs
│   ├── ShapeGraph.cs
│   ├── ShapeGraphBuilder.cs
│   └── ShapePattern.cs
├── Analysis/
│   ├── FretboardChordAnalyzer.cs
│   ├── PhysicalFretboardCalculator.cs
│   ├── BiomechanicalAnalyzer.cs
│   └── VoicingAnalyzer.cs
├── Primitives/
│   ├── Position.cs
│   ├── Fingering.cs
│   └── Tuning.cs
└── Interfaces/
    ├── IFretboardGenerator.cs
    ├── IShapeGraphBuilder.cs
    └── IFretboardAnalyzer.cs
```

**Dependencies**: GA.Business.Core, GA.Business.Core.Harmony

---

**GA.Business.Core.Analysis** - Music theory analysis
```
Common/GA.Business.Core.Analysis/
├── Harmonic/
│   ├── HarmonicAnalyzer.cs
│   ├── FunctionalHarmonyAnalyzer.cs
│   └── TonalCenterDetector.cs
├── Spectral/
│   ├── SpectralGraphAnalyzer.cs
│   ├── SpectralMetrics.cs
│   └── ChordFamilyDetector.cs
├── Dynamical/
│   ├── HarmonicDynamics.cs
│   ├── AttractorDetector.cs
│   └── LimitCycleAnalyzer.cs
├── Topological/
│   ├── TopologicalAnalyzer.cs
│   ├── PersistentHomology.cs
│   └── HarmonicClusters.cs
├── InformationTheory/
│   ├── EntropyCalculator.cs
│   ├── ComplexityMeasure.cs
│   └── PredictabilityAnalyzer.cs
├── Invariants/
│   ├── InvariantChecker.cs
│   ├── InvariantRegistry.cs
│   └── InvariantAnalytics.cs
└── Interfaces/
    ├── IHarmonicAnalyzer.cs
    ├── ISpectralAnalyzer.cs
    └── IInvariantChecker.cs
```

**Dependencies**: GA.Business.Core, GA.Business.Core.Harmony, GA.Business.Core.Fretboard

---

### Layer 3: AI and ML (Depend on Domain + Analysis)

**GA.Business.Core.AI** - AI and machine learning
```
Common/GA.Business.Core.AI/
├── SemanticIndexing/
│   ├── SemanticSearchService.cs          (from GA.Business.Core)
│   ├── SemanticFretboardService.cs       (from GA.Business.Core)
│   ├── SemanticDocumentGenerator.cs      (from GA.Business.Core)
│   ├── VoicingSemanticSearchService.cs   (from GA.Business.Core)
│   └── Interfaces/
│       ├── ISemanticSearchService.cs
│       └── IEmbeddingService.cs
├── LLM/
│   ├── OllamaLlmService.cs               (from GA.Business.Core)
│   ├── LmStudioIntegrationService.cs     (already here)
│   └── Interfaces/
│       └── IOllamaLlmService.cs
├── VectorSearch/
│   ├── IRedisVectorService.cs            (from GA.Business.Core/AI)
│   └── Interfaces/
│       └── IVectorSearchService.cs
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

**Dependencies**: GA.Business.Core, GA.Business.Core.Harmony, GA.Business.Core.Fretboard, GA.Business.Core.Analysis

---

### Layer 4: Orchestration (Depend on Everything)

**GA.Business.Core.Orchestration** - High-level orchestration and workflows
```
Common/GA.Business.Core.Orchestration/
├── BSP/
│   ├── IntelligentBSPGenerator.cs        (from GA.Business.Core/BSP)
│   ├── IntelligentBSPGenerator.Optimized.cs (from GA.Business.Core/BSP)
│   ├── BspLevelOptions.cs
│   └── IntelligentBspLevel.cs
├── Learning/
│   ├── ProgressionOptimizer.cs
│   ├── LearningPathGenerator.cs
│   └── DifficultyCalculator.cs
├── Workflows/
│   ├── HarmonicAnalysisEngine.cs
│   ├── FretboardAnalysisWorkflow.cs
│   └── ChordProgressionWorkflow.cs
└── Interfaces/
    ├── IIntelligentBSPGenerator.cs
    └── IProgressionOptimizer.cs
```

**Dependencies**: GA.Business.Core, GA.Business.Core.Harmony, GA.Business.Core.Fretboard, GA.Business.Core.Analysis, GA.Business.Core.AI

**Rationale for IntelligentBSPGenerator placement**:
- Uses SpectralGraphAnalyzer (Analysis layer)
- Uses ProgressionAnalyzer (Analysis layer)
- Uses HarmonicDynamics (Analysis layer)
- Uses ProgressionOptimizer (Orchestration layer)
- Uses HarmonicAnalysisEngine (Orchestration layer)
- Orchestrates multiple domain services
- High-level business workflow
- Should be in orchestration layer, not low-level BSP library

---

### Existing Specialized Modules (Keep As-Is)

**GA.BSP.Core** - Low-level BSP algorithms (geometry, partitioning)
```
Common/GA.BSP.Core/
├── Algorithms/
│   ├── BspTree.cs
│   ├── BspNode.cs
│   └── BspPartitioner.cs
├── Geometry/
│   ├── Rectangle.cs
│   ├── Room.cs
│   └── Corridor.cs
└── Services/
    └── TonalBSPService.cs
```

**Dependencies**: GA.Business.Core (for primitives only)

**GA.Business.Core.Graphiti** - Graphiti visualization (already separated)

**GA.Business.Core.UI** - UI models and view models (already separated)

**GA.Business.Core.Web** - Web-specific models (already separated)

---

## Dependency Graph

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│  (GaApi, GuitarAlchemistChatbot, GaCLI, ga-client)          │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│              GA.Business.Core.Orchestration                  │
│  (IntelligentBSPGenerator, ProgressionOptimizer, Workflows) │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┼──────────────┬──────────────┐
        │              │               │              │
┌───────▼────────┐ ┌──▼──────────┐ ┌─▼────────────┐ │
│ GA.Business.   │ │ GA.Business.│ │ GA.Business. │ │
│ Core.AI        │ │ Core.       │ │ Core.        │ │
│                │ │ Analysis    │ │ Fretboard    │ │
└───────┬────────┘ └──┬──────────┘ └─┬────────────┘ │
        │              │               │              │
        └──────────────┼───────────────┼──────────────┘
                       │               │
                ┌──────▼───────────────▼──────┐
                │  GA.Business.Core.Harmony   │
                └──────┬──────────────────────┘
                       │
                ┌──────▼──────────┐
                │ GA.Business.Core│
                │  (Primitives)   │
                └─────────────────┘
```

---

## Migration Steps

### Step 1: Create New Project Structure

1. Create new projects:
   ```bash
   dotnet new classlib -n GA.Business.Core.Harmony -o Common/GA.Business.Core.Harmony
   dotnet new classlib -n GA.Business.Core.Fretboard -o Common/GA.Business.Core.Fretboard
   dotnet new classlib -n GA.Business.Core.Analysis -o Common/GA.Business.Core.Analysis
   dotnet new classlib -n GA.Business.Core.Orchestration -o Common/GA.Business.Core.Orchestration
   ```

2. Set target framework to net9.0 in all .csproj files

3. Add project references:
   ```xml
   <!-- GA.Business.Core.Harmony -->
   <ItemGroup>
     <ProjectReference Include="..\GA.Business.Core\GA.Business.Core.csproj" />
   </ItemGroup>

   <!-- GA.Business.Core.Fretboard -->
   <ItemGroup>
     <ProjectReference Include="..\GA.Business.Core\GA.Business.Core.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Harmony\GA.Business.Core.Harmony.csproj" />
   </ItemGroup>

   <!-- GA.Business.Core.Analysis -->
   <ItemGroup>
     <ProjectReference Include="..\GA.Business.Core\GA.Business.Core.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Harmony\GA.Business.Core.Harmony.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Fretboard\GA.Business.Core.Fretboard.csproj" />
   </ItemGroup>

   <!-- GA.Business.Core.AI -->
   <ItemGroup>
     <ProjectReference Include="..\GA.Business.Core\GA.Business.Core.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Harmony\GA.Business.Core.Harmony.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Fretboard\GA.Business.Core.Fretboard.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Analysis\GA.Business.Core.Analysis.csproj" />
   </ItemGroup>

   <!-- GA.Business.Core.Orchestration -->
   <ItemGroup>
     <ProjectReference Include="..\GA.Business.Core\GA.Business.Core.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Harmony\GA.Business.Core.Harmony.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Fretboard\GA.Business.Core.Fretboard.csproj" />
     <ProjectReference Include="..\GA.Business.Core.Analysis\GA.Business.Core.Analysis.csproj" />
     <ProjectReference Include="..\GA.Business.Core.AI\GA.Business.Core.AI.csproj" />
   </ItemGroup>
   ```

### Step 2: Move Files (Bottom-Up Approach)

**Phase 2.1: Keep Primitives in GA.Business.Core**
- Keep Primitives/, Atonal/, Interfaces/, Common/ in GA.Business.Core
- This is the foundation - no dependencies

**Phase 2.2: Move Harmony Code**
- Move Chords/, Scales/, Progressions/, VoiceLeading/ to GA.Business.Core.Harmony
- Update namespaces: `GA.Business.Core.Chords` → `GA.Business.Core.Harmony.Chords`

**Phase 2.3: Move Fretboard Code**
- Move Fretboard/ to GA.Business.Core.Fretboard
- Update namespaces: `GA.Business.Core.Fretboard` → `GA.Business.Core.Fretboard`

**Phase 2.4: Move Analysis Code**
- Move analysis-related code to GA.Business.Core.Analysis
- Create subdirectories: Harmonic/, Spectral/, Dynamical/, Topological/, InformationTheory/, Invariants/

**Phase 2.5: Move AI Code**
- Move Fretboard/SemanticIndexing/ → GA.Business.Core.AI/SemanticIndexing/
- Move AI/ → GA.Business.Core.AI/MachineLearning/, VectorSearch/, Models/
- Update namespaces

**Phase 2.6: Move Orchestration Code**
- Move BSP/IntelligentBSPGenerator.cs → GA.Business.Core.Orchestration/BSP/
- Move high-level workflow code to Orchestration/Workflows/

### Step 3: Update Namespaces

Create a namespace mapping document and update systematically:

| Old Namespace | New Namespace |
|--------------|---------------|
| `GA.Business.Core.Chords` | `GA.Business.Core.Harmony.Chords` |
| `GA.Business.Core.Scales` | `GA.Business.Core.Harmony.Scales` |
| `GA.Business.Core.Fretboard.SemanticIndexing` | `GA.Business.Core.AI.SemanticIndexing` |
| `GA.Business.Core.AI` | `GA.Business.Core.AI.MachineLearning` |
| `GA.Business.Core.BSP.IntelligentBSPGenerator` | `GA.Business.Core.Orchestration.BSP` |

### Step 4: Update Project References

Update all consuming projects (GaApi, GuitarAlchemistChatbot, etc.) to reference the new modular projects.

### Step 5: Update Tests

1. Create test projects for each new module:
   - `GA.Business.Core.Harmony.Tests`
   - `GA.Business.Core.Fretboard.Tests`
   - `GA.Business.Core.Analysis.Tests`
   - `GA.Business.Core.AI.Tests`
   - `GA.Business.Core.Orchestration.Tests`

2. Move tests from `GA.Business.Core.Tests` to appropriate test projects

3. Update test namespaces and references

### Step 6: Update Documentation

- Update architecture diagrams
- Update README files
- Update developer guides
- Document new project structure
- Update AGENTS.md with new structure

---

## Benefits

1. **Clear Separation of Concerns**: Each module has a single, well-defined responsibility
2. **Reduced Coupling**: Modules depend only on what they need
3. **Faster Builds**: Only rebuild affected modules
4. **Easier Navigation**: Find code by domain area
5. **Better Testing**: Test each module independently
6. **Clearer Dependencies**: Dependency graph is explicit and enforced
7. **AI Code Properly Located**: All AI/ML in GA.Business.Core.AI
8. **Orchestration Separated**: High-level workflows in dedicated project

---

## Timeline Estimate

- **Step 1** (Create projects): 1-2 hours
- **Step 2** (Move files): 6-8 hours
- **Step 3** (Update namespaces): 2-3 hours
- **Step 4** (Update references): 2-3 hours
- **Step 5** (Update tests): 3-4 hours
- **Step 6** (Documentation): 1-2 hours

**Total**: 15-22 hours

---

## Risk Mitigation

1. **Create feature branch**: `feature/modular-restructuring`
2. **Incremental commits**: Commit after each phase
3. **Run tests frequently**: After each file move
4. **Use git mv**: Preserve file history
5. **Automated refactoring**: Use IDE refactoring tools
6. **Pair review**: Have another developer review changes

---

## Success Criteria

- [ ] All projects build successfully
- [ ] All tests pass
- [ ] No circular dependencies
- [ ] Clean dependency graph (bottom-up)
- [ ] All AI code in GA.Business.Core.AI
- [ ] IntelligentBSPGenerator in Orchestration layer
- [ ] Documentation updated
- [ ] No warnings in build output

