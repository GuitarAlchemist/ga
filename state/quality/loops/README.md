# `state/quality/loops/` — auto-optimize loop ledgers

One append-only JSONL per domain, written by `/auto-optimize` (Step 3):
`<domain>.iterations.jsonl` — **one line per loop cycle** with the cycle's
metric `before → after → delta`, the `verdict`
(`improved | regressed | plateau | error | couldnt_run`), the edited artifact,
and the commit SHA. A misfiring oracle (`oracle_status != "ok"`) is recorded as
`verdict: "couldnt_run"` with `metric_after: null` — never as progress.

`build-views.sql` materializes these into the `loop_iteration` table and the
`loop_convergence` view (classifies each run `improving / plateaued /
oscillating / misfiring`), so "is this loop converging or spinning?" is a query:

```sql
SELECT loop_id, shape, iterations, total_gain, regressions, oracle_misfires
FROM loop_convergence ORDER BY loop_id;
```

`__seed__.iterations.jsonl` is a committed sentinel (one row, `loop_id="__seed__"`)
that keeps the `loops/*.iterations.jsonl` glob non-empty before any loop has run —
DuckDB errors on a zero-match glob. The views filter it out (`loop_id <> '__seed__'`).
Do not delete it.
