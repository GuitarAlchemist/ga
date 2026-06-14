# Repository Guidelines

## Project Structure & Module Organization

- `AllProjects.slnx` is the umbrella entry point for restore, build, and test.
- **Core libraries** in `Common/`. The authoritative, audited inventory of
  projects and which app uses what lives in
  [architecture/apps-and-processes.md](architecture/apps-and-processes.md);
  the layered dependency model is in
  [architecture/README.md](architecture/README.md) and
  [architecture/layers.md](architecture/layers.md). Key projects:
    - `GA.Business.Core` - core domain logic (chords, scales, voicings, fretboard analysis).
    - `GA.Domain.Core` / `GA.Domain.Services` - domain primitives and services; harmony lives under `GA.Domain.Core/Theory/Harmony`, fretboard under `GA.Domain.Core/Instruments/Fretboard` and `GA.Domain.Services/Fretboard`.
    - `GA.Business.ML` - AI/ML (semantic indexing, LLM, vector search, OPTIC-K).
    - `GA.Business.AI` - AI service integration.
    - `GA.Business.Core.Orchestration` - high-level chat/agent orchestration (`ProductionOrchestrator`).
    - `GA.Business.Core.Analysis.Gpu` - GPU-accelerated analysis.
    - `GA.Business.DSL` - music-theory DSL.
    - `GA.Business.Graphiti`, `GA.Business.Intelligence`, `GA.Business.Personalization` - supporting domains.
- **Runtime apps** in `Apps/`: `ga-server/GaApi` (main API + the deployed chatbot's SignalR/REST surfaces), `GaChatbot.Api` (chatbot host), `GaChatbotCli`, `ga-client` (production React bundle). See [architecture/chat-surfaces.md](architecture/chat-surfaces.md) for which host serves which chat surface.
- **Orchestration**: `AllProjects.AppHost`, `GaCLI`, `GaMcpServer`, and experimental agents under `mcp-servers/`
- **Frontend sources**: `ReactComponents/ga-react-components`; production bundle in `Apps/ga-client`
- **Tests** mirror sources under `Tests/**`; supporting research and specs live in `docs/`, `Experiments/`, and `Specs/`

### Layered Architecture

The codebase follows a five-layer bottom-up dependency model (Core → Domain →
Analysis → AI/ML → Orchestration). The authoritative description — including
which concrete projects sit in each layer — is maintained in
[architecture/README.md](architecture/README.md#layered-architecture-recap)
and [architecture/layers.md](architecture/layers.md). Defer to those rather
than duplicating the layer→project mapping here (it drifts otherwise).

**Dependency Rule**: each layer may only depend on layers below it.

**AI Code Location**: All AI-related code (semantic indexing, Ollama services, vector search, ML systems) belongs in
`GA.Business.ML`, NOT in `GA.Business.Core`.

**Orchestration Code Location**: High-level orchestration code (e.g., `IntelligentBSPGenerator`) belongs in
`GA.Business.Core.Orchestration`, NOT in low-level libraries like `GA.BSP.Core`.

## Quick Start Commands

- `pwsh Scripts/setup-dev-environment.ps1` — install prerequisites, restore NuGet/npm, and build the solution.
- `pwsh Scripts/start-all.ps1 -Dashboard` — boot Aspire AppHost with GaApi, Chatbot, MongoDB, dashboards.
- `dotnet build AllProjects.slnx -c Debug` — compile all .NET 10 targets.
- `dotnet test AllProjects.slnx --filter TestCategory=...` — run NUnit/xUnit suites; omit the filter for a full run.
- `pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild` — quick backend regression; remove switches to include
  Playwright.
- `npm ci && npm run dev` in `ReactComponents/ga-react-components` — serve the sandbox; `npm run build` produces
  production bundles.

## Code Style Guidelines

- C#: `.editorconfig` enforces 4-space indentation, file-scoped namespaces, `PascalCase` types/methods, `camelCase`
  locals/parameters, and `var` when intent is clear; keep generated code in `Common/GA.Business.Core.Generated`.
- TypeScript/React: functional components under `src/components`, PascalCase filenames, lint with `npm run lint`, and
  reuse shared exports from `ReactComponents/`.
- Manage configuration via `appsettings.*.json` overlays and environment variables; do not commit secrets or API keys.

## Testing Guidelines

- NUnit/xUnit projects live in `Tests/**`; name files `*Tests.cs` with behavior-focused methods (e.g.,
  `ShouldComputeFretboardPositions`).
- Run `dotnet test` before pushing; `pwsh Scripts/run-all-tests.ps1 -PlaywrightOnly` covers Playwright UI regression.
- Keep Aspire health checks green and add coverage for MongoDB, Semantic Kernel, and MCP integrations whenever they
  change.

## Commit & Pull Request Guidelines

- Use Conventional Commits (`feat:`, `fix:`, `chore:`) with optional scopes (`feat(ga-api): ...`); keep messages
  imperative and focused.
- PRs summarize impact, link issues (`Fixes #123`), paste key command output (`dotnet test`, `npm run build`, relevant
  scripts), and attach UI captures when frontend changes apply.
- Install hooks via `pwsh Scripts/install-git-hooks.ps1`; pre-commit enforces `dotnet format --verify-no-changes` and a
  solution build.

## Security & Configuration Tips

- Manage secrets with `dotnet user-secrets` or environment variables; audit `docs/` and `Specs/` for accidental leaks.
- `Scripts/health-check.ps1`, Aspire (`https://localhost:15001`), Jaeger (`http://localhost:16686`), and Mongo Express (
  `http://localhost:8081`) monitor service health.
- Update manifests in `GaMcpServer/` and `mcp-servers/` plus related docs whenever new agents or integrations are
  introduced.

