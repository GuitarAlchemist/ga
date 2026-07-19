# state/research/

Reproducible artifacts for studies in [`docs/research/`](../../docs/research/README.md).

One subdirectory per study, sharing the study's slug:

```
state/research/<slug>/          ← e.g. optick-sae-feature-atlas/
  verdict.json                  ← small structured outputs: commit these
  summary-stats.json
  pca.json
```

**Commit** small structured results (verdict JSON, summary stats, plot data) —
they *are* the evidence. **Do not commit** heavy regenerable dumps (large
matrices, full embedding exports); add a per-study glob to `.gitignore` and keep
only the summary. This path is not gitignored by default, so anything dropped
here is tracked unless you exclude it explicitly.
