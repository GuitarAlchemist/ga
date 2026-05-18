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
3. **Analysis** — `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard` (chord/scale, voice leading, spectral)
4. **AI/ML** — `GA.Business.ML` (embeddings, vector search, RAG, OPTIC-K schema)
5. **Orchestration** — `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence`

**Rule (CLAUDE.md root)**: AI code in layer 4. Orchestration in layer 5. Never in lower layers.

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
