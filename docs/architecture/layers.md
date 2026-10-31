---
title: GA five-layer architecture
status: living
related:
  - CLAUDE.md (architecture rule lives at root)
---

# Five-layer strict bottom-up dependency model

Guitar Alchemist enforces a one-way dependency chain. Lower layers MUST NOT know about higher layers.

1. **Core** — `GA.Core`, `GA.Domain.Core` (pure primitives: Note, Interval, Fretboard)
2. **Domain** — `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core` (logic, YAML, BSP)
3. **Analysis** — `GA.Domain.Services` (chord/scale analysis, voicing generation and analysis, voice leading, spectral)
4. **AI/ML** — `GA.Business.ML` (embeddings, vector search, RAG, OPTIC-K schema)
5. **Orchestration** — `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence`

**Rule (CLAUDE.md root)**: AI code in layer 4. Orchestration in layer 5. Never in lower layers.

## Musical objects, analysis, and realization

`Core` names the foundational dependency layer; `Domain` names the musical subject
matter. `GA.Domain.Core` therefore contains musical objects and their intrinsic
operations. Analysis in `GA.Domain.Services` interprets those objects; instrument
realization combines them with explicit string pitches, tuning, and positions.
An instrument model can live in Core without making every musical object depend
on standard guitar tuning. `Tuning` already represents bass, ukulele, and
seven-string guitar as well as the default guitar.

`Scale` preserves an ordered snapshot of spelled notes, including repetitions;
its first note anchors `Intervals`. `PitchClassSet` is the explicit projection to
unordered pitch-class membership, discarding spelling, order, and multiplicity.
C major and A natural minor share that set but have different tonic intervals.
The empty pitch-class set (`default(PitchClassSetId)`, value 0) remains valid for
set operations; it cannot realize a `Scale`, whose declared invariant is
`Count > 0`. A one-note scale is valid. `Scale` retains reference equality;
the snapshot does not introduce value equality or a new canonical spelling.

These distinctions also constrain precalculation keys. A pitch-class-set ID can
key a result that depends only on membership. Spelled or tonic-relative results
need the ordered note spelling and any additional tonal context the operation
uses. Instrument results also need the tuning's ordered pitches and the relevant
string, fret, and realization constraints. An interval-class vector alone is not
a unique set or tonal identity. These are boundaries to preserve when adding a
cache, rather than a request to introduce one. ML features keep their own
documented invariance contract in `GA.Business.ML`.

### Known Core dependency exceptions and bounded follow-up

The layer diagram is a target, not proof that the current assembly is I/O-free.
`GA.Domain.Core.csproj` references `GA.Business.Config`: `Common/GA.Domain.Core/Theory/Tonal/ModeCatalog.cs`
calls `ModesConfig.GetModalFamilies()` from its static initialization. That F#
module locates and reads `Modes.yaml`, supports reload, and maintains caches;
`ModeCatalog` then freezes its own snapshot. Simply removing the reference would
break compilation. `ScaleVideoUrlById` also has a filesystem fallback; its resource
packaging is tracked separately in PR #817.

The next compatible extraction should move the existing YAML-backed
`ModeCatalog` loading/composition responsibility to `GA.Business.Core`, retaining
musical metadata types and math in Core. First inventory consumers of its public
type, including assembly references, and decide compatibility before moving it.
Characterize catalog names, mode IDs, characteristic intervals, missing-file
behavior, and the existing static-snapshot behavior across config reloads. Retain
`Tests/Common/GA.Business.Core.Tests/Tonal/ModeCatalogTests.cs`, then verify the Core
project builds without the Config reference and those catalog tests still pass.
Do not replace this with a new catalog framework. Persistence (`DocumentBase`)
and other I/O exceptions require separate inventories; this scale-invariant slice
does not claim assembly-wide purity.

## Apps

Apps live in `Apps/`:

- `ga-server/GaApi` — ASP.NET + SignalR + GraphQL
- `GaChatbot` — chatbot API (port 5252)
- `ga-client` — React + R3F frontend
- `GaMusicTheoryLsp` — F# language server
- `AllProjects.AppHost` — Aspire orchestrator

## Conventions

- **C# 14 / .NET 10**: `<Nullable>enable</Nullable>`, file-scoped namespaces with `using` inside, `record` for DTOs, primary constructors, `[]` collection expressions, `System.Threading.Lock`, zero-warnings policy.
- **Railway-Oriented Programming**: services return `Result<T,E>` / `Try<T>` / `Validation<T>` / `Option<T>` from `GA.Core.Functional`. `throw` only at system boundaries.
- **F#** (`GA.Business.DSL`, `GA.Business.Config`): 4-space indent, `[<CLIMutable>]` on YAML records, PascalCase YAML keys, hardcoded fallback if YAML missing.
- **Frontend**: React 18, TypeScript strict, MUI v5 (`sx` prop only), R3F (no state in `useFrame`), no `any`.

For deeper language-specific guidance, consult `.agent/skills/` (auto-discovered by Claude Code).
