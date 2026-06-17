# `state/quality/loops/` — auto-optimize loop ledgers

One append-only JSONL per domain, written by the shared writer
`Scripts/loop-record.ps1` (called from `/auto-optimize` Step 3.8 and the
`/ga-chatbot-afk-harness` outer loop): `<domain>.iterations.jsonl` —
**one line per loop cycle** with the cycle's metric `before → after → delta`,
the `verdict` (`improved | regressed | plateau | error | couldnt_run`), the
`worst_item`, the edited artifact, and the commit SHA. A misfiring oracle
(`oracle_status != "ok"`) is recorded as `verdict: "couldnt_run"` with
`metric_after: null` — never as progress (the writer enforces this fail-closed).

Schema is pinned by `state/quality/_fixtures/loop-iterations.sample.jsonl`;
`state/quality/_fixtures/verify-loop-record.ps1` asserts the writer matches it.

**Consumers:** this directory is a JSON-on-disk contract. The sibling **ix**
repo's `ix-duck` analyst bench reads `loops/*.iterations.jsonl` directly (its own
DuckDB) to cluster failure signatures across iterations — `worst_item`
recurrence, oscillating-vs-improving verdicts, artifact churn. GA's own
DuckDB convergence view + the `loop-decide` self-termination controller are a
separate, optional internal consumer (loop-observability feature) and are **not**
required by the cross-repo contract.

`__seed__.iterations.jsonl` is a committed sentinel (one row, `loop_id="__seed__"`,
`domain="__seed__"`) that keeps the `loops/*.iterations.jsonl` glob non-empty
before any real loop has run — a DuckDB glob over zero files errors. Consumers
filter it out (`loop_id <> '__seed__'`). Do not delete it; the writer refuses to
emit `__seed__`/`__test__` as a production row unless `-AllowSentinel` is passed.
