# Quality analytics (DuckDB)

A zero-server SQL layer over the file-based quality artifacts under `state/quality/`.
DuckDB reads the JSON snapshots in place and materializes them into a portable,
self-contained `quality.duckdb` so trends are queryable across sessions and from
.NET — without bespoke loaders that silently skip off-pattern files.

## Files

| File | Tracked | Purpose |
|---|---|---|
| `build-views.sql` | ✅ | Materializes the snapshots into tables + the `quality_latest` view. |
| `quality.duckdb` | ❌ (gitignored) | Generated binary; rebuild any time from the script. |

## Rebuild / refresh

Run from `state/quality/` so the relative globs resolve:

```bash
duckdb analytics/quality.duckdb < analytics/build-views.sql
```

(`duckdb` CLI: `winget install DuckDB.cli`.)

## Tables

- **`chatbot_qa`** — daily prompt-corpus pass rate (`pass_pct` is NULL when the run
  was backend-degraded; `degraded` / `degraded_reason` flag why).
- **`routing_eval`** — semantic intent router accuracy (`accuracy`, `in_scope_accuracy`,
  `oos_decline_rate`).
- **`voicing_analysis`** — OPTIC-K corpus size + chord-recognition coverage.
- **`optick_sae`** — OPTIC-K sparse-autoencoder training artifacts (the cross-repo
  contract output ix produces, consumed here); flattened to mirror ix's columns for
  producer/consumer parity (`reconstruction_r2`, `dead_features_pct`, `features_alive`).
- **`pr_grades`** — post-merge grade cards (`/grade-last-pr`); empty until cards land
  (see the commented populate query in `build-views.sql`).
- **`quality_latest`** (view) — latest value per source.

## Query from .NET

`Tools/QualityLens` opens `quality.duckdb` read-only via `DuckDB.NET.Data.Full`:

```bash
dotnet run --project Tools/QualityLens                         # latest rollup
dotnet run --project Tools/QualityLens -- "SELECT * FROM routing_eval ORDER BY day"
```
