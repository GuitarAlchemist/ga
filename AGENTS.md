# Repository Guidelines

## Project Structure & Module Organization
- `AllProjects.slnx` is the umbrella entry point for restore, build, and test.
- **Core libraries** in `Common/` organized by domain:
  - `GA.Business.Core` - Core domain primitives (Note, Interval, PitchClass, etc.)
  - `GA.Business.Core.Harmony` - Chords, scales, progressions, voice leading (NEW - in development)
  - `GA.Business.Core.Fretboard` - Fretboard-specific logic and analysis (NEW - in development)
  - `GA.Business.Core.Analysis` - Advanced music theory analysis (spectral, dynamical, topological) (NEW - in development)
  - `GA.Business.ML` - AI/ML functionality (semantic indexing, LLM, vector search, style learning)
  - `GA.Business.Core.Orchestration` - High-level workflows (IntelligentBSPGenerator, progression optimization) (NEW - in development)
  - `GA.BSP.Core` - Low-level BSP algorithms and geometry
  - `GA.MusicTheory.DSL` - Music theory DSL
  - `GA.Business.Core.Generated` - Generated code
  - `GA.Business.Core.Graphiti` - Graphiti visualization
  - `GA.Business.Core.UI` - UI models and view models
  - `GA.Business.Core.Web` - Web-specific models
- **Data integrations**: `GA.Data.MongoDB`, `GA.Data.SemanticKernel.Embeddings`, `GA.Business.Querying`
- **Runtime apps** in `Apps/`: `ga-server/GaApi`, `GuitarAlchemistChatbot`, `ga-client`
- **Orchestration**: `AllProjects.AppHost`, `GaCLI`, `GaMcpServer`, and experimental agents under `mcp-servers/`
- **Frontend sources**: `ReactComponents/ga-react-components`; production bundle in `Apps/ga-client`
- **Tests** mirror sources under `Tests/**`; supporting research and specs live in `docs/`, `Experiments/`, and `Specs/`

### Modular Architecture (In Progress)
The project is transitioning from a monolithic `GA.Business.Core` to a modular architecture:
- **Layer 1 (Core)**: `GA.Business.Core` - Pure domain models and primitives
- **Layer 2 (Domain)**: `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard` - Domain-specific logic
- **Layer 3 (Analysis)**: `GA.Business.Core.Analysis` - Advanced analysis and theory
- **Layer 4 (AI)**: `GA.Business.ML` - AI/ML functionality
- **Layer 5 (Orchestration)**: `GA.Business.Core.Orchestration` - High-level workflows

**Dependency Rule**: Each layer can only depend on layers below it (bottom-up dependency graph).

**AI Code Location**: All AI-related code (semantic indexing, Ollama services, vector search, ML systems) belongs in `GA.Business.ML`, NOT in `GA.Business.Core`.

**Orchestration Code Location**: High-level orchestration code (e.g., `IntelligentBSPGenerator`) belongs in `GA.Business.Core.Orchestration`, NOT in low-level libraries like `GA.BSP.Core`.

See `docs/MODULAR_RESTRUCTURING_PLAN.md` and `docs/MODULAR_RESTRUCTURING_PROGRESS.md` for details.

- `pwsh Scripts/setup-dev-environment.ps1` — install prerequisites, restore NuGet/npm, and build the solution.
- `pwsh Scripts/start-all.ps1 -Dashboard` — boot Aspire AppHost with GaApi, Chatbot, MongoDB, dashboards.
- `dotnet build AllProjects.slnx -c Debug` — compile all .NET 10 targets.
- `dotnet test AllProjects.slnx --filter TestCategory=...` — run NUnit/xUnit suites; omit the filter for a full run.
- `pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild` — quick backend regression; remove switches to include Playwright.
- `npm ci && npm run dev` in `ReactComponents/ga-react-components` — serve the sandbox; `npm run build` produces production bundles.

- C#: See [C# Development Standards](#c-development-standards) for 2026 conventions; keep generated code in `Common/GA.Business.Core.Generated`.
- TypeScript/React: functional components under `src/components`, PascalCase filenames, lint with `npm run lint`, and reuse shared exports from `ReactComponents/`.
- Manage configuration via `appsettings.*.json` overlays and environment variables; do not commit secrets or API keys.

## C# Development Standards (2026 Edition)

### Core Principles
- **Target Framework**: .NET 10.0+ / C# 14.0.
- **Nullability**: Mandatory enabled (`<Nullable>enable</Nullable>`).
- **Immutability**: Favor `readonly` and `record` types for domain models.
- **Clarity**: Use `var` when the type is obvious from the right-hand side (e.g., `var list = new List<string>();` or `var result = GetResult();` if clear).

### Naming Conventions
- **PascalCase**: Classes, Structs, Enums, Interfaces, Methods, Properties, Constants.
- **camelCase**: Local variables, Parameters.
- **_camelCase**: Private fields (e.g., `_chordRepository`).
- **Prefixes**: Interfaces must start with `I`.

### Modern Language Features (2026 Best Practices)
1. **Primary Constructors**: Use for boilerplate-free dependency injection and state initialization.
2. **Collection Expressions `[]`**: Standardize on `[]` for all collection initializations (Arrays, Lists, Spans, and Custom Types). Avoid `new List<T>()` or `new[]`.
3. **Spread Operator `..`**: Use within collection expressions to flatten or combine: `List<int> all = [..first, ..second, 42];`.
4. **Collection Builders**: For custom collection types, use `[CollectionBuilder(typeof(T), nameof(Create))]` to enable `[]` syntax.
5. **File-Scoped Namespaces**: Use `namespace MyNamespace;` to keep indentation levels low.
6. **Using Directives**: Place **inside** the namespace declaration to avoid global scope pollution (as per `.editorconfig`).
7. **Raw String Literals `"""`**: Use for JSON, SQL, or multi-line documentation to eliminate escape characters.
8. **Expression-Bodied Members**: Use `=>` for methods, properties, and constructors where the body is a single expression (even if formatted across multiple lines).
9. **Lock Object**: Use `System.Threading.Lock` instead of `object` for synchronization.

### High-Quality 2026 Examples

#### Modern Collection Pattern (2026)
Custom types should support the `[]` syntax via the `CollectionBuilder` attribute. This enables compiler optimizations and improves readability.

```csharp
namespace GA.Domain.Core.Primitives;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using GA.Core.Collections;

[CollectionBuilder(typeof(AccidentedNoteCollection), nameof(Create))]
public sealed class AccidentedNoteCollection(IReadOnlyCollection<Note.Accidented> items) 
    : LazyPrintableCollectionBase<Note.Accidented>(items)
{
    public static AccidentedNoteCollection Create(ReadOnlySpan<Note.Accidented> items) => new([..items]);
}

// Usage:
AccidentedNoteCollection notes = [Note.Chromatic.C, Note.Chromatic.E, Note.Chromatic.G];
```

#### Modern Service Pattern
```csharp
namespace GA.Business.Core.Services;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

/// <summary>
/// Demonstrates Primary Constructors, Collection Expressions, and Spread Operator.
/// </summary>
public class HarmonicAnalysisService(IChordRepository repo, ILogger<HarmonicAnalysisService> logger)
{
    private readonly List<Chord> _processedCache = []; // Collection expression

    public async Task<AnalysisSummary> ProcessAsync(params ReadOnlySpan<Pitch> pitches)
    {
        if (pitches is []) return AnalysisSummary.Empty;

        logger.LogInformation("Analyzing {Count} pitches", pitches.Length);
        
        var matches = await repo.FindAsync([..pitches]); // Spread operator
        return new AnalysisSummary(matches, DateTime.UtcNow);
    }
}

public record AnalysisSummary(IEnumerable<Chord> Chords, DateTime Timestamp)
{
    public static readonly AnalysisSummary Empty = new([], DateTime.MinValue);
}
```

#### Advanced Pattern Matching & Property Expressions
```csharp
public string CategorizePlayer(PlayerProfile profile) => profile switch
{
    { PreferredMaxFret: > 12, SkipWeight: < 0.7 } => "Virtuoso / Jazz",
    { HandSize: HandSize.Small }                  => "Compact Ergonomics",
    _                                             => "Standard"
};
```

#### Domain Collection Pattern
For domain collections, enforce immutability, primary constructors, and collection expressions support via `[CollectionBuilder]`.

```csharp
namespace GA.Domain.Core.Primitives;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using GA.Core.Collections;
using JetBrains.Annotations;

/// <summary>
/// An ordered set of chromatic notes
/// </summary>
/// <param name="notes"></param>
[PublicAPI]
[CollectionBuilder(typeof(ChromaticNoteSet), nameof(Create))]
public sealed class ChromaticNoteSet(ImmutableSortedSet<Note.Chromatic> notes)
    : PrintableImmutableSet<Note.Chromatic>(notes)
{
    /// <summary>
    ///     Empty <see cref="ChromaticNoteSet" />
    /// </summary>
    public static readonly ChromaticNoteSet Empty = new([]);

    public static ChromaticNoteSet Create(ReadOnlySpan<Note.Chromatic> notes) => new([..notes]);
}
```

10. **Span Conversion**: Use the `.ToSpan()` extension member to simplify converting `IEnumerable<T>` to `ReadOnlySpan<T>` while avoiding unnecessary allocations if the source is already an array.

```csharp
// Simplification
public static double ShannonEntropy(IEnumerable<double> p) => ShannonEntropy(p.ToSpan());
```

## Testing Guidelines
- NUnit/xUnit projects live in `Tests/**`; name files `*Tests.cs` with behavior-focused methods (e.g., `ShouldComputeFretboardPositions`).
- Run `dotnet test` before pushing; `pwsh Scripts/run-all-tests.ps1 -PlaywrightOnly` covers Playwright UI regression.
- Keep Aspire health checks green and add coverage for MongoDB, Semantic Kernel, and MCP integrations whenever they change.

## Commit & Pull Request Guidelines
- Use Conventional Commits (`feat:`, `fix:`, `chore:`) with optional scopes (`feat(ga-api): ...`); keep messages imperative and focused.
- PRs summarize impact, link issues (`Fixes #123`), paste key command output (`dotnet test`, `npm run build`, relevant scripts), and attach UI captures when frontend changes apply.
- Install hooks via `pwsh Scripts/install-git-hooks.ps1`; pre-commit enforces `dotnet format --verify-no-changes` and a solution build.

## Security & Configuration Tips
- Manage secrets with `dotnet user-secrets` or environment variables; audit `docs/` and `Specs/` for accidental leaks.
- `Scripts/health-check.ps1`, Aspire (`https://localhost:15001`), Jaeger (`http://localhost:16686`), and Mongo Express (`http://localhost:8081`) monitor service health.
- Update manifests in `GaMcpServer/` and `mcp-servers/` plus related docs whenever new agents or integrations are introduced.

## Agent Skills
The `.agent/skills` directory contains specialized instruction sets for AI agents working on this codebase. Agents MUST consult these skills when performing relevant tasks.

- **C# Coding Standards** (`.agent/skills/csharp-coding-standards/SKILL.md`): Mandatory guidelines for C# 14/NET 10 development, including warning resolution and style enforcement.
- **F# & Configuration Architecture** (`.agent/skills/fsharp-csharp-bridge/SKILL.md`): Comprehensive guide for F# development, YAML configuration standards, and C# interoperability patterns.
- **Music Theory Validator** (`.agent/skills/music-theory-validator/SKILL.md`): Validation rules for Chords, Scales, and Intervals.
- **OPTIC-K Schema Guardian** (`.agent/skills/optic-k-schema-guardian/SKILL.md`): Rules for the OPTIC-K embedding schema.
- **React & Frontend Engineering** (`.agent/skills/react-frontend-engineering/SKILL.md`): Standards and best practices for React, TypeScript, and 3D visualization development within the Guitar Alchemist frontend.
- **Semantic Search & RAG Architecture** (`.agent/skills/semantic-rag-architecture/SKILL.md`): Standards for AI embedding generation, vector indexing strategies, and Retrieval-Augmented Generation (RAG) pipelines within Guitar Alchemist.
- **Annotations Guidance** (.agent/skills/annotations-guidance/SKILL.md): Guidelines for using JetBrains.Annotations vs native System.Diagnostics.CodeAnalysis attributes in C# 14/.NET 10+ codebases.
- **Verification Before Completion** (.agent/skills/verification-before-completion/SKILL.md): Requires running verification commands and confirming output before making any success claims.
- **Systematic Debugging** (.agent/skills/systematic-debugging/SKILL.md): Four-phase debugging methodology requiring root cause investigation before proposing fixes.
