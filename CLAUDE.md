# CLAUDE.md

Guitar Alchemist — .NET 10 / C# 14 + F# DSL + React frontend, Aspire orchestrated.

## Build, test, verify

```powershell
dotnet build AllProjects.slnx -c Debug           # full build
dotnet test  AllProjects.slnx                    # full suite
pwsh Scripts/start-all.ps1 -Dashboard            # start all services via Aspire
pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild   # faster, backend only
```

**Verify before claiming success.** `dotnet build AllProjects.slnx -c Debug` and `dotnet test AllProjects.slnx` must pass. Frontend: `npm run build && npm run lint` in `ReactComponents/ga-react-components`.

## Architecture (breadcrumb)

Strict bottom-up five-layer model. **Rule: AI code in layer 4 (`GA.Business.ML`). Orchestration in layer 5. Never in lower layers.** Full layer map + conventions: [docs/architecture/layers.md](docs/architecture/layers.md).

## OPTIC-K

240-dim musical embedding (`OPTIC-K-v1.8`). Canonical schema: `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs` — read `TotalDimension` and `Version` constants, do not hardcode. **One-way door: never change dimension without coordinated re-index.**

## Planning & commits

- Ideas → `BACKLOG.md` → `/feature` skill → `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md`. Archive: `docs/archive/`.
- Past fixes / learnings: `docs/solutions/` (YAML frontmatter: `module`, `tags`, `problem_type`).
- Commits: Conventional (`feat:`, `fix:`, etc.). PR includes impact, `Fixes #N`, key test output, UI captures.
- Pre-commit hook (`pwsh Scripts/install-git-hooks.ps1`) enforces `dotnet format` + build.
- Language standards: `.agent/skills/`. Governance: `demerzel-*` skills.

## Cross-repo (breadcrumb)

Sibling repos: **ix** (`../ix/`, Rust ML), **Demerzel** (`../Demerzel/`, governance + IXQL), **tars** (`../tars/`, F# theory validator). Contracts at `docs/contracts/`. Drafts marked v0.1.x are NOT frozen — only freeze at the explicit Phase 4 milestone of the owning plan.

## AI surfaces (breadcrumb)

Three surfaces in this workspace: Antigravity native, Claude Code, Augment. Split is documented in [docs/methodology/ai-surfaces.md](docs/methodology/ai-surfaces.md). Hand off via `Scripts/antigravity-bridge.ps1`.

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
