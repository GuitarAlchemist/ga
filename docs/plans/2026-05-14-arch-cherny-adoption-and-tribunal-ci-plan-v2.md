---
title: "arch: Cherny adoption + QA Architect Tribunal CI integration — cross-repo (v2)"
type: arch
status: draft-pending-signoff
version: 2.0
date: 2026-05-14
supersedes: docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan.md
origin: v1 + 4-reviewer octo panel (adversarial / feasibility / deployment / security) + NotebookLM interrogation of Compound-the-Compounding + Software-3.0-Karpathy notebooks
related_plans:
  - docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md
related_contracts:
  - docs/contracts/2026-05-02-qa-verdict.contract.md
  - docs/contracts/qa-verdict.schema.json
reversibility: layered — Phase 0 mostly two-way; Phase 1 hooks + doc shape additive; Phase 2 loops compound and rollback is hard (one-way at trajectory level, two-way per-commit); Phase 3 schema freeze v1.0.0 = canonical one-way door
revisit_trigger: end of Phase 2 (loops running in 3 of 4 repos for 2 sprints with measurable hill-climb) → review whether to tighten discipline or relax
---

# v2 — Cherny Adoption + Tribunal CI Plan

v1's shape was right; reviewers found 7 convergent issues + the Compound and Karpathy notebooks named the missing patterns. v2 folds both in.

## Decisions made (4 defaults that v1 left open)

| # | Decision | v2 default | Why |
|---|---|---|---|
| **D-deadline** | 2026-05-18 trigger | **Moved to 2026-06-01** | 3 reviewers said v1 Phase 0 = theater (hardcoded informational verdict). 14 extra days buys real Phase 0. Trigger reschedule + sign-off required. |
| **D-auth** | PAT_TOKEN scope expansion | **GitHub App `demerzel-tribunal`** with `checks:write` + `pull-requests:write` only | Security flagged ambient `repo:write` PAT × 4 repos as supply-chain crown jewel. GitHub App is per-repo install, fine-grained, audit-logged. |
| **D-runtime** | Path A (build IXQL CLI ~1 week) vs Path B (Python port ~2 days) | **Path B as default**; Path A → Q3 separate project | v1's "~1 week" not credible per Feasibility — inventory said Demerzel has zero IXQL CI runtime today. Real Path A estimate 3-4 weeks. |
| **D-uniformity** | R10 uniform GA-canonical shape across 4 repos | **Per repo**: GA + Demerzel + tars get full shape; ix gets **contracts-only** (frontmatter on existing solutions, no doc moves) | Architect's prior brainstorm said per-repo; v1 contradicted it. ix's Rust inner loop intolerant of 7-phase ceremony. |

All four are reversible — listed in §One-Way Doors with revisit triggers.

## Problem Frame (unchanged shape, abbreviated)

Demerzel orchestrates the tribunal but has **no IXQL CI runtime**. ix produces OPTIC-K SAE artifacts in code only, never via CI (contract test asserts against a fixture frozen from GA = circular). tars is greenfield — 14 of 15 Cherny patterns absent, produces no verdict. All four repos lack PreCompact / `state/digests/` infrastructure (now **GA has it** as of this commit — see §Phase 0 status).

## Cherny + Notebook Patterns (concrete, named)

From the 2026-05-14 NotebookLM drill — replacing v1's abstract framing with concrete pattern names:

| Pattern | Source | What it provides |
|---|---|---|
| **Karpathy 4 Rules CLAUDE.md** | `forrestchang/karpathy-skills` GitHub repo | Drop `CLAUDE.md` in project root — 4 rules: Think before coding (`include.mmd` injection + Plan mode), Simplicity first (PostToolUse formatter + deterministic gates), Surgical changes (`agentproof-react` + `pipeline_runner.go` schema rejection), Goal-driven (`Stop` hook + ralph-wiggum loop) |
| **Protected files hook** | "Compound the Compounding" notebook | Deterministic shell hook in `.claude/settings.json` blocks Edit/Write on declared paths *before execution*. Override marker = string in commit message / task note / approved workflow context. **Closes the loop-PR review-bypass gap.** |
| **Harness Engine** | "Compound the Compounding" notebook | Mutation declares `rollback` / `rollback_metadata` field; `roundtrip-validate/SKILL.md` runs rollback test; `grammar-governor` skill rejects on failure → Engine reverts. **Closes the loop-edit-trajectory one-way-door gap.** |
| **Master-Orchestrator** | "Compound the Compounding" notebook | Single `.claude/skills/compound/SKILL.md` as sole user-facing entrypoint, delegates to hidden subskills + `.claude/agents/` subagents, aggregates via strict structured report. **GA's `/feature` already plays this role.** |
| **Schema-rejection at emit** | Karpathy-skills CLAUDE.md v2 (Forrest Chang fork) | Every AI step declares output schema; runtime rejects mismatched outputs; retry under budget or halt. **Now R11.** |
| **`/digest` + PreCompact** | NotebookLM context-manager design | Session-digest infrastructure. **Shipped today in GA.** Propagate to siblings in Phase 1. |

References for v2 readers:
- "Claude Code 10x Fix: Karpathy's 4 Rules (2026)" video
- "Karpathy-skills CLAUDE.md v2 — extending forrestchang's pattern with lessons from building fixclaw" GitHub Gist
- `ChatGPT-Compounding the Compounding.md` (in user's NotebookLM library)

## Goals (updated from v1)

R1–R10 from v1 unchanged in **shape**, only R1 deadline moved to 2026-06-01.

**New R11.** Every AI-step output (verdict emit, loop commit proposal, /digest invocation, intent-router classification) **declares an output schema and validates against it before persisting**. Schema-mismatched outputs trigger retry under budget or halt. (Closes the "no schema enforcement on AI outputs" gap that Adversarial flagged as unstated assumption #1.)

**New R12.** All four repos enforce a **Karpathy-CLAUDE.md** baseline. ix and Demerzel and tars get the 4 rules dropped into their root CLAUDE.md or appended to existing ones, by end of Phase 1.

**New R13.** Autonomous improvement loops declare an explicit `scope_boundary` and a `protected_paths` exclude list. **Hard refuse** to edit `.github/`, `Scripts/install-git-hooks.*`, `docs/contracts/`, or any path listed in `protected_paths` — enforced via Protected files hook (NOT just observability).

## Current State Matrix (delta from v1 — GA hooks now ✅)

Only changes vs v1's matrix:

| # | Pattern | GA | ix | Demerzel | tars |
|---|---|---|---|---|---|
| 7 | `.claude/settings.json` hooks | ✅ (now incl. PreCompact + SessionStart digest read, 2026-05-14) | 🟡 | ❌ | ❌ |
| 13 | `state/digests/` | ✅ (shipped 2026-05-14) | ❌ | ❌ | ❌ |
| 16 | Karpathy CLAUDE.md (new row) | 🟡 (project CLAUDE.md doesn't yet have the 4 rules verbatim) | ❌ | ❌ | ❌ |
| 17 | Protected files hook (new row) | ❌ | ❌ | ❌ | ❌ |

Rest of matrix from v1 stands.

## Phasing — v2

### Phase 0 — Real unblock, by 2026-06-01

**Deliverables (changed from v1):**

1. **GitHub App `demerzel-tribunal`** — register, install per repo (GA + ix + tars), permissions: `checks:write`, `pull-requests:write` only. **No `contents:write`.** Secret stored as `DEMERZEL_TRIBUNAL_APP_KEY` in Demerzel.
2. **Vendor `qa-verdict.schema.json`** into Demerzel at `Demerzel/schemas/contracts/qa-verdict.schema.json` with `schema_source_commit_sha` field referencing GA's canonical SHA. **CODEOWNERS** on the GA canonical file requires human review on any change. **Vendored copies are never auto-updated** — manual PR per consumer repo.
3. **`Demerzel/.github/workflows/qa-tribunal.yml`** — triggers: `repository_dispatch: [sibling-pr-opened]` + `schedule: cron: '0 9 * * *'`. Job runs a **Python emitter** (Path B per D-runtime) that:
   - Validates dispatch payload (allowlist `repo` ∈ {GA, ix, tars}, `gh api` verify `head_sha` exists in named repo, PR-number cross-check, reject forked-PR runs from secretless job).
   - Reads `client_payload` + git state.
   - Emits a contract-valid verdict to `state/quality/verdicts/<repo>/<pr>/<verdict_id>.json`.
   - Phase 0 verdict body: real `blast_radius` (static analyzer over changed paths) + `informational` verdict + populated `reviewer_chain` (one role: `demerzel.qa-architect-cycle/architecture`). **Not** hardcoded.
4. **`qa-verdict-dispatch.yml`** in GA + ix + tars — `on: pull_request: [opened, synchronize]`. Validates secret then `gh api repos/spareilleux/Demerzel/dispatches -f event_type=sibling-pr-opened ...` with `client_payload={repo, pr, head_sha, base_sha, idempotency_key}`. Idempotency key = `<repo>:<pr>:<head_sha>` to prevent replay-overwrite of stricter verdicts.
5. **Schema-pin all `state/quality/<domain>/baseline.json`** in 4 repos. JSON top-level `_schema: {version: 1, pins: {OPTIC-K: "v1.8", dotnet: "10.0.1xx"}}`. JSONL files get a leading header line.
6. **Branch protection on GA + ix + tars `main`** requires the `qa-architect/tribunal` Check Run. Protected against rename via comment in the workflow file.

**Reversibility:** all two-way except (a) GitHub App install on sibling repos (one-way until App revoked), (b) branch protection rule.

**Done when:** opening a PR on GA, ix, or tars triggers a Demerzel dispatch within 30s, Demerzel writes a contract-valid verdict + Check Run within 5 min, payload validation rejects a deliberately malformed test dispatch.

### Phase 1 — Common Discipline Foundation (week of 2026-06-02)

**Per repo (not uniform per D-uniformity):**

| Deliverable | GA | ix | Demerzel | tars |
|---|---|---|---|---|
| Karpathy 4-rules in CLAUDE.md | append section | append section | append section | append section |
| Doc skeleton (BACKLOG.md, docs/plans/, docs/solutions/, docs/runbooks/, docs/archive/) | ✅ already | **frontmatter-only on existing 12 solutions** (no path moves — preserves inbound links per Feasibility's gap call-out) | full skeleton | full skeleton |
| PreCompact + SessionStart digest hooks | ✅ shipped today | port from GA | port from GA | port from GA |
| Pre-commit installer (`Scripts/install-git-hooks.{ps1,sh}`) | ✅ already | new | new | new |
| `.mcp.json` federation (each repo can call peer MCP servers) | already has ga-dsl + chrome-devtools + others | already 7 servers | add ix + tars | add demerzel |

**Verify task #176** (SessionStart matcher behavior on auto-compact) before relying on mid-session digest read. Fall back to model self-reading `state/digests/latest.md` via Bash on next prompt if matcher:compact doesn't fire.

**Reversibility:** two-way. All deliverables are additive.

**Done when:** all 4 repos have Karpathy CLAUDE.md, all 3 sibling repos have digest hooks, MCP federation works (calling `mcp__ix__*` from Demerzel resolves).

### Phase 2 — Improvement Loops with Harness Engine Discipline (week of 2026-06-09)

**Concrete pattern adoption (the missing piece v1 lacked):**

1. **Protected files hook in `.claude/settings.json`** for every repo running loops. Declared protected:
   - `.github/workflows/*` (loop must not edit its own gate)
   - `Scripts/install-git-hooks.*`
   - `docs/contracts/*`
   - `docs/plans/*` (loop must not edit its scope-boundary contract)
   - per-domain `protected_paths` from the loop's baseline.json
   
   Override marker convention: `[allow-protected: <path>]` in commit subject. Hook script in `Scripts/protected-files-hook.{ps1,sh}` checks against current commit context.

2. **Harness Engine rollback wiring.** Every loop's baseline.json adds:
   ```yaml
   _harness:
     rollback_metadata:
       roundtrip_validator: ".claude/skills/<domain>-roundtrip-validate/SKILL.md"
       reject_on_loss: true
   ```
   Loop's `/auto-optimize` skill refuses to commit unless the roundtrip-validate skill passes.

3. **Per-repo loop onboarding** — sequential, one repo per week per D-uniformity matrix:
   - GA (already running chatbot loop): codify, add Harness wiring, ship Karpathy CLAUDE.md addendum. Already at Phase 2 internally.
   - ix (week of 2026-06-16): `optick-sae-loop.yml` daily 04:00 UTC; oracle script wraps `cargo run -p ix-optick-sae`; baseline at `state/quality/optick-sae/baseline.json` with schema pin OPTIC-K-v1.8; declared `protected_paths = [".github/", "crates/ix-optick-sae/src/lib.rs"]`.
   - Demerzel (week of 2026-06-23): wire `demerzel-self-improvement.yml` to actually execute `qa-architect-cycle` (via Path B Python). `protected_paths = ["pipelines/qa-architect-cycle.ixql", "schemas/contracts/"]`.
   - tars (week of 2026-06-30): `tars-evolve-loop.yml` daily 05:00 UTC; diffs against `v2/baselines/glm-4-9b-q4/*.golden.json`; emits a `qa-verdict.schema.json`-conforming artifact (closing tars's verdict asymmetry).

4. **`/auto-optimize` skill** authored in GA, copied to siblings. Inputs: `oracle_script_path`, `baseline_path`, `domain`. Behavior: acquire `state/quality/<domain>/.lock`, run oracle, propose fix, **invoke Harness rollback validator before commit**, commit if improved + roundtrip-passes, revert otherwise. Mandatory: `max_commits_per_session=50`, `max_wall_clock_minutes=480`, `.STOP` file kill-switch, auto-exit on metric plateau (<0.5% improvement for 5 runs).

5. **Loop-PR review gate (closes the Adversarial+Deployment+Security gap):**
   - Loops open PRs with label `auto-loop`.
   - `Scripts/protected-files-hook` (CI-level) re-runs as a required check on every PR — fails if diff touches paths outside the loop's declared `scope_boundary`.
   - `auto-loop` labeled PRs **must** receive `/octo:review` invocation — wired as a workflow that auto-invokes via Claude Code Action.
   - Branch protection requires both the protected-files check AND the auto-review check to pass.

6. **Ollama-vs-cloud signal measurement** — Phase 2 gate per Adversarial's biggest-unstated-assumption finding. Before promoting any repo's loop to "merges-its-own-PRs," sample 10 PRs through both local-Ollama tribunal and a cloud-LLM control. If Ollama signal degrades >50%, gate stays manual (no auto-merge).

**Reversibility:** **one-way at trajectory level**, two-way per-commit. Loop commits tagged `Co-Authored-By: <repo>-auto-loop <noreply@guitar-alchemist.io>`. `auto-loop-revert` skill performs bulk revert via git log filter.

**Done when:** all 4 repos have at least one CI-scheduled loop running for one week with the protected-files-hook + Harness rollback enforced; loop-PR review gate has rejected ≥1 PR that touched a protected path.

### Phase 3 — Tribunal Phase 1–4 (rolls into existing tribunal plan)

No changes from v1's Phase 3 deliverables beyond:
- Schema-pin coordination now includes the OPTIC-K v1.8 dependency declaration (`depends_on:` field in baselines).
- Cloud-LLM judges still deferred; Ollama-only Phase 1.
- Verdict-as-injection-vector mitigation: every consumer of a verdict body treats `narrative` and free-form fields as **untrusted input** — sanitize/escape before passing to a downstream LLM.

## CI Integration Map (revised)

### Triggers + dispatches

```
GA / ix / tars : on pull_request [opened, synchronize]
   ↓ qa-verdict-dispatch.yml
   ↓ gh api repos/spareilleux/Demerzel/dispatches
   ↓ event_type=sibling-pr-opened
   ↓ client_payload={repo, pr, head_sha, base_sha, idempotency_key}
       ↓
Demerzel : on repository_dispatch [sibling-pr-opened]
   ↓ qa-tribunal.yml — Python emitter (Path B)
   ↓ [validate payload: allowlist repo, gh api verify SHA, PR cross-check, fork-PR isolation]
   ↓ writes state/quality/verdicts/<repo>/<pr>/<verdict_id>.json
   ↓ posts Check Run via GitHub App demerzel-tribunal (NOT a PAT)
       ↓
GA / ix / tars : Check Run blocks merge if verdict=block
```

### Staggered cron grid (per Deployment recommendation)

| Time (UTC) | Repo | Workflow | Phase |
|---|---|---|---|
| 03:00 | ix | `optick-sae-loop.yml` | Phase 2 |
| 04:00 | tars | `tars-evolve-loop.yml` | Phase 2 |
| 05:00 | GA | `chatbot-improvement-loop.yml` | Phase 2 (codified) |
| 06:00 | Demerzel | `qa-tribunal.yml` sweep | Phase 0 stub, Phase 3 real |
| 09:00 | Demerzel | `qa-tribunal.yml` deadline trigger | Phase 0 (date pending sign-off) |

### Artifact paths (canonical) — same as v1

### Secrets (revised)

- **GitHub App `demerzel-tribunal`** — per-repo install; private key in Demerzel secret `DEMERZEL_TRIBUNAL_APP_KEY`; rotation policy = annually or on suspected compromise.
- **`qa-verdict-dispatch`** uses GitHub Actions' default `GITHUB_TOKEN` augmented by a fine-grained PAT scoped only to `repository_dispatch` on Demerzel.
- **No `PAT_TOKEN` with `repo:write` anywhere** beyond what already existed pre-plan.

## Security Model (new section — addresses all 7 Security findings)

| Surface | Threat | Mitigation |
|---|---|---|
| Auth | PAT crown-jewel compromise | GitHub App per-repo, `checks:write` + `pull-requests:write` only, no `contents:write`, audit-logged, annual rotation |
| Dispatch payload | Forged `client_payload`, SHA confusion, replay | Allowlist source repo, `gh api` SHA-existence check, PR-number cross-check, idempotency_key prevents replay, fork-PR isolation (`pull_request_target` not used; forks run secretless) |
| Verdict body | Prompt-injection of downstream consumers | Treat `narrative` + free-form fields as untrusted; sanitize before LLM ingest; no auto-tool-chain on unsanitized verdict content |
| Loop authorization | Loop edits its own gate or unauthorized paths | Protected files hook enforces at write time (not just observability); `protected_paths` declared in baseline; CI check re-validates pre-merge |
| Schema integrity | Malicious widening of canonical schema | CODEOWNERS on GA `docs/contracts/qa-verdict.schema.json`; vendored copies never auto-update; `governance-validate.yml` fails closed on SHA mismatch |
| Digest privacy | Credentials in `state/digests/` | gitignored in all 4 repos (GA done); `Scripts/precompact-digest.ps1` includes a credential-scrubber regex pass for common patterns (API keys, AWS/Azure tokens) before write |
| MCP federation | Compromised server reaches across repos | Each `.mcp.json` declares explicit `allowedTools: [...]` per sibling server, default-deny; transitive trust not assumed |

## Instrumentation (same as v1, R11 added)

| Metric | Baseline | Direction | Guardrail |
|---|---|---|---|
| Verdict latency P95 | new | ≤ 10 min by Phase 3 | hard fail at 15 min |
| Schema-rejection rate (R11) | new | < 5% of AI outputs | review prompts if > 15% |
| % PRs receiving verdict | 0% | ≥ 95% by Phase 3 | < 80% audit workflow |
| Tokens-to-warm post-compaction | measure pre-Phase-1 | ↓ 50% | review hook plumbing |
| Loop commits/day per repo | 0 | 1–10 | > 25 review budget |
| Loop-PR scope_boundary violations caught | new | trends ↓ over time | spike = scope_boundary too narrow |
| Ollama-vs-cloud signal delta (sampled monthly) | measure first 10 PRs | within 50% | gate stays manual if exceeded |

## One-Way Doors

- **D1** (named in tribunal plan). QA Verdict schema v1.0.0 freeze (end of Phase 3 + 2-sprint soak).
- **D2** (named). Producer slug registry.
- **D3** (v2). **D-runtime: Path B is default; switching to Path A requires deprecating Path B.** Revisit trigger: Q3-2026 review of IXQL CI maturity.
- **D4** (v2). Loop edit trajectory — per-commit reversible, campaign reversible only via `auto-loop-revert` skill. Mitigation: `Co-Authored-By: <repo>-auto-loop` tagging.
- **D5** (v2). Schema-pin format `_schema: {version: 1, pins: {...}}` canonical for `state/quality/`. Changing field name breaks every loop.
- **D6** (v2, new). **GitHub App `demerzel-tribunal` installation across 3 sibling repos.** Removing the App requires revoking installation per repo. Revisit trigger: any security finding that mandates token rotation > weekly cadence.

Two-way: per-repo doc skeleton, hooks (already two-way per design), digest schema (versioned), MCP federation, loop cron timing, Karpathy CLAUDE.md addendum.

## Risks (delta from v1)

| Risk | v1 status | v2 update |
|---|---|---|
| R1 Demerzel no IXQL runtime by 5/18 | Critical | **Resolved** — D-deadline moved to 6/01, D-runtime committed to Path B Python emitter. R1 retired. |
| R2 PAT compromise | High | **Reframed** — D6 GitHub App replaces PAT; R2 now "GitHub App key compromise" with annual rotation + revocation runbook. |
| R3 Schema drift between vendored copies | High | **Hardened** — CODEOWNERS + manual vendoring + fail-closed mismatch check. |
| R4 Loop edits compound | High | **Hardened** — Protected files hook + Harness rollback validator + `auto-loop-revert` skill. |
| R5 Verdict noise | Medium | unchanged |
| R6 Cross-repo `state/quality/` races | Medium | **Hardened** — per-domain `.lock` + `depends_on:` cascade invalidation + schema-pin refusal. |
| R7 GSD scope-reduction detector skipped wrong | Low | Folded into Phase 2 deliverable #5 (loop-PR review gate). |
| R8 PreCompact unused (matcher uncertainty) | Medium | **Partially resolved** — GA digest hooks shipped 2026-05-14 and verified end-to-end; task #176 still verifies the `matcher:compact` edge case. |
| R9 Harness underweighting | Medium | **Resolved by notebook patterns** — discipline (gates, schemas, contracts) is organizational structure (C7); harness is in-conversation tooling. Cherny says one gets less important, not the other. |
| R10 Tribunal cost | Medium | unchanged |
| R11 (v2 new) Karpathy 4-rules CLAUDE.md addendum collides with existing CLAUDE.md guidance | Low | Mitigation: append the 4 rules to existing CLAUDE.md as a final section, not at the top. |

## Open Questions (smaller list — most decided)

- **Q1.** Override marker syntax for the Protected files hook — `[allow-protected: <path>]` in commit subject, or a `.protected-override` file in the PR? Default: commit-subject marker. Decide before Phase 2.
- **Q2.** Cross-repo schema canonical source — still in GA's `docs/contracts/`. Defer hub/spoke contract repo. v1's Q3 punt stands.
- **Q3.** Loop-PR auto-merge threshold — at what signal-delta confidence does auto-merge unlock? Phase 2 measurement task names the gate; the threshold itself is open until data lands.

## Sign-off Required Before Phase 0

1. **Confirm D-deadline 2026-06-01** (move trigger via `gh schedule`).
2. **Authorize GitHub App `demerzel-tribunal` registration** + install on GA + ix + tars (D6).
3. **Confirm D-runtime = Path B** (Python emitter); Path A as separate Q3 project.
4. **Confirm D-uniformity** (per-repo, ix gets contracts-only).
5. **Confirm Q1** (override marker syntax).
6. **Confirm `Co-Authored-By: <repo>-auto-loop <noreply@guitar-alchemist.io>` tag format** for loop commits.

## Phase 0 status as of this commit (2026-05-14)

- ✅ Plan v2 drafted (this file).
- ✅ Two memory references saved: `feedback_chrome_never_edge`, `reference_karpathy_4_rules_skills`, `reference_compound_protected_files_hook`.
- ✅ Session-digest infrastructure shipped in GA: `Scripts/precompact-digest.ps1`, `Scripts/sessionstart-digest.ps1`, `.claude/skills/digest/SKILL.md`, `state/digests/{README.md, archive/}`, hooks wired in `.claude/settings.json`, `.gitignore` updated. Validated end-to-end with a test dispatch.
- ✅ v1 marked `superseded_by` this file.
- ⏳ Sign-off on the 6 items above.
- ⏳ Phase 0 implementation (GitHub App + dispatch workflows + Python emitter + schema-pin baselines) starts after sign-off.
