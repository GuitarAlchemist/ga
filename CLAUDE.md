# CLAUDE.md

Guitar Alchemist — .NET 10 / C# 14 + F# DSL + React frontend, Aspire orchestrated.

> **Note:** `AGENTS.md` is auto-synced from this file by `Scripts/sync-agents-md.ps1` (also called from `.githooks/pre-commit`). Edit `CLAUDE.md`; never edit `AGENTS.md` directly.

## Build, test, verify

```powershell
dotnet build AllProjects.slnx -c Debug                    # full build
dotnet test  AllProjects.slnx                             # full suite
pwsh Scripts/start-all.ps1 -Dashboard                     # start all services via Aspire
pwsh Scripts/start-all.ps1 -NoBuild -Dashboard            # daily dev restart (skip build)
pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild    # backend-only suite (faster)
dotnet test --filter "FullyQualifiedName~<name>"           # single test
```

**Verify before claiming success.** `dotnet build AllProjects.slnx -c Debug` and `dotnet test AllProjects.slnx` must pass. Frontend: `npm run build && npm run lint` in `ReactComponents/ga-react-components`.

## Architecture

Strict bottom-up five-layer model:

1. **Core** — `GA.Core`, `GA.Domain.Core` (pure primitives: Note, Interval, Fretboard)
2. **Domain** — `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core` (logic, YAML, BSP)
3. **Analysis** — `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard` (chord/scale, voice leading, spectral)
4. **AI/ML** — `GA.Business.ML` (embeddings, vector search, RAG, OPTIC-K schema)
5. **Orchestration** — `GA.Business.Core.Orchestration`, `GA.Business.Assets`, `GA.Business.Intelligence`

**Rule: AI code in layer 4 (`GA.Business.ML`). Orchestration in layer 5. Never in lower layers.** Full layer map + conventions: [docs/architecture/layers.md](docs/architecture/layers.md).

Apps live in `Apps/`: `ga-server/GaApi` (ASP.NET + SignalR + GraphQL), `GaChatbot`, `ga-client` (React + R3F), `GaMusicTheoryLsp` (F#), `AllProjects.AppHost` (Aspire).

## Conventions

- **C# 14 / .NET 10**: `<Nullable>enable</Nullable>`, file-scoped namespaces with `using` inside, `record` for DTOs, primary constructors, `[]` collection expressions, `System.Threading.Lock`, zero-warnings policy.
- **Railway-Oriented Programming**: Services return `Result<T,E>` / `Try<T>` / `Validation<T>` / `Option<T>` from `GA.Core.Functional`. `throw` only at system boundaries.
- **F#** (`GA.Business.DSL`, `GA.Business.Config`): 4-space indent, `[<CLIMutable>]` on YAML records, PascalCase YAML keys, hardcoded fallback if YAML missing.
- **Frontend**: React 18, TypeScript strict, MUI v5 (`sx` prop only), R3F (no state in `useFrame`), no `any`.

## OPTIC-K

240-dim musical embedding (`OPTIC-K-v1.8`). Canonical schema: `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs` — read `TotalDimension` and `Version` constants, do not hardcode. v1.8 (2026-04-19) added a 12-dim `ROOT` partition at slots 228–239 to carry root pitch class outside `STRUCTURE`, closing the T-invariance gap that 91% of same-PC-set cross-instrument voicings exposed in invariant test #25. **One-way door: never change dimension without coordinated re-index.**

## Planning & commits

- Ideas → `BACKLOG.md` → `/feature` skill → `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md`. Archive: `docs/archive/`.
- Past fixes / learnings: `docs/solutions/` (YAML frontmatter: `module`, `tags`, `problem_type`).
- Commits: Conventional (`feat:`, `fix:`, etc.). PR includes impact, `Fixes #N`, key test output, UI captures.
- Pre-commit hook (`pwsh Scripts/install-git-hooks.ps1`) enforces `dotnet format` + build.
- Language standards: `.agent/skills/`. Governance: `demerzel-*` skills.

## Cross-repo contracts

GA collaborates with sibling repos via JSON-on-disk contracts (the canonical handoff pattern across the GuitarAlchemist ecosystem). Sibling clones are typically peers under the same parent directory:

- **ix** (`../ix/`, Rust ML algorithms): produces `state/voicings/optick.index` consumed by GA's RAG layer; produces SAE artifacts at `state/quality/optick-sae/<date>/optick-sae-artifact.json` per `docs/contracts/2026-05-02-optick-sae-artifact.contract.md` (schema: `docs/contracts/optick-sae-artifact.schema.json`).
- **Demerzel** (`../Demerzel/`, governance + IXQL): orchestrates the QA Architect tribunal per `docs/contracts/2026-05-02-qa-verdict.contract.md` (schema: `docs/contracts/qa-verdict.schema.json`); pipelines under `Demerzel/pipelines/*.ixql`.
- **tars** (`../tars/`, F# grammar + metacognition): cross-model theory validator.

Locked-field changes need cross-repo coordination. The `links.supersedes` pattern in `optick-sae-artifact` is how to introduce a non-breaking baseline shift without freezing the schema. Contracts marked v0.1.x in their headers are still drafts — only freeze at the explicitly named Phase 4 milestone of their respective plans.

## AI surfaces in this workspace

This repo is typically opened in **Antigravity** (a VS Code fork). That gives two AI surfaces in the same window:

- **Antigravity native AI** (bottom-right panel, Claude Opus 4.6 Thinking) — fast, ad-hoc, MCP-capped at 100 tools per instance.
- **Claude Code extension** (top-right panel, `anthropic.claude-code-2.1.126`) — multi-step, large-context, no tool cap. Reads project-level `.mcp.json`.

Plus codex/OpenAI tooling reads `AGENTS.md` (auto-synced from this file).

Split the work:

- **Antigravity native** → quick lookups, single-file edits, sketch-level brainstorming.
- **Claude Code** → multi-step plans, multi-file refactors, cross-repo work, anything that needs `/feature`, agent fanout, or the 1M context window.
- **codex CLI** → headless adversarial review, parallel-dispatch second opinions on plans.

Hand off between surfaces via [`Scripts/antigravity-bridge.ps1`](Scripts/antigravity-bridge.ps1) — drops a note in `state/handoffs/` (gitignored) that the other surface reads on next ask. Plan: [docs/plans/2026-05-05-tools-antigravity-claude-code-integration-plan.md](docs/plans/2026-05-05-tools-antigravity-claude-code-integration-plan.md). Multi-agent project context: fetch `https://demos.guitaralchemist.com/dev-data/manifest` (or `http://localhost:5176/dev-data/manifest` locally).

## Karpathy 6 Rules — AI coding discipline

Apply to every code-touching turn:

1. **Think before coding.** State your interpretation + assumptions; ask one clarifying question if ambiguous; wait for confirmation.
2. **Simplicity first.** Minimum code that solves the exact problem. No speculative features, no future-proofing.
3. **Surgical changes only.** Only modify code directly related to the request.
4. **Goal-driven execution.** Every task → verifiable success criteria. Use `/goal <condition>` (v2.1.139+) to mechanize. "Task completed" ≠ "goal achieved."
5. **Frame problem before solution.** Who is in pain, what changes for them. Check prior art first: `Common/GA.Business.Core/Analysis/**`, `docs/methodology/**`.
6. **Instrument + log one-way doors.** Metric-moving changes declare baseline + direction + guardrail (baselines under `state/quality/`). Non-trivial plans record reversibility + revisit trigger; one-way doors (OPTIC-K dims, schema changes, public APIs, pricing) require explicit sign-off.

Self-improvement reflex: when the user corrects you, invoke `/correct` so the rule lands in **Session-learned rules** below.

## Session continuity (Cherny pattern)

- `/digest` — captures session state to `state/digests/latest.md`. Auto-fallback via `Scripts/precompact-digest.ps1`; auto-injected via `Scripts/sessionstart-digest.ps1`.
- `/learnings` — captures surprises to `docs/solutions/<category>/<date>-<topic>.md`.
- `/correct` — turns user corrections into permanent rules below. Keep entries ~5 lines: Rule, Why, How to apply.

CI validation: `.github/workflows/karpathy-cherny-discipline.yml`.

## Session-learned rules

_Appended by `/correct` when the user corrects an approach. Persists across sessions._

### Stop-hook digest stomp (2026-05-16)

**Rule:** the Stop hook (`Scripts/precompact-digest.ps1`) overwrites `state/digests/latest.md` with a metadata-only stub if `/digest` wasn't recent. Re-write the rich digest as the final action of the session, or rewrite from memory if you see `trigger: stop-hook-finalize` in the frontmatter mid-session.

**Why:** observed twice 2026-05-16 (merge drive + supervised `/auto-optimize` cycle). Each stomp reverts `session_id` to `stop-finalize` and bodies to a four-line stub. Family pattern at `docs/solutions/tooling/2026-05-16-auto-optimize-oracle-silent-success-build-failure.md`.

**How to apply:** treat the modified-file system-reminder as a stomp signal, re-read, rewrite. Don't argue with the hook; fixing it is a separate concern.
