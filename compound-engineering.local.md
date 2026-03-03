---
review_agents:
  - skill: security-sentinel
  - skill: performance-oracle
  - skill: architecture-strategist
  - skill: code-simplicity-reviewer
---

# Guitar Alchemist — Compound Engineering Config

## Project Context

See [Conductor Index](conductor/index.md) for full product context, tech stack, and track registry.

## Tech Stack

- **Runtime:** .NET 9 / C# 12 / F# 9
- **Orchestration:** .NET Aspire (`AllProjects.AppHost`)
- **API Gateway:** YARP Reverse Proxy (`GaApi`)
- **Domain Service:** `GA.MusicTheory.Service` (Port 7001)
- **AI/ML:** Microsoft.SemanticKernel v1.38.0, Microsoft Extensions for AI (MEAI), Ollama (local LLM)
- **Database:** MongoDB (`guitar-alchemist` db), Redis (caching/HybridCache), FalkorDB (graph)
- **Frontend:** React 18 + Vite + MUI + Jotai + Three.js
- **Testing:** NUnit + xUnit, Playwright (E2E)

## Commands

### Build & Test

```powershell
# Run all backend tests
pwsh Scripts/run-all-tests.ps1 -BackendOnly

# Run a single project's tests
dotnet test Tests/Common/GA.Business.ML.Tests/

# Build to check for errors/warnings
dotnet build AllProjects.slnx
```

### Start the Platform

```powershell
pwsh Scripts/start-all.ps1 -Dashboard
# Aspire Dashboard: http://localhost:18888
# Frontend: http://localhost:5173
# API Gateway: https://localhost:7000
```

### Lint / Quality Gate

```powershell
# Build is the lint check — treat warnings as errors
dotnet build AllProjects.slnx -warnaserror
```

## Coding Conventions

- Use **primary constructors** for services/controllers (C# 12)
- Use **collection expressions** `[...]` instead of `new List<T>()`
- Use **nullable reference types** (`#nullable enable`) — no null suppression without justification
- Follow **Clean Architecture** layering: Domain → Business → Service → API
- All controllers return `ApiResponse<T>` — never raw types
- Secrets via `dotnet user-secrets` — **never commit secrets**
- Conventional commits: `feat(scope): description`, `fix(scope): description`, `refactor(scope): description`

## Active Tracks

See [conductor/tracks.md](conductor/tracks.md) for the full registry.

Key active tracks to drive with `/ce:plan`:

| Track | Status | Notes |
|---|---|---|
| `spectral-rag-chatbot` | Active | Semantic search + ChatBot integration |
| `modernization` | Active | Microservices migration remaining items |
| `semantic-event-routing` | Proposed | Run `/ce:brainstorm` first |
| `core-schema-design` | Needs Reconciliation | Run `/ce:plan` to resolve |
| `meai-integration` | Needs Reconciliation | MEAI + SemanticKernel alignment |

## Workflow

```
Plan → Work → Review → Compound → Repeat
```

1. **`/ce:plan <feature>`** — Creates `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md`
2. **`/ce:work docs/plans/<plan>.md`** — Executes the plan with TodoWrite tracking
3. **`/ce:review`** — Runs configured `review_agents` from frontmatter above
4. **`/ce:compound`** — Captures learnings into `docs/solutions/` for future reuse

## Key Patterns to Follow

- **Controller pattern:** See `GA.MusicTheory.Service/Controllers/` for ApiResponse<T> usage
- **Service pattern:** See `GA.MusicTheory.Service/Services/MonadicServiceBase.cs`
- **Embedding pattern:** All RAG documents extend `RagDocumentBase` — see `GA.Business.Core/Domain/`
- **OPTIC-K schema:** See `.agent/skills/optic-k-schema-guardian/` for embedding dimension spec
