---
title: "arch: QA Architect Tribunal — Cross-Repo Senior QA Engineer"
type: arch
status: draft
date: 2026-05-02
origin: in-conversation investigation, 2026-05-02
contract: docs/contracts/2026-05-02-qa-verdict.contract.md
reversibility: mostly two-way doors; QA verdict schema (v1.0.0 freeze) is the one-way door
revisit_trigger: end of Phase 4 (verdict-driven PR gating live for 2 sprints) → review schema, decide v1.0 freeze
---

# QA Architect Tribunal — Cross-Repo Senior QA Engineer

## Overview

Stand up a senior-QA-engineer-grade capability that spans GA, ix, Demerzel, and TARS. Two coupled deliverables:

- **Option B — Demerzel pipeline `qa-architect-cycle.ixql`**: the *role*. Orchestrates a tribunal of specialist agents on every PR and on a daily cadence; emits a QA Verdict (per contract); posts PR comments, opens GitHub issues, feeds the algedonic monitor.
- **Option C — `Apps/GaQaMcp/` MCP server**: the *hands*. Exposes blast-radius scoring, gap analysis, invariant verification, adversarial replay, snapshot drift, and test proposals as MCP tools so any agent in any repo can invoke them.

The verdict contract ([2026-05-02-qa-verdict.contract.md](../contracts/2026-05-02-qa-verdict.contract.md)) is the connective tissue.

## Problem Frame

Today GA has all the *parts* of senior QA — semantic basins, daily quality snapshots, CriticAgent, Playwright, ix's adversarial corpus, Demerzel's `weakness-probe` / `chaos-test` / `governance-shake-test` pipelines — but no *accountable role* synthesizes them. Each surface produces signal in its own format; no agent currently:

- Scores a PR's blast radius against the 5-layer rule and OPTIC-K invariants.
- Designs the test surface for new features (property tests, fuzz inputs, contract tests across sibling repos).
- Watches `state/quality/voicing-analysis/*.json` as a time series and triages drift.
- Maintains a defect knowledge graph that says "we got burned by X last quarter — re-check before shipping similar."

The user-visible failure mode: regressions land that a senior QA engineer would have caught architecturally, even when individual tests pass.

## Requirements Trace

- R1. Every PR receives a QA Verdict comment within 10 minutes of opening / push.
- R2. Verdicts conform to [qa-verdict.contract.md](../contracts/2026-05-02-qa-verdict.contract.md) v0.1+.
- R3. P0 verdicts block merge via GitHub branch protection check.
- R4. P1 verdicts open a `qa-followup` GitHub issue auto-linked to the PR.
- R5. Daily sweep at 06:00 UTC re-scores `state/quality/*.json` snapshots, emits informational verdicts on drift > guardrail.
- R6. MCP server exposes ≥ 6 primitives (§ MCP Surface) callable from GA, ix, Demerzel, Octopus, Compound Engineering, Conductor.
- R7. Reviewer chain in every verdict shows ≥ 3 distinct agent roles for non-trivial diffs (`estimated_blast_score ≥ 0.4`).
- R8. Defect knowledge graph (Graphiti) ingests every followup; `qa-architect-cycle` queries it before emitting a verdict to surface "we burned ourselves on this before."
- R9. Algedonic monitor consumes a derived signal (P0 rate, P1 backlog growth) — not raw verdicts.
- R10. Verdict latency P95 ≤ 8 minutes for diffs under 1000 changed lines.

## Scope Boundaries

- In scope: GA repo + Demerzel + ix integration. TARS used opportunistically for deep test design (one role in tribunal, not blocking).
- In scope: PR gating via GitHub Actions check.
- Out of scope: replacing existing `claude-code-review.yml` / `gemini-review.yml` workflows — they continue, the QA Verdict is *additive* and authoritative on architectural / cross-repo concerns where they're not.
- Out of scope: visual regression / screenshot diffing for R3F demos (deferred to follow-up plan; evidence kind reserved in schema).
- Out of scope: cost / pricing of cloud LLM judges — Phase 1 uses local Ollama judges from existing `IJudgeService`. Cloud judges added under separate cost-budget plan.
- Out of scope: web UI for browsing verdicts — JSON in `state/quality/verdicts/` + GitHub PR comments are the surface for v1.

## Context & Research

### Relevant Code and Patterns

- **GA agents** ([Common/GA.Business.ML/Agents/](../../Common/GA.Business.ML/Agents/)): `GuitarAlchemistAgentBase`, `SemanticRouter` (already supports `AggregateAsync` for tribunals), `CriticAgent` (semantic judge — wire as a tribunal role), `AgentConstants.cs` (add `QaArchitect`).
- **Semantic testing** (`Common/GA.Testing.Semantic`, `IJudgeService`, `OllamaJudgeService`): four-phase roadmap complete per [ai_testing_roadmap.md](../../Common/GA.Business.ML/Documentation/Architecture/ai_testing_roadmap.md). Reuse as the `semantic_basin` evidence producer.
- **Quality snapshots** ([state/quality/](../../state/quality/)): daily JSON; `ix-quality-trend` aggregator already exists per CLAUDE.md. Plug snapshot drift into evidence array.
- **Demerzel pipelines** (`../Demerzel/pipelines/`): `weakness-probe.ixql`, `chaos-test.ixql`, `governance-shake-test.ixql`, `shake-metafix-loop.ixql`, `render-critic.ixql`, `algedonic-belief-monitor.ixql`. `qa-architect-cycle.ixql` joins this family — same IXQL grammar, same scheduler.
- **ix adversarial** (`../ix/tests/adversarial/`): existing replay corpus. New `ix-adversarial-runner` thin agent invokes the corpus and emits an evidence object.
- **MCP precedent**: [`plugin_ga_ga-dsl`](../../Apps/) — already exposes 60+ GA primitives. `Apps/GaQaMcp/` mirrors the project shape.
- **Contract precedent**: [optick-weights-config contract](../contracts/2026-04-27-optick-weights-config.contract.md) — same producer/consumer/JSON-shape structure used here.
- **Compound-engineering personas**: `ce-correctness-reviewer`, `ce-architecture-strategist`, `ce-data-integrity-guardian`, `ce-security-reviewer`. These are *advisory*; we don't replace them — `qa-architect-cycle` may invoke them as subagents for high-blast-score diffs.

### Institutional Learnings

- **One-way door discipline** (CLAUDE.md): Plan logs reversibility. Verdict schema is the one-way door — everything else is two-way.
- **Instrument before ship** (CLAUDE.md): baseline + direction + guardrail. § Instrumentation declares all three.
- **Fallback data required** (memory: `feedback_fallback_data.md`): every panel must show data even on failure. The verdict surface must degrade gracefully — partial tribunal output is still a valid `informational` verdict.
- **API param verification** (memory: `feedback_api_params.md`): always curl-test API params before coding. MCP primitives validated against real GA test suite before integration.
- **IXQL-native architecture** (memory: `feedback_ixql_architecture.md`): user prefers IXQL DSL over YAML configs. `qa-architect-cycle.ixql` extends IXQL, doesn't introduce a new orchestration syntax.

## Key Technical Decisions

- **Verdict schema is the one-way door.** Every other piece can be replaced (a different MCP server, a Python orchestrator instead of IXQL, a different judge model) as long as it produces / consumes valid verdicts. The schema gets the most scrutiny.
- **MCP server, not REST.** Aligns with existing `plugin_ga_ga-dsl` pattern, lets every agent surface (Claude Code, Octopus, Conductor, Compound) call the same primitives without each one writing its own HTTP client.
- **C# host for the MCP server.** Reuses GA's loaded test infrastructure (xUnit runners, `GA.Testing.Semantic`, OPTIC-K schema). Rust would mean re-marshalling the test corpus across a process boundary — premature optimization given measured GA test latency.
- **Local Ollama judge for Phase 1**, cloud judges later. Cost predictable, latency local, matches `OllamaJudgeService` precedent.
- **Tribunal aggregation via existing `SemanticRouter.AggregateAsync`.** Don't write a new fan-out primitive; harden the existing one.
- **Defect memory in Graphiti.** Tests already wire it; reuse instead of standing up a parallel store.
- **Verdicts are immutable; re-assessment links via `supersedes`.** Mirrors event-sourcing discipline; lets the time series be replayed.

## MCP Surface (Option C)

`Apps/GaQaMcp/` — registered tools:

| Tool | Input | Output | Backed by |
|---|---|---|---|
| `qa_assess_blast_radius` | `{repo, base_sha, head_sha}` | `blast_radius` object per contract | Static analysis of changed paths against 5-layer map + invariant registry |
| `qa_gap_analyze` | `{diff, test_globs?}` | array of `followup` candidates | Coverage tool + heuristics; LLM judge for "is this critical-path?" |
| `qa_propose_tests` | `{component, kind: property\|fuzz\|contract\|integration}` | array of test stubs (file path + body) | Template library + LLM completion grounded in existing test patterns |
| `qa_verify_invariants` | `{target}` | array of `evidence{kind: contract_check}` | Runs invariant suite (OPTIC-K dim, 5-layer rule, schema-locked contracts) |
| `qa_replay_adversarial` | `{target, corpus?: "ix" \| "ga"}` | `evidence{kind: adversarial_replay}` | Shells to ix corpus runner; falls back to GA's local adversarial set |
| `qa_score_quality_drift` | `{metric, window_days}` | `evidence{kind: quality_snapshot}` | Reads `state/quality/*.json` time series, computes drift vs guardrail |
| `qa_lookup_defect_memory` | `{query, k?}` | array of past `followups` matching pattern | Graphiti query |
| `qa_emit_verdict` | full verdict object | persisted path + verdict_id | Validates against schema, writes to `state/quality/verdicts/`, posts PR comment |

Naming follows MCP convention (`qa_` prefix). All tools idempotent except `qa_emit_verdict`.

## Phasing

Each phase ends with a working slice. No phase is gated on later phases.

### Phase 0 — Schema & Skeletons (this week)

- Sign off on contract v0.1.0 (this turn).
- Generate `docs/contracts/qa-verdict.schema.json` from the contract markdown.
- Stand up `Apps/GaQaMcp/` skeleton: project, MCP host wiring, smoke test (returns hardcoded verdict).
- Stand up `Common/GA.Business.ML/Agents/QAArchitectAgent.cs` skeleton: extends `GuitarAlchemistAgentBase`, registered in `AgentConstants.QaArchitect`, returns hardcoded verdict.
- Stand up `Demerzel/pipelines/qa-architect-cycle.ixql` skeleton: triggers on cron, calls a no-op stage, writes a hardcoded verdict to `state/quality/verdicts/`.
- Round-trip test: skeleton emits a contract-valid verdict that parses on the consumer side.

**Done when:** end-to-end skeleton emits a valid verdict and a Graphiti node for one followup.

### Phase 1 — Real evidence producers (week 2)

- Wire `qa_verify_invariants` to the actual invariant suite (5-layer dependency check, OPTIC-K dim assertion, contract-locked field check).
- Wire `qa_score_quality_drift` to existing `ix-quality-trend` aggregator output.
- Wire `qa_assess_blast_radius` to a static analyzer over changed paths (heuristic: file path → layer; namespace → component).
- Wire `CriticAgent` as the `semantic_judge` reviewer role.
- Verdicts now contain real evidence; followups still hand-authored.

**Done when:** running the MCP server against a real PR produces a verdict with ≥ 3 evidence items, no followups, narrative auto-generated.

### Phase 2 — Tribunal & gap analysis (week 3)

- Implement `qa_gap_analyze` (coverage + LLM judge).
- Implement `qa_propose_tests` (template-driven for property/fuzz; LLM-completion grounded for integration/contract).
- Add `ix-adversarial-runner` thin client; wire as a tribunal role.
- `SemanticRouter.AggregateAsync` aggregates roles into `reviewer_chain`.
- Followups now auto-generated; severity rollup wired.

**Done when:** the tribunal produces a verdict on a deliberately broken PR that catches the break with a `must_fix` followup and proposes a test.

### Phase 3 — PR integration & defect memory (week 4)

- GitHub Actions workflow `qa-architect.yml` invokes `qa-architect-cycle` on PR open/sync.
- PR comment posting + check-run for branch protection (P0 blocks).
- Followup → GitHub issue automation.
- Graphiti ingestion of every followup; `qa_lookup_defect_memory` callable.
- TARS test-designer wired as optional deep-reasoning role for `estimated_blast_score ≥ 0.7`.

**Done when:** open a PR, get a verdict comment in ≤ 10 min, P0 blocks merge, P1 opens a tracked issue.

### Phase 4 — Algedonic feedback & sweep cadence (week 5)

- Daily 06:00 UTC sweep verdict on snapshot drift.
- Algedonic monitor consumes derived signals (P0 rate, P1 backlog growth, regression latency) — not raw verdicts.
- 2-sprint soak.
- End-of-Phase-4 review: freeze schema at v1.0.0 if no breaking issues found.

**Done when:** verdict-driven gating live for 2 sprints, P0 false-positive rate measured, schema v1.0.0 sign-off.

## Instrumentation

Per CLAUDE.md "instrument before you ship":

| Metric | Baseline | Direction | Guardrail | Storage |
|---|---|---|---|---|
| Verdict latency P95 (PR open → comment) | none (new) | ≤ 8 min by Phase 3 | hard fail at 15 min | `state/quality/qa-architect/latency.json` |
| P0 false-positive rate | none (new) | ≤ 5% by end of Phase 4 | review schema if > 15% | manual triage log |
| Defect-escape rate (post-merge bugs that match a missed `qa_gap_analyze` candidate) | measure for 1 sprint pre-Phase-3 | ↓ 30% by end of Phase 4 | review tribunal weights if no movement | `state/quality/qa-architect/escapes.json` |
| Tribunal coverage (% of PRs with ≥ 3 reviewer roles) | none (new) | ≥ 80% on `blast_score ≥ 0.4` | review `AggregateAsync` reliability if < 60% | derived from verdicts |
| Quality-snapshot drift detection lag | currently undetected | ≤ 24h by Phase 4 | manual sweep if > 72h | derived from sweep verdicts |

Baselines without prior data: measure for 1 sprint before Phase 3 lands so post-launch deltas are real.

## One-Way Doors

- **D1.** QA Verdict schema v1.0.0 freeze (end of Phase 4). Until then it's draft and may break.
- **D2.** MCP tool surface (renames / removals are breaking). Additive changes safe.
- **D3.** `risk_tier` enum (P0–P3). Adding a fifth tier is breaking; map all consumers first.
- **D4.** Producer slug registry (currently 6 slugs). Removing a slug breaks historical verdicts.

Two-way doors (everything else): MCP server language, judge model, tribunal composition, scheduler, storage layout under `state/quality/verdicts/`.

## Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Tribunal cost / latency blows past guardrail | Phase 1 uses local Ollama only; cloud judges gated behind cost budget plan. |
| Schema premature freeze locks bad shape | Explicit v0.1 draft phase + 2-sprint soak before v1.0.0. Open Questions §6 of contract resolved before freeze. |
| Verdict noise overwhelms PR signal | P3 informational verdicts collapse into a single weekly summary; only P0–P2 post per-PR comments. |
| Defect memory poisons future verdicts (stale "we got burned" facts) | Graphiti nodes have age decay; `qa_lookup_defect_memory` ranks by recency × frequency, surfaces age in verdict narrative. |
| Sibling repo (ix / Demerzel) lags GA on contract version | Schema v1.x additive-only rule; consumers treat unknown fields as informational. |
| MCP server becomes load-bearing single point of failure | Stateless tools; verdicts are the persistent state. Restarting the MCP server doesn't lose history. |

## Sign-off Required Before Phase 1

1. Approve contract v0.1.0 shape (especially §3 field reference and §3.2 risk tier definitions).
2. Confirm MCP surface (8 tools) covers the senior-QA workflow, or name additions.
3. Confirm Phase 0 done-when criteria.

Phase 0 work is reversible — no schema freeze, no PR gating, no GitHub workflow yet. Skeletons can be deleted if direction changes.
