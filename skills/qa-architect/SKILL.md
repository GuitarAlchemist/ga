---
Name: "qa-architect"
Description: "How any AI collaborator (Codex, Conductor, Octopus, Compound Engineering, future-Claude) works alongside the QA Architect agent on this repo. Read before designing or shipping a non-trivial change."
Triggers:
  # "qa" was dropped — too short under SkillMdParser.MinTriggerLength (would
  # otherwise be silently filtered at parse time and create a confusing
  # state). The longer triggers below cover every real use case.
  - "verdict"
  - "qa-architect"
  - "tribunal"
  - "blast radius"
  - "invariant"
  - "regression"
  - "quality drift"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.3.0"
metadata:
  triggers:
    # "qa" intentionally omitted — see top-level Triggers comment.
    - verdict
    - qa-architect
    - tribunal
    - blast radius
    - invariant
    - regression
    - quality drift
  evidence-kinds:
    - contract_check
    - adversarial_replay
    - quality_snapshot
  contract:
    path: docs/contracts/2026-05-02-qa-verdict.contract.md
    schema: docs/contracts/qa-verdict.schema.json
    schema-version: "0.1.0"
allowed-tools:
  - qa_assess_blast_radius
  - qa_gap_analyze
  - qa_propose_tests
  - qa_verify_invariants
  - qa_replay_adversarial
  - qa_score_quality_drift
  - qa_lookup_defect_memory
  - qa_emit_verdict
---

# QA Architect Skill

This is the rubric for collaborating with Guitar Alchemist's senior QA agent. Read it before opening a PR, proposing a refactor, or shipping a metric-moving change. It is a checklist, not a narrative — follow it in order.

## 1. Who the QA Engineer Is

Two surfaces of the same role:

- **`QAArchitectAgent`** — `Common/GA.Business.ML/Agents/QAArchitectAgent.cs`. Synchronous, in-process, callable from any C# code. Returns a `QaVerdict`.
- **`qa-architect-cycle.ixql`** — `Demerzel/pipelines/qa-architect-cycle.ixql`. Long-running, governance-grade, runs on PR open/sync and on a daily 06:00 UTC sweep. Persists verdicts to `state/quality/verdicts/`.

They both speak the same protocol: the `QaVerdict` contract.

## 2. The Contract Is Not Optional

- Source of truth: `docs/contracts/2026-05-02-qa-verdict.contract.md` (see also `references/qa-verdict-contract.md` bundled with this skill).
- JSON Schema: `docs/contracts/qa-verdict.schema.json`.
- Schema is **draft v0.1.0**, will freeze at v1.0.0 after a 2-sprint soak. Adding a new evidence kind, risk tier, or producer slug requires amending the contract markdown AND bumping the schema, not silently extending the JSON.

## 3. The 8 MCP Primitives

Available from the `GaQaMcp` MCP server. Treat them as your hands — call them rather than re-implementing.

| Tool | Call when… | Returns |
|---|---|---|
| `qa_assess_blast_radius` | Before designing a non-trivial change | `blast_radius` object: layers touched, one-way doors crossed, invariants at risk, score 0..1 |
| `qa_gap_analyze` | After writing code, before opening a PR | Array of `followup` candidates surfacing untested critical paths |
| `qa_propose_tests` | When adding new components or breaking out abstractions | Array of test stubs (path + body) grounded in repo patterns |
| `qa_verify_invariants` | Before merging changes that touch Core / Domain / Analysis / AI/ML | `evidence{kind: contract_check}` items, one per invariant |
| `qa_replay_adversarial` | Before merging changes to chord/voicing/embedding pipelines | `evidence{kind: adversarial_replay}` |
| `qa_score_quality_drift` | When changes may move a metric in `state/quality/*.json` | `evidence{kind: quality_snapshot}` with delta + guardrail check |
| `qa_lookup_defect_memory` | Before designing — surfaces "we got burned by this before" | Array of past followups matching the touched components |
| `qa_emit_verdict` | At the end, to persist a verdict | Path of the persisted JSON + `verdict_id` |

## 4. Pre-PR Rubric

Run this before the PR is opened. Skip steps only when you can name the reason.

1. `qa_lookup_defect_memory` over the components you'll touch. If a past followup matches, your change must address it explicitly or document why it doesn't apply.
2. `qa_assess_blast_radius` on the diff. If `estimated_blast_score >= 0.4`, expand testing per WP-2 of the plan: add property tests, fuzz inputs, or contract tests across the boundary you're crossing.
3. `qa_verify_invariants` for the layer you're changing. The 5-layer dependency rule and the OPTIC-K dim invariant are both checked here.
4. `qa_propose_tests` for any new component. Use the proposals as starting scaffolds, not as final tests.
5. `qa_gap_analyze` last. Address `must_fix` candidates before opening the PR.

## 5. How to Read a Verdict

The verdict you'll see in PR comments and at `state/quality/verdicts/`. Read in this order:

1. **`risk_tier`** — what action is required.
   - `P0` → **stop**. Don't merge. Fix every `must_fix` followup, re-request review.
   - `P1` → must-fix is required before next release. Open a tracked issue if you cannot fix in this PR.
   - `P2` → should-fix. Address inline or annotate why deferred.
   - `P3` → informational only. No action required, but read it.
2. **`verdict`** — what gating is enforced.
   - `block` → branch protection blocks merge. Pair with P0.
   - `merge_with_followups` → mergeable; followups are tracked.
   - `pass` → no must-fix items.
   - `informational` → scheduled sweep or exploratory; not gating.
3. **`reviewer_chain`** — who objected and why. If a single specialist role gave a low `score`, that's where the risk concentrates.
4. **`followups[].rationale`** — the *why* of each item. If the rationale references a past incident or contract ID, take it literally.
5. **`blast_radius.one_way_doors_crossed`** — non-empty here forces P0. These crossings require human sign-off, not a test fix.
6. **`narrative`** — the human-readable summary, max 500 chars. Skim if you trust the structured fields; read closely if you don't.

## 6. What to Escalate to a Human

You do not have authority to:

- Modify the QA verdict schema (any field, enum, or required-list change). Amend the contract markdown via PR with a `schema_version` bump proposal.
- Add or rename a producer slug.
- Change a `risk_tier` rollup rule (e.g. demote a P0 to P1).
- Cross a one-way door listed in any active plan under `docs/plans/`.
- Disable a verdict producer or skip the QA gate "just for this PR".

Surface these to the human in the PR description with the heading `Requires QA sign-off:` and a one-paragraph justification.

## 7. Cross-Reference

- Plan: `docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md`
- Contract: `docs/contracts/2026-05-02-qa-verdict.contract.md`
- Schema: `docs/contracts/qa-verdict.schema.json`
- Agent: `Common/GA.Business.ML/Agents/QAArchitectAgent.cs`
- Pipeline: `Demerzel/pipelines/qa-architect-cycle.ixql` (sibling repo)
- MCP server: `Apps/GaQaMcp/`
