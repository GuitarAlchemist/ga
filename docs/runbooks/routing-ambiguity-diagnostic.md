# Routing-ambiguity diagnostic

Explains **why** the `SemanticIntentRouter` misroutes — by measuring the geometry
of the embedding anchors it routes against. Complements `RoutingEvalHarness`
(which measures routing *accuracy*, the symptom) with a *diagnostic* (the cause).
Uses IX's DuckDB vector extension (`ix.duckdb_extension`) — no new GA dependency.

## Why

The router classifies a query by max-cosine against each intent's
`Description` + `ExamplePrompts`. When two intents' example clouds overlap in
embedding space, queries land on the wrong one. There was previously **zero
visibility** into that geometry. This tool surfaces it so you can fix routing
the **semantic** way — contrast the colliding example prompts — instead of
keyword/regex hint rules.

## Run

```powershell
# Needs Ollama up with nomic-embed-text (same embedder the router uses).
pwsh Scripts/routing-ambiguity-diagnostic.ps1

# Reuse the last embedding dump (no Ollama) while iterating on the SQL:
pwsh Scripts/routing-ambiguity-diagnostic.ps1 -SkipEmbed

# Point at a non-default extension build:
pwsh Scripts/routing-ambiguity-diagnostic.ps1 -Extension ../ix/crates/ix-duck-ext/ix.duckdb_extension
```

Prereqs: `duckdb` on PATH (v1.0+), the built `ix.duckdb_extension`
(`pwsh ../ix/crates/ix-duck-ext/build.ps1`), and — unless `-SkipEmbed` — a live
Ollama embedder. The runner fails non-zero (never "clean") if any prereq is
missing or DuckDB errors.

## Pipeline

1. `RoutingEvalHarness.DumpRoutingAnchors_ForAmbiguityDiagnostic` ([Explicit]
   test) embeds every routed intent's description + examples with the **same
   normalization** (`Trim().ToLowerInvariant()`) and embedder the production
   router uses → `state/quality/routing-diagnostic/routing-anchors-<date>.json`
   (gitignored — multi-MB, regenerable).
2. `Scripts/routing-ambiguity-diagnostic.sql` loads the IX extension and runs:
   - `ix_silhouette` — per-intent separability (low/negative = confusable)
   - `ix_cosine` nearest-wrong-neighbour — names the confusable intent **pairs**
   - `ix_pca_project` — 2-D coords for a scatter plot

## Outputs (committed as the record)

| File | What |
|---|---|
| `routing-ambiguity-<date>.md` | Human report: overall + per-intent silhouette, confusable pairs, worst individual collisions |
| `routing-silhouette-by-intent.json` | Per-intent mean/min silhouette (sorted worst-first) |
| `routing-confusable-pairs.json` | Intent pairs by nearest-wrong-neighbour cosine |
| `routing-pca-coords.json` | 2-D PCA coords per example anchor (for plotting) |

## Reading it

- **Negative per-intent silhouette** → that intent's example prompts are, on
  average, closer to a *different* intent's examples than their own. Top
  candidates to rewrite/contrast.
- **Confusable pair with cosine > ~0.85** → the two intents' examples nearly
  coincide (e.g. `relativekey` "How many sharps in D major" vs `circleoffifths`
  "How many sharps does D major have?"). Decide which intent owns the phrasing,
  then push the other's examples away from it.
- The fix is **always** example-prompt curation, never a keyword rule.
