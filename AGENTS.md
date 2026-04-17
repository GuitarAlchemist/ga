# CLAUDE.md

Guitar Alchemist — .NET 10 / C# 14 solution with F# DSL layer, React frontend, and Aspire orchestration.

## Build & Run

```powershell
dotnet build AllProjects.slnx -c Debug          # full build
pwsh Scripts/start-all.ps1 -Dashboard           # start all services via Aspire
pwsh Scripts/start-all.ps1 -NoBuild -Dashboard  # daily dev restart
```

## Test

```powershell
dotnet test AllProjects.slnx                    # full suite
pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild   # backend only (faster)
dotnet test --filter "FullyQualifiedName~<name>"          # single test
```

## Verify before claiming success

`dotnet build AllProjects.slnx -c Debug` and `dotnet test AllProjects.slnx` must pass. Frontend: `npm run build && npm run lint` in `ReactComponents/ga-react-components`.

## Architecture

Five-layer strict bottom-up dependency model:

1. **Core** — `GA.Core`, `GA.Domain.Core` (pure primitives: Note, Interval, Fretboard)
2. **Domain** — `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core` (logic, YAML, BSP)
3. **Analysis** — `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard` (chord/scale, voice leading, spectral)
4. **AI/ML** — `GA.Business.ML` (embeddings, vector search, RAG, OPTIC-K schema)
5. **Orchestration** — `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence`

**Rule**: AI code in layer 4. Orchestration in layer 5. Never in lower layers.

Apps live in `Apps/`: `ga-server/GaApi` (ASP.NET + SignalR + GraphQL), `GaChatbot`, `ga-client` (React + R3F), `GaMusicTheoryLsp` (F#), `AllProjects.AppHost` (Aspire).

## Conventions

- **C# 14 / .NET 10**: `<Nullable>enable</Nullable>`, file-scoped namespaces with `using` inside, `record` for DTOs, primary constructors, `[]` collection expressions, `System.Threading.Lock`, zero-warnings policy.
- **Railway-Oriented Programming**: Services return `Result<T,E>` / `Try<T>` / `Validation<T>` / `Option<T>` from `GA.Core.Functional`. `throw` only at system boundaries.
- **F#** (`GA.Business.DSL`, `GA.Business.Config`): 4-space indent, `[<CLIMutable>]` on YAML records, PascalCase YAML keys, hardcoded fallback if YAML missing.
- **Frontend**: React 18, TypeScript strict, MUI v5 (`sx` prop only), R3F (no state in `useFrame`), no `any`.

## OPTIC-K

228-dim musical embedding (v1.7). Canonical schema: `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs`. Never change dimension without coordinated re-index.

## Planning & Commits

- Ideas: `BACKLOG.md` → `/feature` skill → `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md`.
- Archive: `docs/archive/`.
- Commits: Conventional (`feat:`, `fix:`, etc.). PR includes impact, `Fixes #N`, key test output, UI captures.
- Pre-commit hook (`pwsh Scripts/install-git-hooks.ps1`) enforces `dotnet format` and build.

For detailed C#/F#/Frontend standards, consult `.agent/skills/` (auto-discovered by Claude Code). For governance, use `demerzel-*` skills.
