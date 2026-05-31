---
title: "refactor: Domain project structure and GA.Domain.Core quality cleanup"
type: refactor
status: completed
date: 2026-03-04
origin: docs/brainstorms/2026-03-04-domain-project-structure-cleanup-brainstorm.md
---

# refactor: Domain project structure and GA.Domain.Core quality cleanup

## Overview

A moderate cleanup pass across the Guitar Alchemist solution focused on two goals:
1. **Remove dead weight** — delete empty skeleton projects, mark parked projects, clean up commented-out project references and excluded files
2. **Improve GA.Domain.Core quality** — seal 13 unsealed types, split two large files, add missing XML docs to `Design/`, update stale architecture READMEs

This is not a layer restructure. No code moves between layers. All changes are in git and fully reversible.

**Driver:** Onboarding/readability — `Common/` is hard to navigate for new contributors.
*(see brainstorm: docs/brainstorms/2026-03-04-domain-project-structure-cleanup-brainstorm.md)*

---

## Problem Statement

The `Common/` layer currently has:
- **2 empty skeleton projects** (`GA.Business.Core.AI`, `GA.Business.Core.Generated`) with no compiled output
- **3 fully-parked projects** (`GA.Business.Configuration`, `GA.Business.Analytics`, `GA.Business.Personalization`) — all files excluded, no signal to contributor that this is intentional
- **~6 commented-out `<ProjectReference>` lines** across two `.csproj` files, implying half-done refactors
- **29 `<Compile Remove>` entries** in `GaApi.csproj`, some redundant or undocumented
- **2 stale READMEs** describing a DDD layer model that doesn't match the actual five-layer model
- **13 public types in `GA.Domain.Core`** without the `sealed` keyword (violates codebase convention)
- **2 files >750 lines** in `GA.Domain.Core` (`Interval.cs`, `PitchClassSet.cs`) that mix concerns

---

## Proposed Solution

Work in five sequential phases, each independently buildable and committable.

---

## Implementation Phases

### Phase 1: Delete Empty Skeleton Projects

**Files to delete:**

```
Common/GA.Business.Core.AI/          ← no .csproj, no source files — directory only
Common/GA.Business.Core.Generated/   ← has .fsproj + StaticCollectionGenerator.fs
                                        but entire implementation is in (* ... *) block comments
```

**Steps:**
1. Delete folder `Common/GA.Business.Core.AI/` entirely
2. Delete folder `Common/GA.Business.Core.Generated/` entirely
3. Remove their entries from `AllProjects.slnx`
4. Run `dotnet build AllProjects.slnx` to confirm build passes

**Acceptance criteria:**
- [x] `Common/GA.Business.Core.AI/` folder does not exist
- [x] `Common/GA.Business.Core.Generated/` folder does not exist
- [x] Solution builds cleanly after removal
- [x] No other project references these two projects (verify with grep)

---

### Phase 2: Mark Parked Projects and Clean Commented References

#### 2a. Mark parked projects in `AllProjects.slnx`

Move these three projects into a `<!-- PARKED -->` XML comment group in the solution file:

```
Common/GA.Business.Configuration/    ← all source files excluded
Common/GA.Business.Analytics/        ← all service files excluded
Common/GA.Business.Personalization/  ← all files excluded
```

In `AllProjects.slnx`, wrap each with an XML comment explaining their status:

```xml
<!-- PARKED: All source files are excluded from compilation. Code preserved for future reference. -->
<Project Path="Common/GA.Business.Configuration/GA.Business.Configuration.csproj"/>
```

Consolidate all three parked projects under a `<Folder Name="/Parked/">` in the solution if that folder doesn't exist.

#### 2b. Remove commented-out `<ProjectReference>` entries

**File:** `Apps/ga-server/GaApi/GaApi.csproj` (lines 82–99)

Remove:
```xml
<!-- TODO: Create this project when needed -->
<!-- <ProjectReference Include="..\..\..\Common\GA.Business.Fretboard\GA.Business.Fretboard.csproj"/> -->

<!-- TODO: Create this project when needed -->
<!-- <ProjectReference Include="..\..\..\Common\GA.Business.Orchestration\GA.Business.Orchestration.csproj"/> -->
```

**File:** `Common/GA.Business.Intelligence/GA.Business.Intelligence.csproj` (lines 21–28)

Remove:
```xml
<!-- TODO: Create these projects when needed -->
<!-- <ProjectReference Include="..\GA.Business.Analysis\GA.Business.Analysis.csproj"/> -->
<!-- <ProjectReference Include="..\GA.Business.Fretboard\GA.Business.Fretboard.csproj"/> -->
<!-- <ProjectReference Include="..\GA.Business.Harmony\GA.Business.Harmony.csproj"/> -->
```

> **Note:** These are labeled "TODO: Create when needed" but are pure noise — if the projects are needed, they'll be created and referenced at that time. Keeping commented-out dead refs misleads contributors about what's planned.

**Acceptance criteria:**
- [x] Parked projects not in AllProjects.slnx at all — no change needed
- [x] No commented-out `<ProjectReference>` lines remain in `GaApi.csproj`
- [x] No commented-out `<ProjectReference>` lines remain in `GA.Business.Intelligence.csproj`
- [x] Solution builds cleanly

---

### Phase 3: GaApi Excluded Controllers Audit

**File:** `Apps/ga-server/GaApi/GaApi.csproj`

Currently has 29 `<Compile Remove="...">` entries. Audit and clean:

**Keep with documented reason (already have inline comment):**
- `CUDA/**/*.cs` — keep, CUDA comment is clear
- `_Parked\**` — keep, `_Parked/` directory is self-documenting
- `Models/ApiResponse.cs`, `Models/ErrorResponse.cs`, `Models/PaginationInfo.cs`, `Models/PaginationRequest.cs`, `Models/SearchRequest.cs` — keep, comment "now in AllProjects.ServiceDefaults" is clear

**Audit each remaining excluded controller/service:**
For each entry, check if the file still exists in the filesystem:
- If file **does not exist**: remove the `<Compile Remove>` entry (nothing to exclude)
- If file **exists** and has an active counterpart: delete the file and remove the entry
- If file **exists** and is genuinely parked: move it to `_Parked/` (already excluded via `_Parked\**`) and remove its individual `<Compile Remove>` entry

Files to evaluate individually:
```
Controllers/AdvancedAnalyticsController.cs
Controllers/AdvancedAIController.cs
Controllers/EnhancedPersonalizationController.cs
Controllers/InvariantsController.cs
Controllers/IntelligentBSPController.cs
Controllers/BiomechanicsController.cs
Controllers/GuitarAgentTasksController.cs
Controllers/AdaptiveAIController.cs
GraphQL/Queries/MusicHierarchyQuery.cs
Hubs/ConfigurationUpdateHub.cs
Services/MongoVectorSearchIndexes.cs
Extensions/BSPServiceExtensions.cs
Extensions/HealthCheckServiceExtensions.cs
GraphQL/Types/FretboardChordAnalysisType.cs
GraphQL/Queries/FretboardQuery.cs
Controllers/Api/FretboardController.cs
```

**Acceptance criteria:**
- [x] Every remaining `<Compile Remove>` entry has either an inline reason comment or the file is in `_Parked/`
- [x] No orphaned `<Compile Remove>` entries for non-existent files
- [x] Solution builds cleanly

---

### Phase 4: Seal 13 Unsealed Types in GA.Domain.Core

Add the `sealed` keyword to 13 public types that have no inheritance hierarchies. All are in `Common/GA.Domain.Core/`.

| # | Type | File | Current Declaration |
|---|------|------|---------------------|
| 1 | `Chord` | `Theory/Harmony/Chord.cs:17` | `public class Chord : IEquatable<Chord>` |
| 2 | `ChordFormula` | `Theory/Harmony/ChordFormula.cs:15` | `public class ChordFormula : IEquatable<ChordFormula>` |
| 3 | `ChordFormulaInterval` | `Theory/Harmony/ChordFormulaInterval.cs:12` | `public class ChordFormulaInterval(...)` |
| 4 | `ChordVoicing` | `Theory/Harmony/ChordVoicing.cs:11` | `public class ChordVoicing(ChordTemplate chordTemplate, ...)` |
| 5 | `ModalFamily` | `Theory/Atonal/ModalFamily.cs:15` | `public class ModalFamily : IStaticReadonlyCollection<ModalFamily>` |
| 6 | `IntervalStructure` | `Theory/Structures/IntervalStructure.cs:15` | `public class IntervalStructure : IParsable<IntervalStructure>, ...` |
| 7 | `PitchClassSetIdEquivalences` | `Theory/Atonal/PitchClassSetIdEquivalences.cs:6` | `public class PitchClassSetIdEquivalences` |
| 8 | `Scale` | `Theory/Tonal/Scales/Scale.cs:28` | `public class Scale : IStaticReadonlyCollection<Scale>, ...` |
| 9 | `ScaleNameById` | `Theory/Tonal/Scales/ScaleNameById.cs:6` | `public class ScaleNameById() : LazyIndexerBase<...>` |
| 10 | `ModeFormula` | `Primitives/Formulas/ModeFormula.cs:13` | `public class ModeFormula(ScaleMode mode) : IReadOnlyCollection<...>` |
| 11 | `PositionLocationSet` | `Instruments/Positions/PositionLocationSet.cs:3` | `public class PositionLocationSet(IEnumerable<PositionLocation> positions)` |
| 12 | `FretVector` | `Instruments/Primitives/FretVector.cs:7` | `public class FretVector : IReadOnlyCollection<Fret>, ...` |
| 13 | `Tuning` | `Instruments/Tuning.cs:18` | `public class Tuning : IIndexer<Str, Pitch>` |

**For each type:**
1. Add `sealed` before `class`
2. Run `dotnet build` — verify no subclass compilation errors
3. Fix any `CA1852` warnings for free

> Do NOT seal attribute classes (`DomainInvariantAttribute`, `DomainRelationshipAttribute`) — these are intentionally inheritable.

**Acceptance criteria:**
- [x] All 13 types are sealed
- [x] Zero new compilation errors introduced
- [x] `dotnet build AllProjects.slnx` passes with zero warnings in touched files

---

### Phase 5: Split Large Files in GA.Domain.Core

#### 5a. Split `Interval.cs` (784 lines) using `partial record`

`Interval.cs` is declared as `abstract partial record` — the `partial` keyword already anticipates this split.

**Current structure (in one file):**
```
Interval.cs
├── abstract record Interval (base, ~40 lines)
├── #region Chromatic → sealed record Interval.Chromatic (~200 lines)
└── #region Diatonic
    ├── abstract record Interval.Diatonic (~50 lines)
    ├── sealed record Interval.Diatonic.Simple (~250 lines)
    └── sealed record Interval.Diatonic.Compound (~200 lines)
```

**Target structure (3 files):**
```
Primitives/Intervals/
├── Interval.cs                      ← base abstract partial record + IComparable (~60 lines)
├── Interval.Chromatic.cs            ← partial record Interval { sealed record Chromatic }
└── Interval.Diatonic.cs             ← partial record Interval { abstract record Diatonic,
                                        sealed record Simple, sealed record Compound }
```

**Steps:**
1. Create `Interval.Chromatic.cs` — move the `#region Chromatic` block there, wrap in `partial record Interval`
2. Create `Interval.Diatonic.cs` — move the `#region Diatonic` block there, wrap in `partial record Interval`
3. Remove the `#region` blocks from `Interval.cs`, leaving only the base declaration and `IComparable` implementation
4. Ensure `using` directives are present in each new file
5. Run `dotnet build` — verify no compilation errors

#### 5b. Extract static catalog from `PitchClassSet.cs` (765 lines)

`PitchClassSet` is already `sealed`. The large size comes from mixing instance logic with a large static catalog and factory methods.

**Extract to `PitchClassSetCatalog.cs`:**
- The `private static readonly Lazy<ILookup<IntervalClassVector, PitchClassSet>> _lazyIntervalClassVectorGroup` field
- Any static factory methods and static `Items` collection logic
- Leave instance logic (constructors, set operations, comparisons) in `PitchClassSet.cs`

**Steps:**
1. Create `Common/GA.Domain.Core/Theory/Atonal/PitchClassSetCatalog.cs`
2. Move static catalog members into `internal static class PitchClassSetCatalog`
3. Update `PitchClassSet` to reference `PitchClassSetCatalog` where needed
4. Run `dotnet build`

**Acceptance criteria:**
- [x] `Interval.cs` is < 100 lines (base only) — 70 lines ✓
- [x] `Interval.Chromatic.cs`, `Interval.Diatonic.cs`, `Interval.Diatonic.Simple.cs`, `Interval.Diatonic.Compound.cs` each < 300 lines ✓
- [x] `PitchClassSet.cs` extraction skipped — investigation revealed the 765 lines are rich algorithm code with detailed XML docs (not a static catalog). The `AllPitchClassSets` inner class is only 10 lines and generates dynamically. Extraction would not meaningfully reduce size and would break cohesion.
- [x] Build passes with zero errors

---

### Phase 6: XML Docs and Biomechanics Comment

#### 6a. Add `<remarks>` to `Design/` folder types

These public types have no remarks explaining their purpose:

| Type | File | Add remarks explaining |
|------|------|------------------------|
| `DomainVocabulary` | `Design/Schema/DomainVocabulary.cs` | Provides named musical vocabulary constants used in domain attribute annotations |
| `InvariantInfo` | `Design/Schema/InvariantInfo.cs` | Captures metadata about a domain invariant (rule + predicate expression) |
| `RelationshipInfo` | `Design/Schema/RelationshipInfo.cs` | Captures metadata about a structural relationship between domain types |
| `TypeSchemaInfo` | `Design/Schema/TypeSchemaInfo.cs` | Aggregates all schema metadata for a domain type (invariants + relationships) |

Format:
```csharp
/// <summary>...</summary>
/// <remarks>
/// Used by the type schema reflection system to surface domain rules and relationships
/// without coupling the domain model to infrastructure concerns.
/// </remarks>
```

#### 6b. Add Biomechanics location comment

In `Common/GA.Domain.Core/Instruments/Biomechanics/` add a file-level comment to the primary file (e.g., `HandModel.cs`):

```csharp
// Note: This module models physical hand/finger biomechanics for ergonomic analysis.
// It is a pragmatic inclusion in GA.Domain.Core for proximity to instrument types,
// but is not a music-theory primitive. Candidate for future move to GA.Domain.Services.
```

**Acceptance criteria:**
- [x] All four `Design/Schema` types have `<remarks>` blocks
- [x] `HandModel.cs` has the biomechanics location comment
- [x] Fixed IDE0304 (ImmutableList.Create → collection expressions) in HandModel.cs
- [x] Zero new build warnings (`dotnet build GA.Domain.Core` → 0 warnings, 0 errors)

---

### Phase 7: Update Stale Architecture READMEs

Both READMEs exist but describe an outdated DDD model inconsistent with CLAUDE.md.

#### `Common/README.md`

**Current:** Describes GA.Domain.Repositories, GA.Application, GA.Infrastructure, GA.Infrastructure — none of which exist in the actual solution.

**Update to:** Match the five-layer model from CLAUDE.md:

```markdown
# Guitar Alchemist - Common Libraries

This directory contains all shared libraries organized by the five-layer dependency model.
Each layer may only depend on layers below it.

## Layer Map

| Layer | Project(s) | Purpose |
|---|---|---|
| **1 – Core** | `GA.Core`, `GA.Domain.Core` | Pure domain primitives: Note, Interval, PitchClass, Fretboard types |
| **2 – Domain** | `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core` | Business logic, YAML configuration, BSP geometry |
| **3 – Analysis** | `GA.Domain.Services`, `GA.Business.DSL`, `GA.Business.Core.Analysis.Gpu` | Chord/scale analysis, voice leading, spectral/topological analysis |
| **4 – AI/ML** | `GA.Business.ML` | Semantic indexing, Ollama/ONNX embeddings, vector search, tab solving |
| **5 – Orchestration** | `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence` | High-level workflows, chatbot orchestration, curation |

## Which project do I add code to?

- **New music theory primitive** (Note variant, Interval type, Chord type) → `GA.Domain.Core`
- **Business rule or service** → `GA.Business.Core`
- **Analysis algorithm** (voice leading, chord detection) → `GA.Domain.Services`
- **AI/ML feature** (embeddings, vector search, agents) → `GA.Business.ML`
- **Orchestration workflow** (multi-step pipelines, agent coordination) → `GA.Business.Core.Orchestration`

## Parked Projects

These projects contain parked/excluded code preserved for future reference:
- `GA.Business.Configuration` — configuration watcher service
- `GA.Business.Analytics` — musical analytics services
- `GA.Business.Personalization` — user personalization services
```

#### `Common/GA.Domain.Core/README.md`

**Current:** Says "Layer 1.5" and lists projects that don't exist (`GA.Business.Services`, `GA.Business.Repositories`).

**Update to:** Correct layer number (Layer 1, alongside `GA.Core`), correct consumers, add folder guide and sealed-by-default note:

```markdown
# GA.Domain.Core

Pure domain primitives for Guitar Alchemist. This is Layer 1 in the five-layer model —
the foundation everything else depends on. No business logic, no I/O, no services.

## Folder Guide

| Folder | Contents |
|---|---|
| `Primitives/` | Note, Interval, PitchClass, Semitones, Formulas — raw musical building blocks |
| `Theory/` | Chord, Scale, Mode, Key, PitchClassSet, Harmony, Atonal structures |
| `Instruments/` | Fretboard, Tuning, Position, Voicing, Biomechanics |
| `Design/` | Domain metadata attributes (`[DomainInvariant]`, `[DomainRelationship]`) and schema reflection types |

## Key Domain Relationships

```
Note → PitchClass → Interval → ChordFormula → Chord
Note → Pitch → Octave
Scale/Mode → PitchClassSet → IntervalClassVector → ModalFamily
Tuning → Fretboard → Position → Voicing
```

## Conventions

- **Sealed by default.** All concrete types are `sealed` unless inheritance is intentional.
- **Records for value objects.** Prefer `sealed record` over `sealed class` for immutable domain types.
- **No services.** If it needs a service, it belongs in `GA.Domain.Services` (Layer 3).
- **XML docs on all public types.** Every public type needs at minimum a `<summary>`.
```

**Acceptance criteria:**
- [x] `Common/README.md` correctly describes the five-layer model and matches CLAUDE.md
- [x] `Common/GA.Domain.Core/README.md` says "Layer 1", lists correct consumers, includes folder guide
- [x] Both READMEs have a "which project do I add to" guide

---

## System-Wide Impact

- **Build:** All changes are purely additive (docs, `sealed`, file splits) or subtractive (dead code). No public API changes.
- **Tests:** Sealing types may expose subclassing in test code — run full test suite after Phase 4.
- **Downstream:** `sealed` keyword adds compiler enforcement. Any external code subclassing these 13 types will break at compile time (not runtime). Grep for subclasses before sealing each type.

---

## Acceptance Criteria

### Phase completion gates

- [x] **Phase 1:** `GA.Business.Core.AI/` and `GA.Business.Core.Generated/` folders deleted; solution builds
- [x] **Phase 2:** Parked projects not in solution file (no change needed); commented-out `<ProjectReference>` lines removed
- [x] **Phase 3:** All `<Compile Remove>` entries have documented reason or file is in `_Parked/`
- [x] **Phase 4:** All 13 unsealed types are sealed; zero new compilation errors
- [x] **Phase 5:** `Interval.cs` < 100 lines (70 lines); Interval split into 5 files; PitchClassSet skip documented
- [x] **Phase 6:** XML remarks on 4 Design/ types; Biomechanics comment added; IDE0304 fixed
- [x] **Phase 7:** Both READMEs updated to match actual five-layer model

### Final quality gates

- [ ] `dotnet build AllProjects.slnx -c Debug` — zero warnings in touched files
- [ ] `dotnet test AllProjects.slnx` — all tests pass
- [ ] A new contributor reading `Common/README.md` can identify where to add code without asking

---

## Dependencies & Risks

| Risk | Mitigation |
|---|---|
| A type in the 13 is subclassed somewhere | Grep for each type before sealing; compiler will catch it immediately |
| `partial record` split breaks Interval logic | Build after each file creation; run `dotnet test --filter Interval` |
| `PitchClassSet` static extraction breaks lazy init | Extract carefully; test with `dotnet test --filter PitchClassSet` |
| Deleting `GA.Business.Core.Generated` breaks something | Grep `GA.Business.Core.TypeProviders` in all `.csproj` files first |

---

## Out of Scope

*(see brainstorm: docs/brainstorms/2026-03-04-domain-project-structure-cleanup-brainstorm.md)*

- Moving `IntelligentBSPGenerator` to Layer 5 — complex, deferred
- Splitting God objects (`GpuVoicingSearchStrategy`, `MusicalAnalyticsService`)
- Resolving circular dependency between `GA.Business.AI` and `GA.Business.Intelligence`
- Frontend structure cleanup
- Per-layer READMEs beyond `Common/` and `GA.Domain.Core/` (already have READMEs to update)

---

## Sources & References

### Origin

- **Brainstorm:** [docs/brainstorms/2026-03-04-domain-project-structure-cleanup-brainstorm.md](../brainstorms/2026-03-04-domain-project-structure-cleanup-brainstorm.md)
  - Key decisions: (1) dead code first, docs second; (2) keep parked projects, mark with comments; (3) moderate scope, no cross-layer moves

### Internal References

- Five-layer model: `CLAUDE.md` (Architecture section)
- C# standards (sealed types): `.agent/skills/csharp-coding-standards/SKILL.md`
- Domain type patterns: `Common/GA.Domain.Core/Theory/Harmony/Chord.cs:17`
- `partial record` pattern: `Common/GA.Domain.Core/Primitives/Intervals/Interval.cs:15`
- Solution file format: `AllProjects.slnx` (XML, uses `<Folder>` and `<Project Path>`)
