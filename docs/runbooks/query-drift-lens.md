# Per-query routing/retrieval drift lens

Measures the geometry of the **live queries** the `SemanticIntentRouter` actually
routed — not the design-time anchors. Surfaces **out-of-distribution** queries
(coverage gaps) and the **runtime separability** of each routed intent. Uses IX's
DuckDB vector extension (`ix.duckdb_extension`) — no new GA dependency.

## Why

`routing-ambiguity-diagnostic` measures the geometry of the *anchors* the router
classifies against — the design-time picture. This lens measures the geometry of
the *queries real users sent*, using the **exact vector the router scored on**
(persisted by `QueryEmbeddingLog`, not a re-embed). Two distinct signals:

- **OOD** — a query far from all its neighbours (`ix_kdist`) is unlike what the
  router usually sees: a coverage gap, or a genuinely novel ask the corpus should
  grow to cover.
- **Runtime confusability** — low/negative per-intent `ix_silhouette` means real
  users' queries for an intent land amongst *another* intent's. The anchor
  diagnostic predicts this; this lens confirms it in production traffic.

It's the GA-side **local** view of cross-repo Contract B — the `ix` sibling's
`ix-duck` analyst bench is the canonical OOD authority (it ran the ROC sweep that
validated raw-cosine OOD). This lens needs no `ix` checkout, only the built
extension and the on-disk JSONL.

## Run

```powershell
# Default sink dir (state/quality/query-embeddings):
pwsh Scripts/query-drift-lens.ps1

# A specific export dir / larger sample:
pwsh Scripts/query-drift-lens.ps1 -Dir state/quality/query-embeddings -Sample 5000
```

Prereqs: `duckdb` on PATH (v1.0+), the built `ix.duckdb_extension`
(`pwsh ../ix/crates/ix-duck-ext/build.ps1`), and **at least one `*.jsonl` in the
sink**. The sink is **gitignored** and only fills when the live router runs (GaApi);
a fresh checkout has none. The runner fails non-zero (code 2) with a clear "exercise
the live router, then re-run" message rather than reporting a false "clean".

> The router writes the sink automatically unless `GA_QUERY_EMBEDDING_NO_LOG=1`.
> Point the lens elsewhere with `-Dir` or `GA_QUERY_EMBEDDING_DIR`.

## Pipeline

`Scripts/query-drift-lens.sql` loads the IX extension and:

1. Pins to the **dominant `(embedder, dim)`** — cosine across embedder swaps
   (nomic → bge-large) is meaningless, so off-embedder rows are dropped and counted.
2. **`ix_kdist`** (k=5) over all pinned query vectors (declined ones included — they
   are still real vectors the router saw) → the OOD signal.
3. **`ix_silhouette`** over routed-only queries, labelled by intent → runtime
   separability.

## Outputs (under `<sink-dir>/drift/`)

| File | What |
|---|---|
| `query-drift-<date>.md` | Human report: sample/decline rate, OOD distribution, most-OOD queries, per-intent runtime silhouette, lowest-confidence routed queries |
| `query-drift-isolated.json` | Top-200 queries by kdist (text, intent, confidence) |
| `query-drift-per-intent.json` | Per-intent mean kdist + silhouette (worst-first) |

## Reading it

- **High-`kdist` queries** → coverage gaps. If a cluster of real asks sits far from
  everything, add example prompts / an intent for them.
- **Negative per-intent silhouette** → that intent's live queries overlap another's.
  Cross-reference the anchor `routing-ambiguity-diagnostic`; fix by contrasting the
  colliding intents' example prompts — **never** a keyword rule
  (`feedback_no_regex_routing_cheating`).
- **Low-confidence routed queries near the decline threshold** → candidates that
  may flip to fallback under small embedder/threshold changes; watch them when
  tuning the threshold (it's embedder-specific — 0.55 nomic → ~0.64 bge-large).
- **High `declined_pct`** → the threshold may be too high, or real demand exists for
  an unmodelled intent.
