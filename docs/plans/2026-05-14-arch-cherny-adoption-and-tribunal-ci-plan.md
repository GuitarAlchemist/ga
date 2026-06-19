---
title: "arch: Cherny adoption + QA Architect Tribunal CI integration — cross-repo (v1 — SUPERSEDED)"
type: arch
status: superseded
superseded_by: docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md
date: 2026-05-14
origin: octo brainstorm (strategy-analyst / tdd-orchestrator / context-manager / backend-architect) + deep inventory of ix/Demerzel/tars + Cherny "Why Coding Is Solved" talk (Sequoia AI Ascent 2026)
related_plans:
  - docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md
related_contracts:
  - docs/contracts/2026-05-02-qa-verdict.contract.md
  - docs/contracts/qa-verdict.schema.json
reversibility: layered — Phase 0 is mostly two-way; Phase 1 hook + skill rollouts are two-way; Phase 2 loop generalization is one-way (loop edits compound and rolling back leaves edited code); Phase 3 schema freeze is the canonical one-way door (already named in tribunal plan)
revisit_trigger: end of Phase 2 (loops running in 3 of 4 repos for 2 sprints with measurable hill-climb) → review whether to tighten the discipline gates or relax them
---

> ⚠️ **SUPERSEDED (audited 2026-05-31).** Frontmatter status=superseded, superseded_by=docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md — explicit pointer to replacement. Treat as historical; see [architecture/README.md](architecture/README.md) (or the topic's current doc) for the up-to-date picture.

# Cherny Adoption + QA Tribunal CI Integration — Cross-Repo Plan

## North Star

Two coupled goals, neither sufficient alone:

1. **Disciplined development** — every repo (GA, ix, Demerzel, tars) gates merges through the same shape: PR → CI quality gate → multi-LLM review → tribunal verdict → merge. No repo is allowed to ship around the gate.
2. **Compound improvement over time** — every repo runs at least one cron-scheduled CI loop that hill-climbs a measurable quality metric. The loop's outputs feed the next sprint's discipline.

Discipline without improvement is bureaucracy. Improvement without discipline is drift. Both are required.

## Problem Frame

We have working pieces in GA — the chatbot improvement loop, the `/learnings` ritual, `state/quality/` baselines, multi-LLM review, the QA Architect Tribunal Phase 0 skeleton. The deep inventory (this turn) shows that:

- **Demerzel is the linchpin and has the largest gap.** It is supposed to orchestrate the tribunal but has **no IXQL runtime in CI**. `qa-architect-cycle.ixql` is a declarative artifact; nothing in `.github/workflows/` invokes it. **Phase 1 of the tribunal cannot fire on 2026-05-18 as currently scheduled.**
- **ix has the strongest CI muscle but is asymmetric** — produces `optick-sae-artifact.json` in code only, never via CI. The contract test asserts against a fixture frozen from GA, making ix a *consumer of its own contract*.
- **tars is largely greenfield** — 14 of 15 Cherny patterns absent. Smallest lift but also produces zero verdicts today.
- **Every repo has the hooks/digests vacuum** — only GA has `SessionStart`/`Stop`/`PostToolUse`. No repo has `PreCompact`, `PostCompact`, or `state/digests/`. Cross-session continuity is rebuilt cold every session in every repo.

The user-visible failure is: regressions land that a senior QA engineer would have caught structurally, work compounds in one repo while siblings drift, and every session-restart burns 200–400 tokens of ramp.

## Goals (Requirements Trace)

- **R1.** Phase 1 of the QA Architect Tribunal **fires on 2026-05-18** (or the deadline is explicitly moved with sign-off, not silently missed).
- **R2.** All four repos can produce a `qa-verdict.schema.json`-conforming verdict from CI on PR open.
- **R3.** All four repos have a `state/digests/` directory and a `PreCompact` hook writing to it.
- **R4.** All four repos run at least one CI cron-scheduled improvement loop. (GA already does for chatbot; ix, Demerzel, tars onboard sequentially.)
- **R5.** Every loop output is **schema-version-pinned** (`version_pins: {OPTIC-K: v1.8, ...}`) and refuses to compare against mismatched schemas.
- **R6.** Cross-repo dispatch is **bidirectional and explicit** — sibling PR open → Demerzel hears about it via `repository_dispatch`, not via cron-poll.
- **R7.** The schema file (`qa-verdict.schema.json`) is **vendored** into every consumer repo, not referenced cross-repo at runtime.
- **R8.** Multi-LLM review (the "tribunal" reviewer chain) runs in CI on every PR with `estimated_blast_score >= 0.4`, contributing structured `reviewer_chain[]` entries to the verdict, not free-form PR comments.
- **R9.** Verdict latency P95 ≤ 10 min for diffs under 1000 changed lines (matches GA tribunal plan R10 with one minute of slack).
- **R10.** Every repo's CLAUDE.md / AGENTS.md / BACKLOG.md / `docs/plans/` / `docs/solutions/` / `docs/archive/` directory shape matches GA's, so cross-repo skills (the `/feature`, `/learnings`, `/digest` family) work without per-repo conditionals.

## Scope Boundaries

**In scope:**
- All four sibling repos (GA, ix, Demerzel, tars).
- CI integration via GitHub Actions only.
- `repository_dispatch` for cross-repo orchestration.
- MCP server federation for tool exposure (already mostly in place per `.mcp.json` inventories).
- Cherny canonical patterns: 15-item list in §4.

**Out of scope:**
- Adopting Superpowers / GSD / gstack frameworks (decided 2026-05-14 brainstorm — overlap with existing stack, low net value).
- Cloud LLM judges in the tribunal Phase 1 (deferred per the GA tribunal plan; local Ollama only).
- Replacing existing per-repo review workflows (`claude-code-review.yml`, `gemini-review.yml`, Demerzel `cross-model-review.yml`). The tribunal verdict is **additive**, not a replacement.
- Hub/spoke contract repo (Q3 of GA tribunal contract). Schema continues to live in GA's `docs/contracts/` and is vendored elsewhere.
- Web UI for browsing verdicts.

## Cherny Talk — Load-Bearing Claims (verbatim where quoted)

From "Why Coding Is Solved, and What Comes Next" (Boris Cherny, Sequoia AI Ascent 2026, video `SlGRN8jh2RI`):

| # | Claim | Source quote | Implication for this plan |
|---|---|---|---|
| C1 | Loops are first-class | "Loops are the future at this point. If you haven't experimented with it, highly highly recommend it." | Cron-scheduled CI loops are the canonical form. Validate the AutoResearch transplant. |
| C2 | Cron pattern, not in-session | "Claude use cron to schedule a job for some point in the future... I have one that's babysitting my PRs, like fixing CI, auto-rebasing. I have another one that keeps CI healthy." | GitHub Actions `schedule:` cron + `repository_dispatch`, **not** `/loop` in-conversation. Aligns with R4. |
| C3 | Opus 4.7 hill-climbs natively | "With 4.7, it can just hill climb anything. So, if you give it a target and you tell it to iterate until it's done, it will just do it. I think this is the first model like that." | Opus 4.7 — the model in use — has the capability the AutoResearch pattern depends on. Removes the "is the model strong enough" risk. |
| C4 | Multi-Claude as standard | "Claudes are talking all day... they will communicate over Slack to talk to other people's Claudes that are also running in a loop to kind of figure out unknowns." | Multi-agent inter-LLM comm is normal. The QA Tribunal is the disciplined version of this. |
| C5 | Harness gets less important over time | "As the model's gotten better, the harness kind of gets less important." | Keep the harness minimal. One `/digest`, one `/auto-optimize`, two hooks. Resist the urge to add 30 skills. |
| C6 | MCP is the universal integration | "It's always just the simplest answer. It's just MCP." | MCP server federation across sibling repos is correct. Don't invent a parallel protocol. |
| C7 | The organizational shape is the gap | "I think there's actually a far bigger weed in kind of the organizational structure and organizational process." | The 15-pattern matrix below is the **organizational gap**; closing it is the load-bearing work, not adding more tooling. |
| C8 | Build for the next model | "We knew that it wouldn't have PMF for 6 months because we were building for the next model. And that was the idea the whole time." | Design hooks/schemas/contracts that work for Opus 4.8+ behaviors not yet observed (e.g., model proactively starting loops). Reversibility front-loaded. |

## Current State Matrix (validated by 2026-05-14 inventories)

15 Cherny patterns × 4 repos. Legend: ✅ present · 🟡 partial · ❌ absent · N/A.

| # | Pattern | GA | ix | Demerzel | tars |
|---|---|---|---|---|---|
| 1 | CLAUDE.md / AGENTS.md at root | ✅ | ✅ | ✅ | 🟡 (CLAUDE only) |
| 2 | BACKLOG.md at root | ✅ | ❌ | ❌ | ❌ |
| 3 | `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md` | ✅ | 🟡 (loose date convention) | ❌ | ❌ |
| 4 | `docs/solutions/<category>/<date>-<topic>.md` with frontmatter | ✅ | 🟡 (no frontmatter) | ❌ | ❌ |
| 5 | `docs/runbooks/*loop*.md` | ✅ | ❌ | ❌ | ❌ |
| 6 | `docs/archive/` | ✅ | ❌ | ❌ | 🟡 (loose) |
| 7 | `.claude/settings.json` hooks | 🟡 (3 events, no PreCompact) | 🟡 (PreToolUse + PostToolUse only) | ❌ (zero hooks) | ❌ |
| 8 | `.claude/skills/<name>/SKILL.md` | ✅ (~15) | ✅ (~96) | ✅ (~57) | ❌ |
| 9 | `.claude/agents/` custom agents | ✅ | ✅ | 🟡 (`personas/*.persona.yaml`) | ❌ |
| 10 | `.mcp.json` server federation | ✅ | ✅ (7 servers) | 🟡 (ga only) | 🟡 (3 servers, non-root path) |
| 11 | `state/quality/<domain>/baseline.json` with schema pin | 🟡 (no pins yet) | 🟡 (`state/quality-snapshots/`, no pins) | 🟡 (`state/quality-trend/`, no pins) | ❌ |
| 12 | `state/handoffs/` | ✅ | ❌ | ❌ | ❌ |
| 13 | `state/digests/` | ❌ | ❌ | ❌ | ❌ |
| 14 | Pre-commit hooks (installer script) | ✅ | ❌ | ❌ | ❌ |
| 15 | Auto-memory referenced in CLAUDE.md | ✅ | ❌ | ❌ | 🟡 (custom local memory) |
| — | **CI: GitHub Actions** | ✅ (13 workflows) | ✅ (7 workflows) | ✅ (18 workflows) | ✅ (2 workflows) |
| — | **CI: IXQL runtime in CI** | N/A | N/A | ❌ **(BLOCKER for Phase 1)** | N/A |
| — | **CI: `repository_dispatch` listener for sibling PRs** | ❌ | ✅ (consumes `demerzel-updated`) | ❌ | ✅ (consumes `demerzel-updated`) |
| — | **CI: produces `qa-verdict.json`** | ❌ (hardcoded skeleton only) | ❌ | ❌ (hardcoded skeleton in IXQL, never executes) | ❌ |

**Top 3 cross-repo gaps (load-bearing):**

1. **Demerzel has no IXQL runtime in CI.** The Phase 1 tribunal fires 2026-05-18 against a workflow that does not exist.
2. **`PreCompact` + `state/digests/` vacuum** in every repo. Every session re-discovers context cold.
3. **ix's OPTIC-K SAE artifact is code-only, never CI-produced.** The contract is enforced via a frozen fixture from a different repo — a hidden circular dependency.

## Phasing

Each phase ends with a working slice. No phase is gated on later phases.

### Phase 0 — Unblock Tribunal Phase 1 (this week, by 2026-05-18)

**Goal:** the scheduled trigger on 2026-05-18 fires against a CI workflow that produces a contract-valid verdict, even if hardcoded. The schema is not yet frozen.

**Deliverables:**

1. **Vendor `qa-verdict.schema.json` into Demerzel** as `Demerzel/schemas/contracts/qa-verdict.schema.json`. Add a sentinel comment pointing at GA's canonical source + `schema_source_commit_sha` field to detect drift.
2. **Stand up `Demerzel/.github/workflows/qa-tribunal.yml`** with two triggers:
   - `repository_dispatch: types: [sibling-pr-opened]` — for sibling repos to ping Demerzel on PR open.
   - `schedule: cron: '0 9 * * *'` — daily sweep at 09:00 UTC (the Phase 1 trigger time).
3. **Phase 0 verdict producer.** The workflow runs a **Python step** (not IXQL — see Risk R1) that reads the dispatch payload (or sweep target), validates against the vendored schema, and emits a hardcoded `informational` verdict to `state/quality/verdicts/<repo>/<ref>/<verdict_id>.json`. Path is artifact-uploaded for archaeology.
4. **Wire `repository_dispatch` SENDERS in ix + tars + GA** — minimal `qa-verdict-dispatch.yml` workflow, `on: pull_request: types: [opened, synchronize]`, single step: `gh api repos/spareilleux/Demerzel/dispatches -f event_type=sibling-pr-opened -f client_payload[repo]=...`. PAT-secured via existing `PAT_TOKEN` secret pattern already in use for `wiki-sync.yml`.
5. **Schema-pin existing baselines.** Walk `state/quality/<domain>/baseline.json` (GA), `state/quality-snapshots/{embeddings,voicing-analysis,chatbot-qa}/*.json` (ix), `state/quality-trend/*.jsonl` (Demerzel), `v2/baselines/glm-4-9b-q4/*.golden.json` (tars). Add a top-level `_schema: {version: 1, pins: {OPTIC-K: "v1.8", ...}}` field per file. JSONL files get a leading `{"_schema":...}` header line.
6. **Sign off (or move) the 2026-05-18 deadline.** If Phase 0 is not done by 2026-05-17 EOD, the trigger fires against nothing and we lose the institutional signal that "Tribunal works." Better to move the trigger now if it can't land.

**Reversibility:** Two-way. The vendored schema is a copy; the dispatch workflow is additive; baselines gain a `_schema` field that consumers can ignore. No producer-side code changes yet beyond the hardcoded emitter.

**Done when:** `gh workflow run qa-verdict-dispatch.yml --repo guitar-alchemist/ga` triggers a Demerzel workflow that writes a valid verdict JSON to a workflow artifact within 5 minutes.

---

### Phase 1 — Common Discipline Foundation (week of 2026-05-20)

**Goal:** every sibling repo (ix, Demerzel, tars) has the GA-canonical docs+state directory shape, so cross-repo skills work uniformly. Hooks installed. Digest infrastructure live.

**Deliverables:**

1. **Per-repo doc skeleton.** For each of ix, Demerzel, tars:
   - Create `BACKLOG.md` at root (migrate existing scattered work-tracking into it).
   - Create `docs/plans/`, `docs/solutions/<category>/`, `docs/runbooks/`, `docs/archive/`.
   - Move existing dated plans into `docs/plans/YYYY-MM-DD-<type>-<name>-plan.md` form (ix has the most existing inventory; tars least).
   - Add YAML frontmatter to existing `docs/solutions/` entries (ix has ~12 to backfill).
2. **PreCompact + SessionStart hooks** installed in every repo:
   - `PreCompact` writes `state/digests/latest.md` per the schema designed by octo:personas:context-manager (see `.claude/agent-memory/octo-personas-context-manager/project_session_digest_design.md`).
   - `SessionStart(matcher:compact|resume)` reads `state/digests/latest.md` and emits it as plain text to stdout (gets injected as context per the claude-code-guide hook table).
   - Hook command is shared via a checked-in script per repo: `Scripts/precompact-digest.{ps1,sh}` (PowerShell on Windows-primary repos, shell elsewhere).
3. **Pre-commit hook installer** (`Scripts/install-git-hooks.{ps1,sh}`) per repo, mirroring GA's existing one. Each enforces format + build + warnings-as-errors before push.
4. **`.mcp.json` federation.** Demerzel adds `ix` + `tars` + the new `demerzel` server (if a Demerzel MCP server is stood up — see Phase 2). tars adds `demerzel`. Goal: every repo can `mcp__plugin_<sibling>__*` call its peer's tools.
5. **Verify SessionStart matcher behavior** (task #176) before relying on the digest read-back. If `SessionStart(matcher:compact)` doesn't fire on auto-compaction, fall back to model self-reading `state/digests/latest.md` via Bash at session start (less seamless but workable).

**Reversibility:** Two-way. Doc directories are additive; hooks are removable from `settings.json`; the digest schema is v1 with explicit versioning (reader ignores unknown future versions per context-manager's spec).

**Instrumentation:**

| Metric | Baseline | Direction | Guardrail | Storage |
|---|---|---|---|---|
| Tokens-to-warm post-compaction | measure for 1 week pre-Phase-1 | ↓ 50% by end of week 2 | review if no change | derived from session logs |
| % of sessions writing a digest | 0 | ≥ 80% by end of week 2 | review hook plumbing if < 60% | count of `state/digests/archive/*.md` per session |

**Done when:** all four repos can run `/digest` (manual) successfully; PreCompact hook fires and writes a valid digest in at least one observed auto-compaction; sibling-repo SessionStart in a fresh shell reads the digest.

---

### Phase 2 — Compound Improvement Loops (week of 2026-05-27)

**Goal:** every repo runs at least one cron-scheduled CI improvement loop. The loop reads a baseline, runs an oracle, proposes a fix, applies, re-runs, commits if improved, reverts if not. Bounded by kill-switch + budget.

**Deliverables per repo:**

1. **GA** — codify the existing chatbot improvement loop as the **reference**. Already running per `docs/runbooks/chatbot-improvement-loop.md`. Add: schema-version pins (Phase 0 done), `.STOP` file kill-switch (already partly there), max-commits-per-session=50 + max-wall-clock-minutes=480 cap.
2. **ix** — onboard **OPTIC-K SAE quality loop** as second loop. Workflow `optick-sae-loop.yml`:
   - `schedule: cron: '0 4 * * *'` (04:00 UTC daily).
   - Runs `cargo run -p ix-optick-sae` (currently code-only — Phase 2 wires it into CI).
   - Writes `state/quality/optick-sae/YYYY-MM-DD/optick-sae-artifact.json` per the existing contract (`docs/contracts/2026-05-02-optick-sae-artifact.contract.md`).
   - Compares against the prior day's artifact, applies a single proposed retune if delta improves, opens a PR with `auto-loop` label if so.
3. **Demerzel** — wire `demerzel-self-improvement.yml` (already exists, cron Sun 15:17 UTC, currently no-op) to actually execute `qa-architect-cycle.ixql` once Phase 3 lands an IXQL runtime. Phase 2 deliverable: the cron job exists and writes a no-op `informational` verdict to `state/quality/verdicts/sweeps/`. **One-way door** flag: the IXQL runtime decision (build vs Python-port) gets named in this phase (see Risk R1).
4. **tars** — author `tars-evolve-loop.yml`:
   - `schedule: cron: '0 5 * * *'` (05:00 UTC daily).
   - Runs `dotnet run --project v2/src/Tars.Interface.Cli -- evolve --benchmark code --loop 1`.
   - Diffs against `v2/baselines/glm-4-9b-q4/*.golden.json`.
   - Writes a `qa-verdict.schema.json`-conforming artifact to `state/quality/tars/<sha>.verdict.json` (closes tars's asymmetry — it produces a verdict).
5. **Generalized `/auto-optimize` skill** authored in GA and copied to siblings. Skill takes `oracle_script_path` + `baseline_path` + `domain`. Refuses to compare schema-mismatched runs. Per-domain `.lock` file in `state/quality/<domain>/.lock`. Max-commits cap. Kill-switch via `state/quality/<domain>/.STOP`.

**Reversibility:** **One-way** at the loop-edit level. Each commit by a loop is git-revertable, but the *trajectory* of compounded edits cannot be cleanly undone without re-running history. Mitigation: each loop commits with `Co-Authored-By: <repo>-auto-loop <noreply@guitar-alchemist.io>` so git log filtering reveals the loop's footprint; an explicit `auto-loop-revert` skill performs bulk revert if a campaign goes sideways.

**Instrumentation:**

| Metric | Baseline | Direction | Guardrail | Storage |
|---|---|---|---|---|
| Loop commits/day per repo | 0 (current) | 1–10/day after week 1 | review budget if > 25/day | derived from `git log --author='auto-loop'` |
| Quality metric delta per loop run (e.g., chatbot corpus pass rate) | per-domain baseline from Phase 0 | strictly non-decreasing | hard fail if regresses 2 runs in a row | `state/quality/<domain>/loop-deltas.jsonl` |
| Loop plateau detection (consecutive runs with < 0.5% improvement) | n/a | trigger auto-stop at 5 consecutive | — | derived from deltas |

**Done when:** all four repos have at least one cron-scheduled loop running for one week with non-zero commits and a measurable hill-climb. ix's OPTIC-K artifact is actually produced by CI for the first time.

---

### Phase 3 — Tribunal Phase 1–4 (rolls into existing GA tribunal plan)

**Goal:** the QA Architect Tribunal becomes the canonical multi-LLM gate per the existing plan (`docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md`). This plan does not re-author that work; it removes the blockers Phase 0 surfaced.

**Deliverables:**

1. **Decide and ship the IXQL runtime in Demerzel CI** (Risk R1). Two paths:
   - **A. Build `tars.runtime` CLI** — invokable via `dotnet run --project Demerzel/runtime -- exec pipelines/qa-architect-cycle.ixql --trigger state/qa-architect/trigger.json`. Aligns with IXQL-native architecture preference. ~1 week build.
   - **B. Port `qa-architect-cycle.ixql` logic into Python** orchestrated inside `qa-tribunal.yml`. ~2 days. Loses IXQL declarativity for this pipeline but the other 22 pipelines stay IXQL.
   - Default: A. Fallback: B with explicit "we're cutting IXQL from this one pipeline because IXQL CI runtime didn't ship in time."
2. **Wire real evidence producers** per GA tribunal plan Phase 1:
   - `qa_verify_invariants` → invariant suite (5-layer dep check + OPTIC-K dim assertion + contract-locked field check).
   - `qa_score_quality_drift` → `state/quality/*.json` time-series diff (now Phase 0 pins make this safe).
   - `qa_assess_blast_radius` → static analyzer over changed paths.
   - `CriticAgent` as the `semantic_judge` reviewer.
3. **GitHub Check Run integration.** Demerzel's `qa-tribunal.yml` writes a Check Run on the sibling PR via `gh api repos/<repo>/check-runs`. Branch protection on `main` requires the `qa-architect/tribunal` check to pass.
4. **Phase 3 of GA tribunal plan** — PR comment posting, followup → GitHub issue automation, Graphiti defect-memory ingestion.
5. **Phase 4 of GA tribunal plan** — algedonic feedback + 2-sprint soak → schema v1.0.0 freeze.

**Reversibility:** **One-way** for the schema v1.0.0 freeze (R7 of GA tribunal plan). Everything else two-way. Each Phase 3 deliverable can be reverted by disabling the workflow or the branch protection check.

**Done when:** opening a PR on any of the four repos produces a verdict comment within 10 minutes, P0 verdicts block merge, P1 verdicts open a tracked followup issue. Schema is still v0.1 — soak begins.

---

## CI Integration Map (load-bearing — per user emphasis)

This section names every cross-repo CI flow and the artifact handoffs.

### Triggers and dispatches

```
GA / ix / tars : on pull_request [opened, synchronize]
   ↓ qa-verdict-dispatch.yml
   ↓ gh api repos/spareilleux/Demerzel/dispatches
   ↓ event_type=sibling-pr-opened
   ↓ client_payload={repo, pr, head_sha, base_sha}
       ↓
Demerzel : on repository_dispatch [sibling-pr-opened]
   ↓ qa-tribunal.yml
   ↓ {Phase 0: hardcoded verdict | Phase 3: real tribunal aggregation}
   ↓ writes state/quality/verdicts/<repo>/<pr>/<verdict_id>.json
   ↓ posts Check Run via gh api repos/<repo>/check-runs (Phase 3)
       ↓
GA / ix / tars : Check Run blocks merge if verdict=block, else allows
```

### Scheduled CI jobs (cron grid, all UTC)

| Time | Repo | Workflow | Purpose | Phase |
|---|---|---|---|---|
| 04:00 daily | ix | `optick-sae-loop.yml` | Produce + retune OPTIC-K SAE | Phase 2 |
| 05:00 daily | tars | `tars-evolve-loop.yml` | Grammar evolve + verdict emit | Phase 2 |
| 06:00 daily | Demerzel | `qa-tribunal.yml` (sweep mode) | Snapshot drift triage | Phase 0 stub, Phase 3 real |
| 06:00 daily | GA | `chatbot-improvement-loop.yml` | Existing — codify | Phase 2 (codify) |
| 09:00 daily | Demerzel | `qa-tribunal.yml` (trigger 2026-05-18 fixture) | Phase 1 fixture | Phase 0 |

### Artifact paths (canonical)

| Artifact | Producer | Consumer(s) | Path |
|---|---|---|---|
| Verdict JSON | Demerzel `qa-tribunal.yml` | GA/ix/tars Check Run, Graphiti | `state/quality/verdicts/<repo>/<pr-or-sweep>/<verdict_id>.json` |
| OPTIC-K SAE artifact | ix `optick-sae-loop.yml` | GA chatbot loop, Demerzel sweep | `state/quality/optick-sae/<date>/optick-sae-artifact.json` |
| Chatbot corpus deltas | GA `chatbot-improvement-loop.yml` | Demerzel tribunal (gap evidence) | `state/quality/chatbot-qa/loop-deltas.jsonl` |
| tars verdict | tars `tars-evolve-loop.yml` | Demerzel tribunal | `state/quality/tars/<sha>.verdict.json` |
| Session digest | per-repo PreCompact hook | per-repo SessionStart hook | `state/digests/latest.md` + `state/digests/archive/<ts>-<sessionId>.md` |

### Branch protection

Required status checks on `main` of every repo (post Phase 3):
- existing repo-native CI (e.g., `build`, `test-backend`, `cargo test`)
- new: `qa-architect/tribunal` (the Demerzel-emitted Check Run)

### Secrets / tokens

- `PAT_TOKEN` per repo (existing) — scoped to `repo:write` for cross-repo dispatch.
- Demerzel needs `repo:write` on GA, ix, tars to post Check Runs.
- Verdict JSON in artifacts — public is fine; verdict bodies may contain code excerpts. **Don't post verdict bodies to a public mirror** (security audit item).

## Instrumentation (rolled up)

| Metric | Baseline | Direction | Guardrail | Storage |
|---|---|---|---|---|
| Verdict latency P95 (PR open → comment) | none (new) | ≤ 10 min by end of Phase 3 | hard fail at 15 min | derived |
| % PRs receiving a verdict | 0% | ≥ 95% by Phase 3 end | < 80% triggers workflow audit | derived |
| Cross-repo dispatch latency (PR open → Demerzel job start) | none (new) | ≤ 30s P95 | review payload size if > 60s | derived |
| Tokens-to-warm post-compaction (digest impact) | measure pre-Phase-1 | ↓ 50% | review hook plumbing | session logs |
| Loop commits/day per repo | 0 | 1–10 | > 25 review budget | git log |
| Quality metric per loop run | per-domain baseline | strictly non-decreasing | 2 regressions in a row → fail | `state/quality/<domain>/loop-deltas.jsonl` |
| Schema-pin coverage | 0% | 100% of baselines | hard fail loop on missing pin | inspection |

## One-Way Doors

- **D1 (already named).** QA Verdict schema v1.0.0 freeze (end of Phase 3 + 2-sprint soak). Until then, draft.
- **D2 (already named).** Producer slug registry in the schema. Adding `ix-optick-loop`, `tars-evolve-loop`, `demerzel-sweep` requires a contract amendment.
- **D3 (new).** Choice of IXQL runtime path A (build `tars.runtime` CLI) vs B (Python port). Path A is reversible-ish; Path B prunes IXQL from one pipeline, semi-permanent.
- **D4 (new).** Loop edit trajectory. Compound edits by autonomous loops are reversible commit-by-commit but not undo-the-campaign cleanly. Mitigation: `auto-loop-revert` skill + `Co-Authored-By` tagging for grep-revert.
- **D5 (new).** Schema-pin format. Once `_schema: {version: 1, pins: {...}}` is the canonical header in `state/quality/`, changing the field name breaks every loop. Pick the format once.

Two-way (everything else): per-repo doc skeleton, hooks, digest schema (versioned), MCP server federation, loop cron timing.

## Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| **R1. Demerzel ships no IXQL runtime in time for 5/18.** Highest probability. | Critical | Path B (Python port of `qa-architect-cycle.ixql` logic for the tribunal hot path) is the unblock; Path A continues in parallel. |
| **R2. Cross-repo `repository_dispatch` PAT token expires or is revoked.** Tribunal silently stops firing. | High | Add a Demerzel-side liveness check: if no dispatches in 24h, open an issue. Health endpoint not load-bearing — visibility is. |
| **R3. Schema drift between vendored copies.** Demerzel's copy and GA's copy diverge silently. | High | Phase 0 vendoring includes `schema_source_commit_sha`. CI `governance-validate.yml` step diffs every vendored schema against GA's canonical source on every push; alarms on mismatch. |
| **R4. Loop edits compound into architectural drift.** Each commit looks fine in isolation; the trajectory degrades the architecture. | High | Per-domain `scope_boundary` declaration in baseline (e.g., chatbot loop edits only `Common/GA.Business.ML/Agents/Skills/`). Hard refuse to touch paths outside the boundary. |
| **R5. Verdict noise overwhelms PR signal.** Engineers learn to ignore the bot. | Medium | P3 informational verdicts collapse into a weekly summary (per GA tribunal plan). Only P0–P2 post per-PR. |
| **R6. Phase 2 loops fight for the same `state/quality/` artifacts.** Lock contention or stale-read races. | Medium | Per-domain `.lock` file (single-writer rule, per architect's earlier brainstorm). Plus `cascade_invalidation` via `depends_on:` declarations in baselines. |
| **R7. Octo brainstorm convergence on "skip GSD" was wrong** — we may be underweighting GSD's scope-reduction detector specifically. | Low | Stand up the scope-reduction detector as a Phase 2 deliverable independent of GSD adoption — it's a pre-commit diff between `docs/plans/<plan>.md` requirements list and the diff. ~2 hours, two-way. |
| **R8. PreCompact hook output unused.** If `SessionStart(matcher:compact)` doesn't fire on auto-compaction (claude-code-guide gave conflicting info), the digest read-back fails silently. | Medium | Phase 1 explicitly includes verifying matcher behavior (task #176). If it doesn't fire, fall back to model self-reading `state/digests/latest.md` via Bash on first prompt — works, less seamless. |
| **R9. Cherny's "harness gets less important" framing is taken too literally.** We under-invest in discipline tooling because "the model will figure it out." | Medium | Discipline (gates, schemas, contracts) is not harness — it's organizational structure (C7). The harness Cherny says becomes less important is in-conversation tooling (skills, hooks), not cross-team coordination. Keep discipline tight, harness minimal. |
| **R10. Multi-LLM tribunal cost spirals.** Local Ollama latency may be insufficient at scale. | Medium | Phase 1 explicitly local-only (per GA tribunal plan). Cloud-judge cost budget is a separate plan; not unlocked by this plan. |

## Open Questions (resolve before sign-off)

1. **Q1.** IXQL runtime path A vs B for Demerzel CI (Risk R1) — pick before Phase 0 ships.
2. **Q2.** Do we want a fifth sibling repo (e.g., a future `contracts` repo) holding canonical schemas, or do we keep them in GA's `docs/contracts/`? Cherny tribunal plan §Q3 punted this. This plan defers it again.
3. **Q3.** Cron timing collisions — 04:00 (ix) / 05:00 (tars) / 06:00 (Demerzel + GA) / 09:00 (Demerzel Phase 1 fixture). Acceptable, but if loops grow they will fight for the same shared CI runner pool. Move to staggered hours after instrumentation.
4. **Q4.** Should the autonomous loops be allowed to edit a sibling repo? E.g., the chatbot loop in GA discovers an OPTIC-K mis-binding and the actual fix is in ix. Currently no — loops are repo-local. Future: cross-repo loop coordination via dispatch + PR.
5. **Q5.** tars verdict shape — is "grammar conformance score" the right metric, or should tars verdicts be `informational` (no merge-block) until grammar coverage stabilizes?

## Sign-off Required Before Phase 0

1. **Approve Path A vs Path B for IXQL runtime** (Q1) — chooses one-way door D3.
2. **Confirm 2026-05-18 deadline shape.** If Phase 0 can't ship by 5/17 EOD, the deadline gets moved (with a written note), not silently missed.
3. **Confirm schema-pin format** `_schema: {version: 1, pins: {...}}` as canonical for `state/quality/` JSON.
4. **Confirm `Co-Authored-By: <repo>-auto-loop` tag format** for loop commits (D4 mitigation).
5. **Authorize Demerzel `PAT_TOKEN` scope expansion** to `repo:write` on GA, ix, tars (required for Phase 3 Check Run posting).

## Cross-References

- Octo brainstorm outputs (this plan inherits structurally):
  - Strategy-analyst: tribunal vs framework adoption priority — informs Phase 0 ordering.
  - TDD-orchestrator: block-don't-delete TDD discipline — folded into Phase 2 loop boundaries (loops must not violate test-first guarantees in pure domain code).
  - Context-manager: session-digest schema — fully reused in Phase 1.
  - Backend-architect: skill discoverability + schema-pin + cascade invalidation — fully reused.
- Memory:
  - `feedback_multi_llm_review_pays_off.md` — empirical evidence for the tribunal pattern.
  - `feedback_check_ci_before_next_chunk.md` — CI verification discipline.
  - `feedback_ixql_architecture.md` — informs Path A preference.
  - `project_qa_architect_tribunal.md` — Phase 0 shipped state.
- Cherny talk: `https://www.youtube.com/watch?v=SlGRN8jh2RI` (cached transcript local).
