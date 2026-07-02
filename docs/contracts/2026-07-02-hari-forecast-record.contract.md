# hari forecast-record contract — v0.1.0 (DRAFT, hari-side review required)

> **Status: v0.1.x DRAFT — nothing implements this yet, on either side.**
> This is the GA-authored coordination artifact for the Jarvis Track J2 epic
> (BACKLOG.md § Jarvis Track): give hari the prediction half of its world
> model. hari (`../hari/`, Rust) owns the implementation; GA owns the first
> observable. Freeze only at the J2 epic's tracer-bullet milestone, after
> hari-side review. **Tribunal: REQUIRED** (cross-repo contract, per the epic).

## Why

hari holds beliefs, preserves contradictions, updates on observations —
memory without foresight. A belief that never risks a prediction can never be
wrong, and therefore never gets *better*. This contract defines the smallest
record that closes the loop: **belief → testable expectation → matched
outcome → calibration score → belief update signal.**

## The record

Schema: [`hari-forecast-record.schema.json`](hari-forecast-record.schema.json)
(`hari-forecast-record-v0.1.0`). JSONL append-only ledger, one file per day,
under hari's state dir (`HARI_STATE_DIR/forecasts/YYYY-MM-DD.jsonl` —
exact location is hari's call, the *shape* is the contract).

Design decisions (the parts worth arguing about in review):

- **The observable is pinned at emission time** (`observable.source` /
  `field` / `predicate`): scoring must be mechanical. If resolving a forecast
  requires judgement, the forecast was malformed.
- **Contradiction-aware by construction:** contradictory beliefs each emit
  their own forecast on the same observable. Resolution pressure comes from
  the Brier ledger — the belief that keeps winning earns weight. This is the
  epistemically honest version of belief decay, and it is *why* forecast
  records reference `belief_id` rather than replacing beliefs.
- **`void` is a first-class outcome:** an unreadable observable at horizon
  resolves as void, never silently dropped — otherwise the ledger
  overstates calibration exactly when the world is broken.
- **One forecast, one resolution.** No rolling windows, no re-scoring in
  v0.1. A belief update before horizon emits a new record with
  `links.supersedes` (the optick-sae-artifact non-breaking-shift pattern).

## The first observable (tracer bullet)

GA's J1 presence snapshot is the outcome stream: **will
`sensor:quality-snapshot` be `green` in `ga:state/fleet/presence.json` at
tomorrow's daily liveness rewrite?**

- Cheap: the artifact already exists and is committed on schedule (J1,
  shipped 2026-07-02).
- Frequent: daily resolution, so calibration curves accumulate fast.
- Honest: presence statuses are mechanical projections of workflow
  conclusions, not interpretations.

Example record (pending):

```json
{
  "schema": "hari-forecast-record-v0.1.0",
  "forecast_id": "0197f000-0000-7000-8000-000000000001",
  "belief_id": "ga-quality-snapshot-pipeline-healthy",
  "emitted_at": "2026-07-02T18:00:00Z",
  "observable": {
    "source": "ga:state/fleet/presence.json",
    "field": "/limbs/id=sensor:quality-snapshot/status",
    "predicate": "== green"
  },
  "prediction": { "probability": 0.9, "rationale": "14 consecutive green dailies; last failure 2026-07-02T02:16Z was infra-down, not pipeline" },
  "horizon": "2026-07-03T18:00:00Z"
}
```

## Split of responsibilities

| Side | Owns |
|---|---|
| **hari** | Emitting forecasts from beliefs; the scorer (matching outcomes at horizon, writing `resolution`); the calibration ledger (Brier over time, per belief) |
| **GA** | The observable artifacts forecasts point at (presence snapshot, quality snapshots); keeping their schemas stable or versioned |
| **Demerzel** | Nothing in v0.1 — forecasting is epistemic, not action-taking. The J5 loop is where forecasts meet the action boundary. |

## Locked fields (once frozen)

`schema`, `observable.*` key names, `resolution.outcome` enum, Brier
definition. Everything else may move with `links.supersedes`-style shifts.

## Open questions for hari-side review

1. Ledger location and rotation (`HARI_STATE_DIR/forecasts/` proposed).
2. Should `belief_id` be hari's internal id or a stable slug? (Slug proposed —
   ids shouldn't leak storage.)
3. Cross-repo read path: does the scorer read GA artifacts via the git remote
   (raw URL, ~5 min cache caveat per the 2026-07-02 solutions doc) or via a
   local sibling clone? (Sibling clone proposed, matching the existing
   JSON-on-disk handoff pattern.)
4. Minimum viable calibration output: per-belief Brier mean + count, or a
   bucketed reliability curve? (Mean + count proposed for v0.1.)
