# Repository Guidelines

## Project Structure & Module Organization
- `AllProjects.sln` is the umbrella entry point for restore, build, and test.
- **Core libraries** in `Common/` organized by domain:
  - `GA.Business.Core` - Core domain primitives (Note, Interval, PitchClass, etc.)
  - `GA.Business.Core.Harmony` - Chords, scales, progressions, voice leading (NEW - in development)
  - `GA.Business.Core.Fretboard` - Fretboard-specific logic and analysis (NEW - in development)
  - `GA.Business.Core.Analysis` - Advanced music theory analysis (spectral, dynamical, topological) (NEW - in development)
  - `GA.Business.Core.AI` - AI/ML functionality (semantic indexing, LLM, vector search, style learning)
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
- **Layer 4 (AI)**: `GA.Business.Core.AI` - AI/ML functionality
- **Layer 5 (Orchestration)**: `GA.Business.Core.Orchestration` - High-level workflows

**Dependency Rule**: Each layer can only depend on layers below it (bottom-up dependency graph).

**AI Code Location**: All AI-related code (semantic indexing, Ollama services, vector search, ML systems) belongs in `GA.Business.Core.AI`, NOT in `GA.Business.Core`.

**Orchestration Code Location**: High-level orchestration code (e.g., `IntelligentBSPGenerator`) belongs in `GA.Business.Core.Orchestration`, NOT in low-level libraries like `GA.BSP.Core`.

## Quick Start Commands
- `pwsh Scripts/setup-dev-environment.ps1` — install prerequisites, restore NuGet/npm, and build the solution.
- `pwsh Scripts/start-all.ps1 -Dashboard` — boot Aspire AppHost with GaApi, Chatbot, MongoDB, dashboards.
- `dotnet build AllProjects.sln -c Debug` — compile all .NET 9 targets.
- `dotnet test AllProjects.sln --filter TestCategory=...` — run NUnit/xUnit suites; omit the filter for a full run.
- `pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild` — quick backend regression; remove switches to include Playwright.
- `npm ci && npm run dev` in `ReactComponents/ga-react-components` — serve the sandbox; `npm run build` produces production bundles.

## Code Style Guidelines
- C#: `.editorconfig` enforces 4-space indentation, file-scoped namespaces, `PascalCase` types/methods, `camelCase` locals/parameters, and `var` when intent is clear; keep generated code in `Common/GA.Business.Core.Generated`.
- TypeScript/React: functional components under `src/components`, PascalCase filenames, lint with `npm run lint`, and reuse shared exports from `ReactComponents/`.
- Manage configuration via `appsettings.*.json` overlays and environment variables; do not commit secrets or API keys.

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

