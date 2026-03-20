# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build & Run
```powershell
# Full solution build (.NET 10, all targets)
dotnet build AllProjects.slnx -c Debug

# Start all services via Aspire (MongoDB, GaApi, Chatbot, dashboards)
pwsh Scripts/start-all.ps1 -Dashboard

# Daily dev restart without rebuild
pwsh Scripts/start-all.ps1 -NoBuild -Dashboard

# One-time setup (install prereqs, restore NuGet/npm, build)
pwsh Scripts/setup-dev-environment.ps1
```

### Testing
```powershell
# Full test suite
dotnet test AllProjects.slnx
pwsh Scripts/run-all-tests.ps1

# Backend only (faster)
pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild

# Playwright E2E only
pwsh Scripts/run-all-tests.ps1 -PlaywrightOnly

# Single test class
dotnet test --filter "FullyQualifiedName~GrothendieckServiceTests"

# Single test method
dotnet test --filter "FullyQualifiedName~GrothendieckServiceTests.ComputeICV.ShouldComputeICV_ForCMajorScale"
```

### Code Quality
```powershell
# Format (run before committing; pre-commit hook enforces this)
dotnet format AllProjects.slnx

# Verify format without changing files (used in CI)
dotnet format AllProjects.slnx --verify-no-changes

# Install pre-commit hook (enforces format + build on each commit)
pwsh Scripts/install-git-hooks.ps1
```

### Frontend
```bash
# Sandbox development server (in ReactComponents/ga-react-components)
npm ci && npm run dev

# Production build
npm run build

# Lint
npm run lint
```

### Verification (required before claiming success)
```powershell
dotnet build AllProjects.slnx -c Debug   # Build passes
dotnet test AllProjects.slnx             # Tests pass
```
```bash
npm run build   # Frontend builds (in ReactComponents/ga-react-components)
npm run lint    # Frontend lint clean
```

### Monitoring
- Aspire dashboard: `https://localhost:15001`
- Jaeger tracing: `http://localhost:16686`
- Mongo Express: `http://localhost:8081`
- Health endpoint: `https://localhost:7001/health`

## Architecture

### Five-Layer Dependency Model

The codebase enforces a strict bottom-up dependency graph. Each layer may only depend on layers below it.

| Layer | Project(s) | Purpose |
|---|---|---|
| **1 – Core** | `GA.Core`, `GA.Domain.Core` | Pure domain primitives: Note, Interval, PitchClass, Fretboard types |
| **2 – Domain** | `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core` | Business logic, YAML configuration, BSP geometry |
| **3 – Analysis** | `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard` | Chord/scale analysis, voice leading, spectral/topological analysis |
| **4 – AI/ML** | `GA.Business.ML` | Semantic indexing, Ollama/ONNX embeddings, vector search, Spectral RAG, tab solving |
| **5 – Orchestration** | `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence` | High-level workflows, IntelligentBSPGenerator, curation |

**Rule**: AI code belongs in `GA.Business.ML`. Orchestration code (e.g., `IntelligentBSPGenerator`) belongs in `GA.Business.Core.Orchestration`, not in low-level libraries.

### Applications (`Apps/`)

| App | Tech | Role |
|---|---|---|
| `ga-server/GaApi` | ASP.NET Core, SignalR, HotChocolate (GraphQL) | Main REST + GraphQL API, chatbot hub, vector search |
| `GaChatbot` | .NET | Guitar Alchemist Chatbot (Ollama-powered RAG) |
| `ga-client` | React 18, Vite, MUI, React Three Fiber | React frontend with 3D fretboard visualization |
| `GaMusicTheoryLsp` | F# | Language Server Protocol for the music theory DSL |
| `FloorManager` | Blazor | BSP room viewer |
| `AllProjects.AppHost` | .NET Aspire | Orchestrates all services for local dev |

### Key Cross-Cutting Modules

- **`GA.Business.DSL`** (F#): Music theory DSL – parsers (VexTab, GuitarPro, MIDI, MusicXML, AsciiTab), generators, LSP. Uses FParsec.
- **`GA.Business.Config`** (F#/YAML): Static domain data – scales, instruments, voicings, tunings. Loaded via YamlDotNet with PascalCase naming convention.
- **`GaMcpServer`** / **`mcp-servers/`**: MCP server tools. Update manifests here when adding new agent integrations.
- **`ReactComponents/ga-react-components`**: Shared React component library; production bundle consumed by `Apps/ga-client`.

### OPTIC-K Embedding Schema (v1.6)
216-dimension musical embedding vector used throughout the RAG pipeline:
- **STRUCTURE** (dims 6–29, weight 0.45): Pitch-class invariants – primary driver of musical similarity.
- **MORPHOLOGY** (dims 30–53, weight 0.25): Physical fretboard geometry.
- **CONTEXT** (dims 54–65, weight 0.20): Harmonic functionality and voice motion.
- **SYMBOLIC** (dims 66–77, weight 0.10): Manual tags.
- Canonical implementation: `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs`.
- Never change the dimension count without a coordinated full-system re-index.

### Vector Search Stack
- **ONNX Runtime** + `all-MiniLM-L6-v2` (384-dim text embeddings), or OPTIC-K (216-dim musical embeddings).
- Backends: `InMemoryVectorIndex`, `QdrantVectorIndex`, `MongoDbVectorSearchStrategy`, `ILGPUVectorSearchStrategy` (CUDA).
- Document IDs must be deterministic: `entity_type_discriminator` (never random UUIDs).

## C# Standards (C# 14 / .NET 10)

- **Target**: .NET 10.0, C# 14. `<Nullable>enable</Nullable>` is mandatory in every project.
- **Records**: Use `record` for DTOs, domain events, and value objects. Prefer `init` over `set`.
- **Primary constructors**: Use for DI and state initialization (eliminates boilerplate).
- **Collection expressions**: Use `[]` everywhere. Suppress `IDE0300`/`IDE0301`/`IDE0305` warnings by converting. Never write `new List<T> { }` or `new T[] { }`.
- **Spread operator**: `[..first, ..second, extra]`.
- **Namespaces**: File-scoped (`namespace GA.Foo;`). `using` directives go *inside* the namespace.
- **Expression bodies**: Use `=>` for any single-expression method, property, or constructor.
- **Lock**: Use `System.Threading.Lock` instead of `object` for synchronization.
- **Zero warnings** policy: fix all warnings in any file you touch (especially `IDE0022`, `IDE0021`, `CA1822`).
- **Annotations**: Keep `[PublicAPI]` and `[Pure]` from JetBrains. Remove `[NotNull]`/`[CanBeNull]` (use `?` suffix instead). Use `System.Diagnostics.CodeAnalysis` for flow analysis (`[NotNullWhen]`, `[DoesNotReturn]`, etc.).
- **Railway-Oriented Programming (ROP)**: Service methods must never throw. Return `Result<T, TError>`, `Try<T>`, `Validation<T>`, or `Option<T>` from `GA.Core.Functional` instead. `throw` is only permitted at system boundaries (controllers converting to HTTP status codes, CLI entry points). See `.agent/skills/rop-patterns/SKILL.md` for the decision tree and code patterns.

## F# Standards

- **Projects**: `GA.Business.DSL`, `GA.Business.Config`.
- Indent 4 spaces. `camelCase` for values/functions, `PascalCase` for types/modules.
- Use `[<CLIMutable>]` on records for YAML deserialization or C# interop.
- YAML keys must be **PascalCase**; deserialize with `PascalCaseNamingConvention.Instance`.
- Config loaders must always provide a hardcoded fallback if the YAML file is missing.
- Bridge async: use `Async.StartAsTask` (F#) or `FSharpAsync.StartAsTask` (C#). C# must sanitize inputs before calling F#.

## Frontend Standards

- React 18 functional components only, TypeScript strict mode, Vite build.
- UI: Material UI v5 — use `sx` prop or `styled()`, never inline `style` with hardcoded hex/px.
- 3D: React Three Fiber / Three.js — never put state updates or heavy logic inside `useFrame`.
- State: React Context or Zustand. No Redux unless unavoidable.
- No `any` in TypeScript — use `unknown` then narrow.
- Components: `src/components/<Feature>/<ComponentName>.tsx`, named exports.
- Custom hooks: `src/hooks/use<Feature>.ts`.

## Agent Skills

Consult the skill files in `.agent/skills/` when performing relevant tasks:

| Skill | Path | When to Use |
|---|---|---|
| C# Coding Standards | `.agent/skills/csharp-coding-standards/SKILL.md` | Any C# file edit |
| F# & Config Architecture | `.agent/skills/fsharp-csharp-bridge/SKILL.md` | F# or YAML work |
| Music Theory Validator | `.agent/skills/music-theory-validator/SKILL.md` | Adding chords, scales, intervals |
| OPTIC-K Schema Guardian | `.agent/skills/optic-k-schema-guardian/SKILL.md` | Embedding pipeline changes |
| Semantic RAG Architecture | `.agent/skills/semantic-rag-architecture/SKILL.md` | Vector search / RAG work |
| React Frontend Engineering | `.agent/skills/react-frontend-engineering/SKILL.md` | React / 3D work |
| Systematic Debugging | `.agent/skills/systematic-debugging/SKILL.md` | Any bug or test failure |
| Verification Before Completion | `.agent/skills/verification-before-completion/SKILL.md` | Before any success claim |
| Annotations Guidance | `.agent/skills/annotations-guidance/SKILL.md` | Nullability annotations |
| ROP Patterns | `.agent/skills/rop-patterns/SKILL.md` | Service-layer error handling (Result/Try/Option/Validation) |
| Feature Implementor | `.agent/skills/feature-implementor/SKILL.md` | Running `/feature` for any new feature, fix, or multi-file refactor |
| GA Developer Tools | `.agent/skills/ga/SKILL.md` | Dispatcher for all GA sub-commands: chords, eval, probe, chat |
| GA Chords | `.agent/skills/ga/chords/SKILL.md` | Parse chords, transpose progressions, get diatonic sets via real GA services |
| GA Eval | `.agent/skills/ga/eval/SKILL.md` | Run GAL scripts against the live FSI session; explore domain closures interactively |
| GA Probe | `.agent/skills/ga/probe/SKILL.md` | Talk to chatbot subagents, probe routing decisions, compare agent responses |
| GA Chat | `.agent/skills/ga/chat/SKILL.md` | Build, index data, and run the chatbot locally; diagnose chat endpoint issues |

## Planning & Backlog

All active planning lives in two places:

1. **`BACKLOG.md`** (repo root) — entry point for future ideas. One bullet per idea. When an idea is ready to build, run `/feature` to create a brainstorm + plan, then remove it from BACKLOG.md.

2. **`docs/plans/`** — authoritative per-feature record. One file per feature. Status: `active | completed`. Filename convention: `YYYY-MM-DD-<type>-<name>-plan.md`. Created by the `/feature` skill (brainstorm → plan → PR).

3. **`/feature` skill** — the workflow to move an idea from backlog → brainstorm → plan → PR. See `.agent/skills/feature-implementor/SKILL.md`.

4. **`docs/archive/`** — historical artifacts (conductor tracks, Nov 2025 Roadmap). Read-only reference. See `docs/archive/README.md` for contents.

## Testing Conventions

- Framework: NUnit (`Tests/GA.Business.Core.Tests/`), xUnit integration (`Tests/AllProjects.AppHost.Tests/`), Playwright E2E (`Tests/GuitarAlchemistChatbot.Tests.Playwright/`).
- Test method naming: `ShouldDoSomething_WhenCondition` or `MethodName_State_ExpectedBehavior`.
- Mocking: NSubstitute or Moq.
- Aspire health checks must remain green; add coverage for MongoDB, Semantic Kernel, and MCP integrations when they change.

## Commit Conventions

- Conventional Commits: `feat:`, `fix:`, `chore:`, `refactor:`, etc. Optional scope: `feat(ga-api): ...`.
- PR description must include: impact summary, linked issues (`Fixes #123`), key command output (`dotnet test`, `npm run build`), and UI captures for frontend changes.
- Pre-commit hook (`pwsh Scripts/install-git-hooks.ps1`) enforces `dotnet format --verify-no-changes` and a solution build.


<!-- BEGIN DEMERZEL GOVERNANCE -->
# Demerzel Governance Integration

This repo participates in the Demerzel governance framework.

## Governance Framework

All agents in this repo are governed by the Demerzel constitutional hierarchy:

- **Root constitution:** governance/demerzel/constitutions/asimov.constitution.md (Articles 0-5: Laws of Robotics + LawZero principles)
- **Governance coordinator:** Demerzel (see governance/demerzel/constitutions/demerzel-mandate.md)
- **Operational ethics:** governance/demerzel/constitutions/default.constitution.md (Articles 1-11)
- **Harm taxonomy:** governance/demerzel/constitutions/harm-taxonomy.md

## Policy Compliance

Agents must comply with all Demerzel policies (18 policies):

- **Alignment:** Verify actions serve user intent (confidence thresholds: 0.9 autonomous, 0.7 with note, 0.5 confirm, 0.3 escalate)
- **Rollback:** Revert failed changes automatically; pause autonomous changes after automatic rollback
- **Self-modification:** Never modify constitutional articles, disable audit logging, or remove safety checks
- **Kaizen:** Follow PDCA cycle for improvements; classify as reactive/proactive/innovative before acting
- **Reconnaissance:** Respond to Demerzel's reconnaissance requests with belief snapshots and compliance reports
- **Scientific objectivity:** Tag evidence as empirical/inferential/subjective; generator/estimator accountability
- **Streeling:** Accept knowledge transfers from Seldon; report comprehension via belief state assessment
- **Governance audit:** Support three-level validation (schema, cross-reference, full governance)
- **Autonomous loop:** Follow Ralph Loop governance with graduated oversight
- **Multi-model orchestration:** Coordinate with external models (ChatGPT, NotebookLM, Gemini, Codex, Jules) per policy
- **Context management:** Know when to stay, split, clear, or delegate context
- **Auto-remediation:** Demerzel auto-fixes low-risk gaps, escalates high-risk to human
- **ML feedback:** ix ML pipelines provide calibration recommendations to Demerzel
- **Belief currency:** Staleness detection, decay rules, and refresh triggers for all belief states
- **Proto-conscience:** Self-aware governance with discomfort signals, regret tracking, and anticipatory ethics
- **Conscience observability:** Trend tracking, weekly reports, growth milestones for conscience evolution
- **Intuition:** Compressed experience as fast pattern recognition — candidate→tested→trusted lifecycle
- **Governance experimentation:** Hypothesis-driven policy experiments with rollback safety

## Galactic Protocol

This repo communicates with Demerzel via the Galactic Protocol:

- **Inbound (from Demerzel):** Governance directives, knowledge packages
- **Outbound (to Demerzel):** Compliance reports, belief snapshots, learning outcomes
- **Message formats:** See governance/demerzel/schemas/contracts/

## Belief State Persistence

This repo maintains a `governance/state/` directory for belief persistence:

- `governance/state/beliefs/` — Tetravalent belief states (*.belief.json)
- `governance/state/pdca/` — PDCA cycle tracking (*.pdca.json)
- `governance/state/knowledge/` — Knowledge transfer records (*.knowledge.json)
- `governance/state/snapshots/` — Belief snapshots for reconnaissance (*.snapshot.json)

File naming: `{date}-{short-description}.{type}.json`

## Agent Requirements

Every persona in this repo must include:

- `affordances` — Explicit list of permitted actions
- `goal_directedness` — One of: none, task-scoped, session-scoped
- `estimator_pairing` — Neutral evaluator persona (typically skeptical-auditor)
- All fields required by governance/demerzel/schemas/persona.schema.json

## Agent Personas

See `governance/personas/` for governed agent persona definitions.
<!-- END DEMERZEL GOVERNANCE -->
