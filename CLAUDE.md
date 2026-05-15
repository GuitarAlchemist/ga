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

240-dim musical embedding (`OPTIC-K-v1.8`). Canonical schema: `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs` — read `TotalDimension` and `Version` constants, do not hardcode. v1.8 (2026-04-19) added a 12-dim `ROOT` partition at slots 228–239 to carry root pitch class outside `STRUCTURE`, closing the T-invariance gap that 91% of same-PC-set cross-instrument voicings exposed in invariant test #25. Never change dimension without coordinated re-index.

## Planning & Commits

- Ideas: `BACKLOG.md` → `/feature` skill → `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md`.
- Archive: `docs/archive/`.
- Solutions: `docs/solutions/` — documented past fixes and learnings (bugs, best practices, workflow patterns), organized by category with YAML frontmatter (`module`, `tags`, `problem_type`). Relevant when implementing or debugging in documented areas.
- Commits: Conventional (`feat:`, `fix:`, etc.). PR includes impact, `Fixes #N`, key test output, UI captures.
- Pre-commit hook (`pwsh Scripts/install-git-hooks.ps1`) enforces `dotnet format` and build.

For detailed C#/F#/Frontend standards, consult `.agent/skills/` (auto-discovered by Claude Code). For governance, use `demerzel-*` skills.

## AI surfaces in this workspace

This repo is typically opened in **Antigravity** (a VS Code fork). That gives two in-window AI surfaces, with Augment as a third option (extension, in Antigravity or any other VS Code / JetBrains / Vim host):

- **Antigravity native AI** (bottom-right panel, Claude Opus 4.6 Thinking) — fast, ad-hoc, MCP-capped at 100 tools per instance.
- **Claude Code extension** (top-right panel, `anthropic.claude-code-2.1.126`) — multi-step, large-context, no tool cap. Reads project-level `.mcp.json`.
- **Augment** (VS Code / JetBrains / Vim extension, Claude Opus 4.7) — proprietary real-time codebase index for semantic retrieval across C#/F#/TS/Rust; first-class Linear / Jira / Confluence / Notion tools without MCP setup; `sub-agent-skill-author` and `sub-agent-skill-graduator` wired for the GA chatbot `SKILL.md` lifecycle. Smaller context window than Claude Code, no `/feature` orchestration, no cross-repo fanout.

Split the work:

- **Antigravity native** → quick lookups, single-file edits, sketch-level brainstorming.
- **Claude Code** → multi-step plans, multi-file refactors, cross-repo work, anything that needs `/feature`, agent fanout, or the 1M context window.
- **Augment** → "where is X across C#/F#/TS" semantic queries via the codebase index; Linear/Jira-driven changes without standing up MCP; `skills-dev/` → `skills/` SKILL.md drafting and graduation; non-Antigravity IDE work (Rider, plain VS Code, JetBrains).

Hand off between surfaces via [`Scripts/antigravity-bridge.ps1`](Scripts/antigravity-bridge.ps1) — drops a note in `state/handoffs/` (gitignored) that the other surface reads on next ask. The bridge is documented for the Antigravity native ↔ Claude Code pair; Augment can read/write the same `state/handoffs/` directory when asked. Plan: [docs/plans/2026-05-05-tools-antigravity-claude-code-integration-plan.md](docs/plans/2026-05-05-tools-antigravity-claude-code-integration-plan.md).

## Cross-repo contracts

GA collaborates with sibling repos via JSON-on-disk contracts (the canonical handoff pattern across the GuitarAlchemist ecosystem). Sibling clones are typically peers under the same parent directory:

- **ix** (`../ix/`, Rust ML algorithms): produces `state/voicings/optick.index` consumed by GA's RAG layer; produces SAE artifacts at `state/quality/optick-sae/<date>/optick-sae-artifact.json` per `docs/contracts/2026-05-02-optick-sae-artifact.contract.md` (schema: `docs/contracts/optick-sae-artifact.schema.json`).
- **Demerzel** (`../Demerzel/`, governance + IXQL): orchestrates the QA Architect tribunal per `docs/contracts/2026-05-02-qa-verdict.contract.md` (schema: `docs/contracts/qa-verdict.schema.json`); pipelines under `Demerzel/pipelines/*.ixql`.
- **tars** (`../tars/`, F# grammar + metacognition): cross-model theory validator.

Locked-field changes need cross-repo coordination. The `links.supersedes` pattern in `optick-sae-artifact` is how to introduce a non-breaking baseline shift without freezing the schema. Contracts marked v0.1.x in their headers are still drafts — only freeze at the explicitly named Phase 4 milestone of their respective plans.

## Collaboration discipline

Drawn from Karpathy's skill + sohaibt/product-mode (merged, not installed). Apply to non-trivial work — typos and one-liners skip this.

- **Surface, don't guess.** If a request has multiple plausible interpretations, list them with tradeoffs — don't pick silently. Mark each assumption as *validated / assumed / unknown*.
- **Frame problem before solution.** State who is in pain and what changes for them before proposing code. Check prior art first: `Common/GA.Business.Core/Analysis/**`, `Common/GA.Domain.Services/**`, `docs/methodology/**`.
- **Instrument before you ship.** Metric-moving changes declare baseline + expected direction + guardrail. Baselines live in `state/quality/{embeddings,voicing-analysis,chatbot-qa}/`, aggregated by `ix-quality-trend` → `docs/quality/README.md`. Never "we'll add analytics later."
- **Log one-way doors.** Non-trivial `docs/plans/*.md` must record reversibility (one-way / two-way door) and revisit trigger (metric / date / condition). One-way doors — OPTIC-K dims/partitions, public API shapes, schema changes, pricing — require explicit sign-off.

## Karpathy 4 Rules — AI coding discipline

These rules complement (don't replace) the Collaboration discipline above. They apply to every Claude proposal that touches code:

1. **Think before coding.** State your interpretation of the request + assumptions; ask one clarifying question if anything is ambiguous; wait for confirmation before writing code.
2. **Simplicity first.** Write minimum code that solves the exact problem. No speculative features, no future-proofing.
3. **Surgical changes only.** Only modify code directly related to the request. Don't refactor adjacent code, don't fix unrelated style issues.
4. **Goal-driven execution.** Transform every task into verifiable success criteria. Loop until each is demonstrably met. "Task completed" ≠ "goal achieved." Use native `/goal <condition>` (Claude Code v2.1.139+) to mechanize this — Claude keeps working across turns until an evaluator confirms the condition holds. `/digest`'s `success_criteria` field is the **declared** form; `/goal` is the **operational** driver.

Self-improvement reflex: when the user corrects you, invoke `/correct` so the rule lands in this file's **Session-learned rules** section — Cherny's "most important loop" from the 2026 Sequoia talk.

## Session continuity (Cherny pattern)

- `/digest` — captures meaningful session state (cursor, in-flight, hypotheses, success criteria) to `state/digests/latest.md`. Auto-fallback via `Scripts/precompact-digest.ps1`; auto-injected on next session via `Scripts/sessionstart-digest.ps1`. See `.claude/skills/digest/SKILL.md`.
- `/learnings` — captures surprises (non-obvious facts worth grep-finding later) into `docs/solutions/<category>/<date>-<topic>.md`.
- `/correct` — turns user corrections into permanent rules in this CLAUDE.md.

The hooks are validated in CI by `.github/workflows/karpathy-cherny-discipline.yml`.

## Session-learned rules

_Appended by `/correct` when the user corrects an approach. Persists across sessions._

(none yet)
