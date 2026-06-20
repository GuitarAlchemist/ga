# Plan: loop observability — persist the per-iteration ledger into DuckDB

**Date:** 2026-06-15
**Type:** feat (instrumentation — **zero behavior change** to the loop's decisions)
**Status:** IMPLEMENTED 2026-06-15 (steps 1–3; dashboard strip deferred). See **As built** at the end.
**Parent research:** `docs/research/2026-06-15-nested-loop-chatbot-development-duckdb.md` (Phase 1)
**Reversibility:** two-way door — purely additive (new JSONL files, new tables/view, additive SKILL edits). Nothing existing is removed or re-decided.

## 1. Problem / who is in pain

We run autonomous improvement loops (`/auto-optimize`) against the chatbot, but **we can't see whether a loop is converging, plateauing, or spinning** — the one question the research says must be a *query, not a vibe*. Concretely:

- `auto-optimize` **Step 5** persists a *per-run* summary to `state/quality/<domain>/loop-history.jsonl` (`cycles_ran`, `metric_before/after`, `exit_status`).
- **Step 3** computes each cycle's `metric_before → after` delta and the plateau window **in memory only** — it is never written. So the *trajectory inside a run* (the thing that reveals oscillation, slow climb, or a stuck loop) is lost the moment the run ends.
- Nothing about loops reaches `quality.duckdb`, so the dashboard/`QualityLens` can't answer "are our loops working?".

**What changes for the operator:** after this, `SELECT * FROM loop_convergence` answers "is this loop run improving / plateaued / oscillating / misfiring?" across every domain, and it surfaces on the dev dashboard. The loop's *behavior* is unchanged — we only record what it already computes.

## 2. Design

### 2a. A run identity to join on
Generate a `loop_id` once at run start (Step 1) — `"{domain}-{utc:yyyyMMddTHHmmssZ}"`. Both ledgers carry it so per-iteration rows join to their run.

### 2b. Per-iteration ledger (NEW) — `state/quality/<domain>/loop-iterations.jsonl`
Append one line per cycle inside Step 3 (after the oracle + roundtrip + commit/revert decision is known):

```jsonc
{ "loop_id": "chatbot-qa-20260615T1830Z", "domain": "chatbot-qa", "iteration": 3,
  "ts": "2026-06-15T18:34:02Z",
  "oracle_status": "ok",                     // ok | couldnt_run | error
  "metric_name": "pass_pct",
  "metric_before": 0.90, "metric_after": 0.92, "metric_delta": 0.02,
  "verdict": "improved",                     // improved | regressed | plateau | error | couldnt_run
  "worst_item": "voice-leading-tritone-sub",
  "artifact_edited": "Common/GA.Business.ML/Agents/Skills/VoiceLeadingSkill.cs",
  "commit_sha": "a1b2c3d", "roundtrip_passed": true }
```

Critical: when the oracle's `oracle_status != "ok"` (the fail-closed contract in Step 3.2), the row is written with `verdict = "couldnt_run"` and `metric_after = null` — **a misfire is never recorded as progress** (the documented `auto-optimize` paranoia rule, now durable + queryable).

### 2c. Per-run ledger (KEEP) — `state/quality/<domain>/loop-history.jsonl`
Already written in Step 5; add `loop_id` so it joins. No other change.

### 2d. DuckDB views — append to `state/quality/analytics/build-views.sql`
```sql
-- One row per loop RUN (Step 5 summary).
CREATE OR REPLACE TABLE loop_run AS
SELECT loop_id, domain,
       TRY_CAST(metric_before AS DOUBLE) AS metric_before,
       TRY_CAST(metric_after  AS DOUBLE) AS metric_after,
       cycles_ran, exit_status, pr_url, timestamp AS ran_at
FROM read_json_auto('*/loop-history.jsonl', filename = true, union_by_name = true);

-- One row per loop ITERATION (Step 3 cycle).
CREATE OR REPLACE TABLE loop_iteration AS
SELECT loop_id, domain, iteration, ts,
       oracle_status, metric_name,
       TRY_CAST(metric_before AS DOUBLE) AS metric_before,
       TRY_CAST(metric_after  AS DOUBLE) AS metric_after,
       TRY_CAST(metric_delta  AS DOUBLE) AS metric_delta,
       verdict, worst_item, artifact_edited, commit_sha, roundtrip_passed
FROM read_json_auto('*/loop-iterations.jsonl', filename = true, union_by_name = true)
ORDER BY loop_id, iteration;

-- Convergence per run: the "improving / plateaued / oscillating / misfiring?" query.
CREATE OR REPLACE VIEW loop_convergence AS
SELECT loop_id, domain,
       count(*)                                              AS iterations,
       max(metric_after) - min(metric_before)               AS total_gain,
       round(avg(metric_delta), 4)                          AS mean_delta,
       sum(CASE WHEN abs(metric_delta) < 0.005 THEN 1 ELSE 0 END) AS plateau_iters,
       sum(CASE WHEN verdict = 'regressed'   THEN 1 ELSE 0 END)   AS regressions,
       sum(CASE WHEN verdict = 'couldnt_run' THEN 1 ELSE 0 END)   AS oracle_misfires,
       CASE
         WHEN sum(CASE WHEN verdict='couldnt_run' THEN 1 ELSE 0 END) > 0 THEN 'misfiring'
         WHEN sum(CASE WHEN verdict='regressed'  THEN 1 ELSE 0 END) >= 2 THEN 'oscillating'
         WHEN max(metric_after) - min(metric_before) > 0.01              THEN 'improving'
         ELSE 'plateaued'
       END                                                  AS shape
FROM loop_iteration GROUP BY loop_id, domain;
```
(The `chatbot-qa/*` glob already lives under `state/quality/`; the script runs from there, so `*/loop-iterations.jsonl` picks up every domain.)

### 2e. Surface it (small)
- `QualityLens` already runs arbitrary SQL read-only → `SELECT * FROM loop_convergence ORDER BY ran_at DESC` works day one, no code.
- Dashboard (stretch, optional this phase): a "Loop convergence" strip on the **Harness** tab (`LoopsGoalsCard` neighbour) reading a new `/dev-data/loop-convergence` middleware query, colour by `shape` (improving=green, plateaued=grey, oscillating/misfiring=red).

## 3. Steps

1. **SKILL.md edits** (`.claude/skills/auto-optimize/SKILL.md`): emit `loop_id` at Step 1; append the per-iteration record in Step 3 (incl. the `couldnt_run` branch tied to the existing fail-closed contract in 3.2); add `loop_id` to the Step 5 record. *Spec-only change to the executor; no new behavior, just a write.*
2. **`build-views.sql`**: append the `loop_run` + `loop_iteration` tables and `loop_convergence` view (§2d).
3. **Fixture + verify** (the proof this phase works without running a real multi-hour loop): commit a tiny `state/quality/_fixtures/loop-iterations.sample.jsonl` with a known improving run, an oscillating run, and a misfire run; a test asserts `loop_convergence.shape` classifies all three correctly via `duckdb < build-views.sql` over the fixture. (Keeps fixtures out of the real `chatbot-qa/` glob.)
4. **Dashboard strip** (optional/stretch): `/dev-data/loop-convergence` + a Harness-tab card.

## 4. Success criteria (mechanizable)

- After a loop run (or against the fixture), `SELECT shape, count(*) FROM loop_convergence GROUP BY shape` returns rows, and the three fixture runs classify as `improving` / `oscillating` / `misfiring` respectively.
- A run whose oracle never executed shows `oracle_misfires > 0` and `shape = 'misfiring'` — **never** `improving` (regression-guards the paranoia rule).
- `dotnet`/frontend builds unaffected; `quality.duckdb` rebuilds clean; existing `quality_latest` unchanged.

## 5. One-way doors / guardrails

- None. Additive only. If unwanted, delete the two JSONL globs + the three SQL objects; the loop reverts to its current (silent) behavior.
- Does **not** touch the loop's decision logic, scope_boundary, kill switches, or merge gates — observability only. Changing the loop to *act on* `loop_convergence` (self-termination) is **Phase 2**, explicitly out of scope here.

## 6. Effort

~2–3h: SKILL edits + the SQL block + the fixture test. Dashboard strip is another ~1h if taken this phase.

## As built (2026-06-15)

Two refinements vs. the design above, both simplifications:

- **Single seeded ledger dir.** Ledgers live in `state/quality/loops/<domain>.iterations.jsonl` (one dir, not per-domain snapshot dirs). A committed sentinel `loops/__seed__.iterations.jsonl` keeps the `loops/*.iterations.jsonl` glob non-empty — verified that a **zero-match glob errors** in DuckDB, an empty file does not. The views filter `loop_id <> '__seed__'`. `loops/README.md` documents it.
- **`loop_run` dropped for Phase 1.** `loop_convergence` already groups by `loop_id`, so it *is* the per-run rollup — no separate `loop_run` table, no second glob, and **Step 5 is untouched** (the existing per-run `loop-history.jsonl` write stays). A `loop_run` with `exit_status`/`pr_url` can join in later if the dashboard wants it.

**Files:** `state/quality/analytics/build-views.sql` (+`loop_iteration` table, `loop_convergence` view), `.claude/skills/auto-optimize/SKILL.md` (Step 1 `loop_id`, Step 3 step 9 ledger append incl. the `couldnt_run` branch), `state/quality/loops/{__seed__.iterations.jsonl,README.md}`, `state/quality/_fixtures/{loop-iterations.sample.jsonl,verify-loop-convergence.ps1}`.

**Verified:** `verify-loop-convergence.ps1` runs the real `build-views.sql` with the fixture dropped into the glob and asserts all four shapes — `improving / oscillating / misfiring / plateaued` — classify correctly (incl. a misfiring run that must **not** read as improving). PASS. Confirms the full analytics layer still builds with the new objects.

**Out of scope (Phase 2):** the loop *acting on* `loop_convergence` (self-termination from the trajectory) and the dashboard strip.
