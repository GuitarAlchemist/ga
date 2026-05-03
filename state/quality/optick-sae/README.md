# OPTIC-K SAE Artifacts

This directory holds artifacts produced by the OPTIC-K Sparse Autoencoder pipeline. It exists ahead of any actual training so consumers know where to look.

## Layout

```
state/quality/optick-sae/
├── README.md                                    ← this file
├── baseline-2026-05-03.json                     ← pre-SAE corpus fingerprint
└── <YYYY-MM-DD>/                                ← one directory per training run
    ├── optick-sae-artifact.json                 ← contract-conforming summary (the small JSON)
    ├── feature_activations.parquet              ← per-voicing sparse activations (the big artifact)
    ├── feature_manifest.jsonl                   ← per-feature: top voicings, partition locality, manual labels
    ├── sae_weights.safetensors                  ← model checkpoint for re-encoding new voicings
    └── training.log                             ← plain-text training log
```

## Producer

`ix/crates/ix-optick-sae` (Rust orchestrator + Python `sae-lens` trainer via subprocess). Pinned by [docs/contracts/2026-05-02-optick-sae-artifact.contract.md](../../../docs/contracts/2026-05-02-optick-sae-artifact.contract.md). Schema enforced by [docs/contracts/optick-sae-artifact.schema.json](../../../docs/contracts/optick-sae-artifact.schema.json).

## Consumers

- **`Apps/GaQaMcp/Tools/QaTools.cs`** — `qa_score_quality_drift` reads consecutive artifacts to compute feature-population deltas and emits `evidence{kind: quality_snapshot}` for the QA verdict.
- **`Common/GA.Business.ML/Search/...`** (Phase 4, deferred) — optional retrieval-time reranking on sparse-feature similarity.
- **`Demerzel/pipelines/qa-architect-cycle.ixql`** — orchestrates training cycles when `state/voicings/optick.index` SHA changes.
- **Manual analysis notebooks** under `Notebooks/Research/` — Phase 3 feature interpretation.

## Lifecycle

- Artifacts are immutable. Re-training produces a new artifact with `links.supersedes` pointing at the prior in-lineage artifact.
- The big files (`feature_activations.parquet`, `sae_weights.safetensors`) are gitignored; the small JSON summary lives in git so the verdict drift detector can walk the time series cheaply.
- Contract guardrails (per §5):
  - `reconstruction_mse > 0.05` → producer MUST NOT emit an artifact (run failure recorded in `training.log` instead).
  - `dead_features_pct > 30%` → producer MUST retry with smaller `dict_size` before emitting.
  - `artifact_id` must be filename-safe (dashes, not colons, in timestamp portion).

## Status

Phase 0 complete. Phase 1 implementation scheduled to run via remote agent on 2026-05-19 (routine `trig_01QUrKEsYLPPW4KNLzKZRE2n`). Until then, this directory holds the pre-SAE baseline only.
