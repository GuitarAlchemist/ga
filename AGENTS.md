# AGENTS.md

Guitar Alchemist — .NET 10 / C# 14 + F# DSL + React frontend, Aspire orchestrated.

> **Note:** `AGENTS.md` is auto-synced from this file by `Scripts/sync-agents-md.ps1` (also called from `.githooks/pre-commit`). Source of truth is `CLAUDE.md`. This file is auto-generated — do not edit directly.

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

## Where to find things

Progressive-disclosure map for fresh agents. Look here before grepping — most "I'll add X" reflexes already exist. Plan rationale: [docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md](docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md) item #1.

**Read first (always):**
- `CLAUDE.md` (this file) — conventions, layered architecture, AI surfaces.
- `state/digests/latest.md` — last session's cursor + in-flight + hypotheses. Written by `/digest` (or the Stop-hook fallback in `Scripts/precompact-digest.ps1`); auto-injected at session start by `Scripts/sessionstart-digest.ps1`.
- `BACKLOG.md` — H2 epics → H3 sub-sections. Top-of-queue work.

**If you're …**

| Doing | Look here |
|---|---|
| Implementing a non-trivial feature | `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md` (in flight) → `docs/archive/` (shipped) |
| Debugging a known class of bug | `docs/solutions/<category>/<date>-<topic>.md` (compounded fixes, frontmatter: `module / tags / problem_type`) |
| Touching the layered architecture | `docs/architecture/layers.md` (the 5-layer rule), `docs/architecture/audit-YYYY-MM-DD.md` (latest decisions) |
| Adding a chat / agent endpoint | `docs/architecture/chat-surfaces.md` — which path is canonical, which are dead |
| Touching OPTIC-K embeddings | `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs` (constants, never hardcode); rebuild runbook: `.claude/skills/optic-k-rebuild/SKILL.md` |
| Writing language-standard code (C# / F# / TS) | `.agent/skills/` (per-language standards), `.claude/skills/` (slash commands) |
| Coordinating with a sibling repo | `docs/contracts/*.contract.md` + `docs/contracts/*.schema.json`; sibling repos at `../ix/`, `../tars/`, `../Demerzel/`, `../sentrux/`, `../hari/` |
| Handing off to / from another agent | `state/handoffs/` via [`Scripts/antigravity-bridge.ps1`](Scripts/antigravity-bridge.ps1) (actor / branch / goal / write scope / tests / evidence / next ask) |
| Checking quality baselines | `state/quality/` (daily snapshots: chatbot-qa, voicing-analysis, readme-drift). Aggregated trend: `docs/quality/README.md` |
| Looking up an MCP / federation peer | `.mcp.json` (ix, demerzel, chrome-devtools, context7, ga, tars, notebooklm, sentrux, hari). Capability registry: `../Demerzel/schemas/capability-registry.json` |
| Updating UI tokens / colors | `DESIGN.md` (canonical YAML frontmatter) → `npm run gen:theme` in `ReactComponents/ga-react-components` → `src/theme.ts`. Pre-commit hook verifies sync. |
| Running anything | `Scripts/` (`start-all.ps1`, `install-ga-service.ps1`, `run-all-tests.ps1`, `precompact-digest.ps1`, `sessionstart-digest.ps1`); runbooks in `docs/runbooks/` (`chatbot-deploy.md`) |
| Reading governance / Galactic Protocol | `../Demerzel/` constitutions + IXQL pipelines; `demerzel-*` skills locally |

**Live everything-summary:** `https://demos.guitaralchemist.com/dev-data/manifest` aggregates BACKLOG, quality snapshots, architecture inventory, process health, recent activity, agent files, MCP servers — generated fresh on each request from `vite.config.ts` middleware. The visual dashboard wraps it at `/test` (Demos + Development tabs).

**Knowledge packages:** `governance/state/knowledge/YYYY-MM-DD-*.json` (Demerzel submodule) — Galactic-Protocol-framed lessons from completed work. Read when starting a sibling-repo integration.

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

## Agent skills

Per-repo config for the installed aihero/mattpocock engineering skills (`grill-with-docs`, `grill-me`, `to-prd`, `to-issues`, `tdd`, `improve-codebase-architecture`, `teach`), installed project-scoped into `.claude/skills/` via `npx skills@latest add mattpocock/skills --copy` (MIT; Socket/Snyk clean). Configured 2026-06-14 via `/setup-matt-pocock-skills`.

### Issue tracker

GitHub Issues on `GuitarAlchemist/ga`, via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical defaults (`needs-triage` / `needs-info` / `ready-for-agent` / `ready-for-human` / `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root (codebase itself is the five-layer model). `/grill-with-docs` grows them lazily. See `docs/agents/domain.md`.

## Tracer-bullets + vertical slices (aihero delta, 2026-06-14)

Adopted ecosystem-wide from aihero.dev. Counters AI's "build the whole thing at
once" failure mode:

- **Tracer-bullet first.** For any non-trivial feature, build the smallest
  **end-to-end** slice that touches *every* layer, test it, get feedback, then
  expand — never build layers in isolation. "Context-window constraints make the
  discipline non-negotiable."
- **Vertical, not horizontal, decomposition.** Each task/PR is a thin slice
  cutting through all integration layers (surfacing unknowns early), not a
  horizontal layer.

The aihero skills themselves (`/grill-me`, `/to-prd`, `/to-issues`, `/tdd`,
`/improve-codebase-architecture`, `/teach`) are installed project-scoped under
`.claude/skills/` (see **Agent skills** above) and complement this ecosystem's
existing brainstorming, planning-doc, test, and structural-quality machinery.
