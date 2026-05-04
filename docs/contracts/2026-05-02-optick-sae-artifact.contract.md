# OPTIC-K SAE Artifact — Cross-Repo Contract

**Version:** 0.1.1 (draft, pending sign-off — additive change from v0.1.0: optional `input.compact_training_dim` field)
**Schema version:** 1
**Status:** Draft (Phase 0 of `optick-sae` plan, 2026-05-02)
**Producer:** `ix/crates/ix-optick-sae` (Rust orchestrator + Python PyTorch trainer via subprocess)
**Consumers:**
- `Apps/GaQaMcp/Tools/QaTools.cs` — `qa_score_quality_drift` reads feature distributions for drift evidence
- `Common/GA.Business.ML/Search/...` — optional retrieval-time reranking on sparse-feature similarity (Phase 4)
- `Demerzel/pipelines/qa-architect-cycle.ixql` — orchestrates training + drift evaluation
- Manual analysis notebooks for feature interpretation (Phase 3)

**Companion contract:** [2026-04-27-optick-weights-config.contract.md](2026-04-27-optick-weights-config.contract.md) — same `ix-autoresearch` ↔ GA pattern.

---

## 1. Why This Contract Exists

OPTIC-K v1.8 partitions 240 dimensions into hand-engineered semantic subspaces (IDENTITY / STRUCTURE / MORPHOLOGY / CONTEXT / SYMBOLIC / EXTENSIONS / SPECTRAL / MODAL / HIERARCHY / ATONAL_MODAL / ROOT). The partition design is a *hypothesis* about how musical concepts factor: the schema asserts that "Drop-2-ness" lives in SYMBOLIC, "fingering geometry" in MORPHOLOGY, "consonance" in STRUCTURE, and (since v1.8, 2026-04-19) "root pitch class" lives in ROOT as a 12-dim one-hot — split out of STRUCTURE to make STRUCTURE truly T-invariant.

A sparse autoencoder trained over the 313k-voicing corpus tests that hypothesis. Each learned feature can be located by partition — if a feature spans STRUCTURE and SYMBOLIC, either the partition boundary is wrong or there's a real cross-partition correlation worth knowing.

The artifact this contract describes is the connective tissue: ix produces it, GA consumes it for QA verdicts and (later) retrieval, Demerzel orchestrates the training cycle. Without a fixed shape, every consumer has to re-parse training output and the whole flywheel stalls.

The contract is a **two-way door** until Phase 4 freeze (target 2026-Q3). Field additions are additive; renames or enum changes need a coordinated bump.

---

## 2. JSON Shape

```json
{
  "schema_version": 1,
  "artifact_id": "optick-sae-2026-05-19T09-00-00Z-a1b2c3d4-ix-optick-sae",
  "trained_at": "2026-05-19T09:00:00Z",
  "trainer": "ix-optick-sae",
  "trainer_version": "0.1.0",
  "input": {
    "optick_index_path": "state/voicings/optick.index",
    "optick_index_sha": "sha256:89354bcb3513efbe1523651b734743c6921186a9e807f53bfbe4a74a254bd267",
    "optick_dim": 240,
    "compact_training_dim": 124,
    "schema_version": "OPTIC-K-v1.8",
    "corpus_size": 313487,
    "partitions_used": ["IDENTITY", "STRUCTURE", "MORPHOLOGY", "CONTEXT", "SYMBOLIC", "MODAL", "ROOT"]
  },
  "model": {
    "kind": "topk_sae",
    "dict_size": 1024,
    "k_sparse": 32,
    "training": {
      "epochs": 100,
      "batch_size": 4096,
      "lr": 0.001,
      "seed": 42,
      "loss_final": 0.0123,
      "sparsity_actual_mean": 31.7
    }
  },
  "metrics": {
    "reconstruction_mse": 0.0089,
    "reconstruction_r2": 0.943,
    "active_features_per_voicing_p50": 28,
    "active_features_per_voicing_p95": 41,
    "dead_features_pct": 4.2,
    "feature_partition_purity_mean": 0.71,
    "feature_partition_purity_p10": 0.43
  },
  "features_summary": {
    "total": 1024,
    "alive": 981,
    "high_frequency_count": 73,
    "low_frequency_count": 187
  },
  "links": {
    "feature_activations_parquet": "state/quality/optick-sae/2026-05-19/feature_activations.parquet",
    "feature_manifest_jsonl": "state/quality/optick-sae/2026-05-19/feature_manifest.jsonl",
    "training_log": "state/quality/optick-sae/2026-05-19/training.log",
    "model_weights": "state/quality/optick-sae/2026-05-19/sae_weights.safetensors",
    "supersedes": null
  },
  "narrative": "Phase 1 first run on OPTIC-K v1.8. Reconstruction MSE 0.0089 (under 0.05 guardrail). 4.2% dead features, partition purity 0.71 mean — schema partitions hold reasonably well; long-tail features cross boundaries (worth manual interpretation)."
}
```

---

## 3. Field Reference

### Top-level

| Field | Type | Required | Notes |
|---|---|---|---|
| `schema_version` | int | yes | `1` for v0.1.x. |
| `artifact_id` | string | yes | Stable, sortable, **filename-safe**. Pattern: `optick-sae-<iso8601-basic>-<short_id>-<trainer>`. Same dashes-not-colons convention as QA verdict_id. |
| `trained_at` | RFC3339 string | yes | UTC. |
| `trainer` | string | yes | Slug. Allowed: `ix-optick-sae`, `manual`. New trainers added via PR amending this contract. |
| `trainer_version` | semver | yes | Of the trainer crate. |
| `input` | object | yes | What was trained on. See §3.1. |
| `model` | object | yes | Architecture + training. See §3.2. |
| `metrics` | object | yes | Quality + sparsity scores. See §3.3. |
| `features_summary` | object | yes | Feature population stats. See §3.4. |
| `links` | object | yes | Pointers to large artifacts. See §3.5. |
| `narrative` | string | yes | ≤ 500 chars. Human-readable summary. |

### 3.1 `input`

| Field | Type | Notes |
|---|---|---|
| `optick_index_path` | string | Repo-relative path. Usually `state/voicings/optick.index`. |
| `optick_index_sha` | string | `sha256:...`. Lets consumers detect when the index changed since training. |
| `optick_dim` | int | The OPTIC-K embedding's *total* dimension. Currently 240 (v1.8 — bumped from 228 on 2026-04-19 with the 12-dim ROOT partition added). Read from `EmbeddingSchema.TotalDimension`, do not hardcode. |
| `compact_training_dim` | int (optional, v0.1.1+) | The dimension the SAE *actually trained on*. For the canonical OPTK v4 file this is `EmbeddingSchema.CompactDimension` (124 for v1.8 — sum of similarity-weighted partition dims, IDENTITY excluded since it's not in the compact format). MUST equal `optick_dim` if the trainer used the full embedding. WHY this exists: the 2026-05-03 local validation surfaced that "what was trained" and "what the embedding nominally is" diverge for the production OPTK pipeline; conflating them in `optick_dim` made narratives inconsistent (PR #82 wrote "118-dim" while `optick_dim: 240`). Optional in v0.1.x for compat with v0.1.0 artifacts; required candidate for v1.0 freeze. |
| `schema_version` | string | OPTIC-K schema version, exact match of `EmbeddingSchema.Version` (e.g. `"OPTIC-K-v1.8"`). |
| `corpus_size` | int | Number of voicings in the index. |
| `partitions_used` | array<string> | Which OPTIC-K partitions the SAE trained over. Phase 1 default trains over similarity-weighted partitions plus IDENTITY: `[IDENTITY, STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC, MODAL, ROOT]`. Skip info-only partitions (EXTENSIONS, SPECTRAL, HIERARCHY, ATONAL_MODAL) initially since they don't carry similarity weight. |

### 3.2 `model`

| Field | Type | Notes |
|---|---|---|
| `kind` | enum | `topk_sae` \| `relu_sae` \| `gated_sae`. Phase 1 uses `topk_sae`. |
| `dict_size` | int | Number of features. Phase 1 default 1024 (≈ 4.5× embedding dim). |
| `k_sparse` | int | For `topk_sae`: how many features active per input. |
| `training.*` | object | Hyperparameters + final loss. Reproducibility: `seed` is required. |

### 3.3 `metrics`

| Field | Type | Notes |
|---|---|---|
| `reconstruction_mse` | float | Lower better. **Guardrail: > 0.05 forces failure.** |
| `reconstruction_r2` | float | 0..1, higher better. Sanity-check vs MSE. |
| `active_features_per_voicing_p50` / `p95` | int | Sparsity actuals. Compare to `k_sparse`. |
| `dead_features_pct` | float | % of features that never activate. **Guardrail: > 30% triggers retrain with smaller dict.** |
| `feature_partition_purity_mean` | float | 0..1. Per-feature, fraction of activation mass concentrated in its dominant OPTIC-K partition. Tests the partition design hypothesis. |
| `feature_partition_purity_p10` | float | The 10th-percentile feature's purity — surfaces cross-partition features. |

### 3.4 `features_summary`

| Field | Type | Notes |
|---|---|---|
| `total` | int | Equals `model.dict_size`. |
| `alive` | int | Total - dead. |
| `high_frequency_count` | int | Features active on ≥ 10% of corpus. Often candidates for "too coarse." |
| `low_frequency_count` | int | Features active on < 0.1% of corpus. Often candidates for "too specific" or noise. |

### 3.5 `links`

| Field | Type | Notes |
|---|---|---|
| `feature_activations_parquet` | string | Per-voicing sparse activations: `(voicing_id, feature_id, activation)` rows. The big artifact (10s-100s of MB). |
| `feature_manifest_jsonl` | string | Per-feature: `{feature_id, frequency, top_voicings: [ids], partition_locality: {STRUCTURE: 0.78, ...}, manual_label?: string}`. |
| `training_log` | string | Plain-text training log. |
| `model_weights` | string | `safetensors` checkpoint for the SAE itself. Lets consumers re-encode new voicings. |
| `supersedes` | string \| null | `artifact_id` of the prior artifact this one replaces. |

---

## 4. Storage & Lifecycle

- Artifacts land at `state/quality/optick-sae/<date>/optick-sae-artifact.json`.
- Large links under the same directory.
- Artifacts are immutable. Re-training produces a new artifact pointing at the prior via `links.supersedes`.
- Drift detection (Phase 2): `qa_score_quality_drift` compares feature population stats and partition purity across consecutive artifacts.

---

## 5. Producer / Consumer Obligations

**Producer (`ix-optick-sae`) MUST:**
1. Pin `input.optick_index_sha` from the actual file at training time.
2. Fail the run (do NOT emit an artifact) if `reconstruction_mse > 0.05` or `dead_features_pct > 30`.
3. Set `links.supersedes` when re-training over a prior artifact in the same lineage.
4. Validate against `optick-sae-artifact.schema.json` before persisting.
5. Compute `dead_features_pct` over the **full training set**, not a held-out validation slice. A feature active on 0.1% of voicings is real but a 5% val sample reports it "dead" by chance — this consistently overstates the rate by ~2× on OPTIC-K v1.8 ([2026-05-03 finding](../learnings/2026-05-03-optick-sae-vanilla-topk-dead-features.md)).

**Producer (`ix-optick-sae`) SHOULD:**
1. **Enable AuxK / ghost-grads or equivalent auxiliary loss.** Vanilla top-k without auxiliary loss has structural ~40–65% dead-feature rate on OPTIC-K v1.8 and will fail the §5 MUST #2 guardrail. `sae-lens` enables this by default; the hand-rolled trainer at `Scripts/optick_sae_train.py` supports it via `--use-ghost-grads`. Empirically `aux_alpha = 0.1` (3× the Anthropic 2024 default) and `aux_k ≈ 2 × k_sparse` satisfy the guardrail on this corpus.

**Consumers SHOULD:**
1. Treat unknown `model.kind` values as "skip" rather than "fail."
2. Cache the `feature_activations_parquet` lookup; the file is large.
3. Verify `input.optick_index_sha` against the current index before using features for retrieval — stale features against a re-indexed corpus are worse than no features.

---

## 6. Open Questions (resolve before v1.0.0)

- **Q1.** Should we pin the partition list per artifact, or also include per-partition reconstruction error (in case a future schema change drops MODAL or splits SYMBOLIC)?
- **Q2.** `manual_label` lives on each feature in the manifest. Should it also be promoted to a top-level `labeled_features_pct` metric for governance tracking ("are we keeping up with feature interpretation")?
- **Q3.** For drift detection, what's the right distance metric between two feature populations? Hellinger? Earth-mover's? Per-feature top-voicings overlap?
- **Q4.** Cross-partition features are interesting findings — should they auto-emit a QA verdict followup with `severity: info` so they don't get lost?

---

## 7. Versioning

- v0.1.x — draft, may break.
- v1.0.0 — first frozen schema. Sign-off from GA + ix maintainers. Target 2026-Q3.
- v1.x — additive only.
- v2.0.0 — breaking. Coordinated migration of `state/quality/optick-sae/`.
