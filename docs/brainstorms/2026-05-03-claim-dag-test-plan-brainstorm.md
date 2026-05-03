---
title: "Claim-DAG test plan — AI-friendly verification graph"
type: brainstorm
status: open (not committed to a plan; awaits June 8 hire-review agent's consideration)
date: 2026-05-03
origin: end-of-day chat 2026-05-03 — exploring whether the QA Architect Tribunal should produce formal test plans
companion_plans:
  - docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md (Phase 3+ candidate scope)
  - docs/contracts/2026-05-02-qa-verdict.contract.md (current verdict shape)
audience: future-self, the June 8 hire-review agent (trig_01G2dmhrbof4MnAYg1NTkXku)
---

# Claim-DAG test plan — AI-friendly verification graph

## Problem frame

The QA Architect Tribunal verifies PRs reactively. It scores blast radius, runs invariants, gathers evidence — but the *unit of verification* is a whole verdict, and the protocol's `evidence[]` array is a flat list. Three real shortcomings fall out of that:

1. **Per-diff test selection is ad-hoc.** A PR touching `Common/GA.Business.ML/Search/` should re-verify any claim downstream of voicing-search; today the tribunal has no graph to consult, so it falls back to "run everything that pattern-matches the path."
2. **No caching of partial verifications.** If yesterday's verdict proved "OPTIC-K dim invariant holds at SHA X," and today's PR doesn't touch the embedding schema, the proof should carry over. Currently every verdict re-derives.
3. **No composability.** Saying "PR satisfies acceptance criterion C" requires bundling evidence by hand. Plans declare "Done When" prose but the tribunal can't verify against it.

A flat document — formal test plan or otherwise — doesn't fix these. A **directed graph of small, AI-tractable claim nodes** does.

## The core idea

Replace flat test plans with a DAG where:

- **Nodes** are *claims*: machine-readable assertions about the system, each small enough to fit in one LLM context window.
- **Edges** are *requires*: claim A's truth depends on claim B's truth. Reverse direction = "if B breaks, A is suspect."
- **Roots** are top-level acceptance criteria from `docs/plans/*.md` (one root per "Done When" bullet, ideally).
- **Leaves** are concrete verifications: a passing `test_run`, a passing `contract_check`, a `manual_note` about a reviewed fact.

The tribunal walks the DAG instead of a flat list:

| Question | Traversal | Cost |
|---|---|---|
| What claims hold at SHA X? | bottom-up from leaves whose evidence is fresh | linear in fresh leaves |
| Is acceptance criterion C met? | top-down from root C, recurse into requirements | sub-tree size |
| What's at risk from this diff? | filter by `applies_when.changed_paths`, take dependency closure | sub-linear with caching |
| Which reviewer (in the tribunal) sees what? | partition affected sub-graph among roles | parallelizable |

## Node schema (sketch)

```json
{
  "node_id": "optick-sae.phase1.aux-loss-enables-guardrail",
  "kind": "behavioral_assertion",
  "depends_on": [
    "optick-sae.phase1.trainer-runs-end-to-end",
    "optick-sae.contract.guardrails-enforced"
  ],
  "applies_when": {
    "changed_paths": [
      "Scripts/optick_sae_train.py",
      "ix/crates/ix-optick-sae/**"
    ]
  },
  "claim": "With aux_alpha >= 0.1, dead_features_pct stays under 30% on real OPTIC-K v1.8 corpus",
  "evidence_required": [
    {"kind": "test_run", "name": "test_aux_loss_brings_dead_under_30"},
    {"kind": "manual_note", "name": "docs/learnings/2026-05-03-optick-sae-vanilla-topk-dead-features.md"}
  ],
  "verifier": "qa_verify_acceptance",
  "tags": ["sae", "guardrail", "phase1"],
  "last_verified_sha": null,
  "last_verified_verdict_id": null
}
```

`kind` enumeration:
- `acceptance_criterion` — root-level, mirrors a "Done When" bullet
- `behavioral_assertion` — composite claim about behavior across a sub-system
- `invariant` — must always hold (5-layer rule, OPTIC-K dim, schema-locked contracts)
- `regression_guard` — bound to a past defect; corresponds to a defect-memory entry
- `evidence_leaf` — concrete pass/fail (test_run, contract_check, manual_note)

## How it composes with the existing tribunal

The verdict shape barely changes. A claim DAG reduces to "graph of mini-verdicts," and the existing protocol covers it:

| Today's verdict field | DAG equivalent |
|---|---|
| `evidence[]` | per-leaf verifications |
| `followups[]` | unsatisfied internal nodes |
| `reviewer_chain[]` | the partition of the affected sub-graph each reviewer covered |
| `blast_radius.components_reached` | the `applies_when.changed_paths` matched set |
| `risk_tier` (P0–P3) | severity of the highest unsatisfied root |

So existing consumers (`qa_score_quality_drift`, GitHub Actions PR comment, Graphiti defect memory) keep working. The DAG is an internal organizing principle that produces the same external shape.

## Hard problems + mitigations

**1. Cold start.** Who writes the initial DAG? Honest answer: the tribunal does, by reading existing plans + tests + the codebase and proposing claim nodes. Humans review/edit, but AI-native authoring is the only sustainable path. A bootstrap sub-agent: "scan repo, propose 50 root acceptance_criteria + their decomposition into 200 leaves." Manual seed = doesn't happen.

**2. Drift.** Nodes rot like docs. Mitigations:
   - Each node carries `last_verified_sha`. The tribunal warns when a node hasn't been re-verified in N PRs that match its `applies_when`.
   - Per-node staleness budget surfaces in verdicts as P3 (informational) followups.
   - The June 8 hire-review agent gets a "DAG health" sub-task.

**3. Evidence ambiguity.** What makes "this `test_run` satisfies this `claim`"? Two paths:
   - **Explicit name binding** (brittle): test name must match `evidence_required[].name` literally.
   - **LLM judgment** (cost): a reviewer agent reads the test + claim and judges. Cacheable with the test+claim hash.
   Probably both — exact-match for cheap leaves, LLM for fuzzy ones. Tag each leaf with which mode applies.

**4. Over-ceremony risk.** If every PR has to update the DAG, it becomes the new test-plan-drift problem. Mitigation: PRs don't *write* nodes; the tribunal proposes node updates as part of its verdict, the PR author reviews/accepts. Keeps authoring out of the human's path.

**5. Authoring quality.** AI-authored claim nodes might be vacuous ("the code does what it does") or wrong. Mitigation: every new node goes through the tribunal's existing multi-LLM review with a "claim quality" persona. Claims are themselves diff content.

## Where this fits

**Not Phase 1 or 2.** Phase 1 (May 18) wires real evidence producers. Phase 2 (week of May 26) wires drift integration. The DAG would be **Phase 3+**, candidate replacing or extending "feature interpretation."

**Sized:** ~3 weeks of work post-Phase-2:
- Week A: schema + storage layout (`docs/test-plan/*.json` or one big DAG file?), bootstrap-from-repo sub-agent
- Week B: traversal engine + CI integration (PR comment shows affected sub-graph)
- Week C: caching layer + drift surfacing

**Decision dependency:** wait for Phase 1 verdict feed (May 18 → ~3 weeks of real data) before committing. The June 8 hire-review agent has the standing to recommend this as Phase 3 scope or defer.

## Open questions

- **Q1.** Storage: one large `docs/test-plan/dag.json` (atomic queries, hard to diff) vs many small `docs/test-plan/<node_id>.json` (PR-friendly, traversal needs assembly). Lean toward many-small + a generated index.
- **Q2.** Should claim nodes live in GA, or in their own `guitar-alchemist/test-plan` repo for true cross-repo neutrality (so ix and Demerzel can also contribute claims)?
- **Q3.** Cycle prevention: what stops claim authors from creating dependency cycles? Probably an automated check in the contract validator + a CI gate.
- **Q4.** Backwards compatibility: when a node is renamed, do dependent nodes break? Need stable ids + a redirect mechanism.
- **Q5.** What's the right granularity? Too coarse (10 nodes total) misses the per-diff selection benefit. Too fine (10,000 nodes) hits maintenance hell. Educated guess: 200–800 nodes for a project this size.
- **Q6.** Does sae-lens / similar tooling have a precedent we can borrow? Bazel's test target DAG is the closest existing pattern; behavior trees in robotics are conceptually similar.

## Reference implementations to study

- **Bazel test target DAG** — affected-paths test selection. Closest production-grade precedent.
- **Behavior trees (game AI / robotics)** — tick-based DAG traversal with success/failure propagation.
- **Coq / Lean proof DAGs** — formal verification, every claim has explicit proof obligations. Heavy but principled.
- **Argo Workflows / Airflow** — DAG runtime, retries, partial replays. Operational pattern but for compute, not verification.

## Next-step decision triggers

Reasons to escalate this from brainstorm to plan:

- ✅ Phase 1 verdict feed produces real data showing the tribunal needs better organizational structure (likely: too many `followups[]`, no good way to dedupe across PRs)
- ✅ June 8 hire-review agent recommends "the gap is verification structure, not new agents"
- ✅ A second domain emerges where Phase 0 plans + verdict feed alone isn't enough (e.g., sibling repo wants to plug into same tribunal)

Reasons NOT to escalate:

- ❌ Phase 2 drift integration delivers enough signal that the tribunal feels complete
- ❌ Authoring quality (problem #5) proves intractable in cold-start experiments
- ❌ Bazel-style test selection turns out to solve 80% of the value with 20% of the structure (worth measuring)

## What this brainstorm is NOT

- ❌ A commitment to build it
- ❌ A replacement for the existing `qa_propose_tests` MCP primitive
- ❌ Formal test plans in the document-genre sense — those are explicitly rejected (see chat 2026-05-03 EOD)
- ❌ Scoped for nightly auto-implementation; this needs human deliberation post-Phase-1

The June 8 hire-review agent should treat this as one of several candidate Phase-3 directions, not the default.
