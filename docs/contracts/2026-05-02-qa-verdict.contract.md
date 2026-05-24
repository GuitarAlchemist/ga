# QA Verdict — Cross-Repo Contract

**Version:** 0.1.0 (draft, pending sign-off)
**Schema version:** 1
**Status:** Draft (Phase 0 of `qa-architect-tribunal` plan, 2026-05-02)
**Producers:** `Demerzel/pipelines/qa-architect-cycle.ixql`, `GA.Business.ML.Agents.QAArchitectAgent`, sibling tribunal agents (`ix-adversarial-runner`, `tars-test-designer`, GA `CriticAgent`)
**Consumers:** GitHub Actions PR comment poster, `state/quality/verdicts/*.json` time-series writer, Demerzel algedonic monitor, ce-code-review aggregator
**Companion projects:** `Apps/GaQaMcp/` (Option C — exposes verdict primitives over MCP), `Common/GA.Business.ML/Agents/QAArchitectAgent.cs` (Option B — local execution arm)

---

## 1. Why This Contract Exists

A senior QA engineer's verdict has to travel between repos with no information loss. Today the relevant signals live in four places that don't speak the same shape:

- GA test runs (`xUnit` + `GA.Testing.Semantic` basin scores)
- `state/quality/voicing-analysis/*.json` daily snapshots
- ix `tests/adversarial/` replay outcomes
- Demerzel pipeline outputs (`weakness-probe.ixql`, `governance-shake-test.ixql`)

If the QA Architect agent and its tribunal each emit free-form text, downstream automation (PR gating, regression triage, defect knowledge graph) has to re-parse every time. This contract pins the verdict shape so producers in any language (C#, F#, IXQL, Rust, Python) can write to the same surface, and consumers can trust the fields without bespoke glue.

The contract is a **one-way door**: every consumer encodes the field names, enum values, and severity tiers. Renaming `risk_tier` or adding a fifth tier requires a coordinated bump (`schema_version` 2) and migration of `state/quality/verdicts/`. Treat additions as additive and optional until v2.

---

## 2. JSON Shape

```json
{
  "schema_version": 1,
  "verdict_id": "2026-05-02T14-32-11Z-pr-1234-qa-architect-cycle",
  "produced_at": "2026-05-02T14:32:11Z",
  "producer": "qa-architect-cycle",
  "producer_version": "0.1.0",
  "target": {
    "kind": "pull_request",
    "repo": "guitar-alchemist/ga",
    "ref": "pr/1234",
    "sha": "81870922abc...",
    "base_sha": "a6d8c496def..."
  },
  "risk_tier": "P1",
  "verdict": "merge_with_followups",
  "blast_radius": {
    "layers_touched": ["domain", "analysis", "ai_ml"],
    "one_way_doors_crossed": [],
    "invariants_at_risk": ["optick.dim=240", "five-layer.bottom-up"],
    "components_reached": [
      "Common/GA.Business.Core/Analysis/...",
      "Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs"
    ],
    "estimated_blast_score": 0.62
  },
  "evidence": [
    {
      "kind": "test_run",
      "name": "GA.Business.Core.Tests",
      "outcome": "pass",
      "url": "https://github.com/.../actions/runs/123",
      "delta_from_baseline": null
    },
    {
      "kind": "semantic_basin",
      "name": "voicing-analysis.persona_adherence",
      "score": 0.87,
      "baseline": 0.89,
      "guardrail_min": 0.80,
      "delta_from_baseline": -0.02
    },
    {
      "kind": "quality_snapshot",
      "name": "state/quality/voicing-analysis/2026-05-02.json",
      "drift_summary": "p50 latency +3ms, recall@10 unchanged"
    },
    {
      "kind": "adversarial_replay",
      "name": "ix.tests.adversarial.transposition_invariance",
      "outcome": "pass",
      "url": "ix://runs/2026-05-02/..."
    }
  ],
  "followups": [
    {
      "id": "f1",
      "severity": "must_fix",
      "title": "VoicingAgent missing null-guard on empty corpus",
      "rationale": "Reproduced via fuzz_inputs; throws NRE on first call after re-index.",
      "location": "Common/GA.Business.ML/Agents/VoicingAgent.cs:142",
      "proposed_test": "Tests/Common/GA.Business.ML.Tests/VoicingAgentEmptyCorpusTests.cs",
      "blocks_merge": false
    }
  ],
  "reviewer_chain": [
    {"agent": "ga.QAArchitectAgent", "role": "blast_radius", "score": 0.62},
    {"agent": "ga.CriticAgent", "role": "semantic_judge", "score": 0.87},
    {"agent": "ix.adversarial-runner", "role": "regression_replay", "score": 1.0},
    {"agent": "tars.test-designer", "role": "gap_analysis", "score": 0.74}
  ],
  "narrative": "Diff touches three layers and brushes the OPTIC-K dim invariant without crossing it. Semantic adherence dipped 0.02 — within guardrail but worth a watch. One must-fix on null handling; otherwise mergeable.",
  "links": {
    "pr": "https://github.com/guitar-alchemist/ga/pull/1234",
    "plan": "docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md",
    "knowledge_graph_node": "graphiti://verdicts/2026-05-02/pr-1234"
  }
}
```

---

## 3. Field Reference

### Top-level

| Field | Type | Required | Notes |
|---|---|---|---|
| `schema_version` | int | yes | `1` for v0.1.x. Bump only with coordinated migration. |
| `verdict_id` | string | yes | Stable, sortable, **filename-safe**. Pattern: `<iso8601-basic>-<target_kind>-<short_id>-<producer>`, where the timestamp uses dashes instead of colons (e.g. `2026-05-02T14-32-11Z`). The id is used as a path segment under `state/quality/verdicts/`; colons would break Windows paths. |
| `produced_at` | RFC3339 string | yes | UTC. |
| `producer` | string | yes | Pipeline / agent slug. Allowed: `qa-architect-cycle`, `qa-architect-agent`, `ix-adversarial-runner`, `tars-test-designer`, `ce-code-review`, `manual`. New producers added by PR amending this contract. |
| `producer_version` | semver string | yes | Of the producer code. |
| `target` | object | yes | What was assessed. See §3.1. |
| `risk_tier` | enum | yes | `P0` \| `P1` \| `P2` \| `P3`. See §3.2. |
| `verdict` | enum | yes | `block` \| `merge_with_followups` \| `pass` \| `informational`. See §3.3. |
| `blast_radius` | object | yes | Architectural reach. See §3.4. |
| `evidence` | array | yes | One or more evidence objects. See §3.5. |
| `followups` | array | yes (may be empty) | Concrete next actions. See §3.6. |
| `reviewer_chain` | array | yes | Agents/personas that contributed. See §3.7. |
| `narrative` | string | yes | ≤ 500 chars. Human-readable summary; the *why*, not a re-stating of fields. |
| `links` | object | optional | Free-form pointers. Reserved keys: `pr`, `plan`, `knowledge_graph_node`. |

### 3.1 `target`

| Field | Type | Notes |
|---|---|---|
| `kind` | enum | `pull_request` \| `commit` \| `branch` \| `release` \| `quality_snapshot` \| `scheduled_sweep`. |
| `repo` | string | `org/name` form. |
| `ref` | string | PR number, branch name, or snapshot date. |
| `sha` | string | Commit SHA at time of assessment. |
| `base_sha` | string | For diffs; null for snapshot/sweep targets. |

### 3.2 `risk_tier`

- **P0 — Block.** Invariant violated (5-layer rule, OPTIC-K dim, schema-locked contract), one-way door crossed without sign-off, public API breakage, semantic basin failure on user-facing surface, P0 security finding.
- **P1 — Must-fix before next release.** Critical-path coverage gap, quality-snapshot regression beyond guardrail, contract drift between sibling repos.
- **P2 — Should-fix.** Coverage gaps off the critical path, naming/style, dead code, test brittleness.
- **P3 — Informational.** Observation only — trend notes, telemetry artifacts, "watch this metric."

A verdict's `risk_tier` is the **maximum** severity across its `followups`. A verdict with no followups is `P3`.

### 3.3 `verdict`

- `block` — must be `risk_tier: P0`. Producer asserts the change must not merge in current form.
- `merge_with_followups` — `risk_tier: P1` or `P2`. Mergeable; followups are tracked.
- `pass` — `risk_tier: P2` or `P3`. No must-fix items.
- `informational` — `risk_tier: P3`. Used for scheduled sweeps, snapshot triage, exploratory reports — not gating.

### 3.4 `blast_radius`

| Field | Type | Notes |
|---|---|---|
| `layers_touched` | array<enum> | Subset of `core`, `domain`, `analysis`, `ai_ml`, `orchestration`, `apps`, `frontend`, `infra`, `docs`. |
| `one_way_doors_crossed` | array<string> | Doors crossed *without* documented sign-off. Empty array is fine; presence forces `P0`. |
| `invariants_at_risk` | array<string> | Stable IDs from `docs/contracts/` and CLAUDE.md. Examples: `optick.dim=240`, `optick.schema=OPTIC-K-v1.8`, `five-layer.bottom-up`, `optick.weights.simplex`. |
| `components_reached` | array<string> | Repo-relative paths or namespaces. |
| `estimated_blast_score` | float [0..1] | Producer's heuristic. Score ≥ 0.7 means "wide-reach diff" — used for sampling and prioritization. Score is **advisory**, not gating. |

### 3.5 `evidence`

Each evidence item:

| Field | Type | Notes |
|---|---|---|
| `kind` | enum | `test_run` \| `semantic_basin` \| `quality_snapshot` \| `adversarial_replay` \| `static_analysis` \| `screenshot_diff` \| `mutation_test` \| `contract_check` \| `manual_note`. |
| `name` | string | Test ID, basin name, snapshot path, etc. |
| `outcome` | enum | `pass` \| `fail` \| `flaky` \| `skipped` \| `n/a`. Required for `test_run` and `adversarial_replay`. |
| `score` | float | For `semantic_basin`, `mutation_test`. |
| `baseline` | float | Prior score for delta computation. |
| `guardrail_min` / `guardrail_max` | float | Pre-declared guardrail; breach forces ≥ `P1`. |
| `delta_from_baseline` | float | Signed delta. |
| `url` | string | Pointer to artifact. |
| `drift_summary` | string | Free-form for `quality_snapshot`. |

### 3.6 `followups`

| Field | Type | Notes |
|---|---|---|
| `id` | string | Stable within the verdict. |
| `severity` | enum | `must_fix` \| `should_fix` \| `nice_to_have` \| `info`. Maps to `P0`/`P1`/`P2`/`P3` for tier rollup. |
| `title` | string | ≤ 80 chars. |
| `rationale` | string | Why this matters. Reference incident memory or contract IDs when applicable. |
| `location` | string | `path:line` or `namespace.Type.Member`. |
| `proposed_test` | string | Optional. Path to a test that would catch regressions. |
| `blocks_merge` | bool | Mirrors `severity == must_fix && verdict == block`. Explicit so consumers don't re-derive. |

### 3.7 `reviewer_chain`

Each entry: `{"agent": <slug>, "role": <enum>, "score": <float?>, "notes": <string?>}`. Roles: `blast_radius`, `semantic_judge`, `regression_replay`, `gap_analysis`, `contract_audit`, `accessibility`, `performance`, `security`, `architecture`, `human`. Order is the order contributions were aggregated.

---

## 4. Storage & Lifecycle

- **Per-PR verdicts** are posted as a PR comment by `qa-architect-cycle` and stored at `state/quality/verdicts/<repo>/<pr>/<verdict_id>.json`.
- **Scheduled-sweep verdicts** (snapshot drift triage) land at `state/quality/verdicts/sweeps/<date>/<verdict_id>.json`.
- **Defect knowledge graph**: each `followup` becomes a node in Graphiti (`Tests/Common/GA.Business.Core.Graphiti.Tests` already wires this). Edge: `verdict --produced--> followup --reaches--> component`.
- Verdicts are immutable. Re-assessment after a fix produces a *new* verdict referencing the prior `verdict_id` via `links.supersedes`.

---

## 5. Producer Obligations

Every producer MUST:

1. Validate against the JSON shape before emit (schema file: `docs/contracts/qa-verdict.schema.json` — to be generated from this contract in Phase 0).
2. Set `producer` to one of the registered slugs.
3. Populate `reviewer_chain` with at least itself.
4. Set `risk_tier` consistent with `followups` rollup (highest severity wins).
5. Set `verdict == block` only when at least one followup has `severity == must_fix` AND `blocks_merge == true`.

Every consumer SHOULD treat unknown enum values as the most-conservative bucket (e.g., unknown `risk_tier` → `P0`, unknown `verdict` → `block`) and surface the unknown value to a human.

---

## 6. Open Questions (resolve before v1.0.0)

- **Q1.** Do we need a `confidence` field on the top-level verdict (separate from per-evidence scores)?
- **Q2.** Should `narrative` support markdown, or stay plain text for cross-tool portability?
- **Q3.** Where does the canonical schema file live — GA repo (current) or a new `guitar-alchemist/contracts` repo for true cross-repo neutrality?
- **Q4.** Algedonic integration: does Demerzel's algedonic monitor consume the verdict directly, or does it consume a derived signal? (Likely derived — verdict is too high-bandwidth for algedonics.)
- **Q5.** PII / secret-leak detection — is that a separate evidence kind or a followup severity escalation?

---

## 7. Versioning

- v0.1.x — draft, may break.
- v1.0.0 — first frozen schema. Requires sign-off from GA, ix, Demerzel maintainers.
- v1.x — additive only (new optional fields, new enum variants treated conservatively by old consumers).
- v2.0.0 — breaking. Coordinated migration of `state/quality/verdicts/` and consumer code.

---

## 8. Algedonic Channel Integration (added 2026-05-24)

> When the QA Architect Tribunal issues a `verdict: "block"` verdict, the
> dispatcher MUST also emit an algedonic signal via the algedonic-channel
> contract (`docs/contracts/2026-05-24-algedonic-channel.contract.md`) at
> `severity: "critical"`.

This is how a `block` verdict reaches the GA operator's dashboard without
requiring the operator to be watching the Demerzel pipeline output. The mapping
from QA verdict to algedonic signal is:

| QA verdict field | Algedonic signal field |
|---|---|
| `narrative` (truncated to 140 chars) | `summary` |
| markdown render of `followups` array | `details` |
| `links.pr` (or the verdict file path) | `evidence_url` |
| `blast_radius.components_reached` | `affected_artifacts` |
| (constant) | `severity: "critical"` |
| (constant) | `repo: "demerzel"` |
| (constant) | `source: "qa-architect-tribunal"` |

The Demerzel-side emit lands in a follow-up PR in the Demerzel repo. This
contract documents the integration so both sides can land independently — GA
gets the inbox + dashboard now; Demerzel adds the emit next.
