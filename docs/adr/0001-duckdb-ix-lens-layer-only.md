---
status: accepted
date: 2026-06-20
---

# DuckDB + IX is the offline lens layer; live retrieval stays on WeightedPartitionCosine

## Context

GA already uses DuckDB + the `ix.duckdb_extension` (IX vector UDFs: `ix_cosine`,
`ix_kdist`, `ix_silhouette`, `ix_pca_project`, `ix_optick_scan`) for a growing set
of **offline analytics lenses** over our ML artifacts — routing-ambiguity
diagnostics, OPTIC-K retrieval-quality, IX↔GA set-theory cross-check, domain-invariant
sweeps, loop-convergence analytics, and (this change) SAE feature-coverage and
per-query drift. These read artifacts (embedding indexes, JSONL sinks, SAE
parquet/manifests, loop ledgers) and emit reports + JSON sidecars under
`state/quality/`. They are read-only, scheduled/manual, and never in a request path.

The recurring temptation is to take the next step: route **live voicing retrieval**
through DuckDB + IX too (replace `CpuVoicingSearchStrategy` /
`GpuVoicingSearchStrategy`). We are deciding explicitly not to.

## Decision

**DuckDB + IX is the lens layer only.** Live retrieval ranking stays on
`EmbeddingSchema.WeightedPartitionCosine` inside the in-memory search strategies.
New ML/AI analytics belong in the lens layer; they must not become a second engine
in the serving path.

## Why (the trade-off)

- **Different similarity.** Serving ranks with a **partition-weighted** cosine
  (per-partition weights from `EmbeddingSchema`). IX's `ix_cosine` is unweighted, so
  swapping it into serving would *silently change ranking* — a correctness
  regression disguised as a refactor.
- **The hot path is already fast.** In-memory CPU/GPU search is ~5 ms; DuckDB +
  a native extension in-request adds a process/extension dependency and version
  pinning for no latency win.
- **One-way door.** The OPTIC-K layout is a coordinated-re-index one-way door
  (CLAUDE.md). A second engine with its own opinion of "similar" multiplies the
  surfaces that must move together on any schema change.
- **DX ≈ AX.** A single ranking authority (`WeightedPartitionCosine`) is the deep
  module; lenses are read-only adapters over its *outputs*. Keeping them on opposite
  sides of the request boundary preserves that locality.

## Consequences

- **Embedder "bake-off geometry" is not a separate script.** Its only artifact
  (`state/quality/embedder-bakeoff/bakeoff-latest.json`) is *already-computed*
  silhouette/cv-acc with no raw vectors to crunch. The actual vector-geometry lens
  is `routing-ambiguity-diagnostic` — parameterise it by embedder rather than adding
  a redundant reader. Recorded here so future reviews don't re-suggest a third lens.
- Lenses may use IX UDFs (geometry: `ix_kdist`/`ix_silhouette`/`ix_pca_project`) **or**
  plain DuckDB aggregates (coverage/reconcile). Both are "the lens layer."
- If a future need genuinely requires DuckDB in the serving path, it reopens *this*
  ADR — and must first answer how partition weighting is preserved.

## Related

- `docs/methodology/agentic-engineering.md` — the lens layer is the self-improving-
  system lever (review the system that produces embeddings, not just outputs).
- Lenses: `docs/runbooks/{routing-ambiguity-diagnostic,optick-retrieval-quality,ix-ga-settheory-crosscheck,sae-feature-coverage,query-drift-lens}.md`.
- Serving authority: `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs`
  (`WeightedPartitionCosine`), `Common/GA.Business.ML/Search/*VoicingSearchStrategy.cs`.
