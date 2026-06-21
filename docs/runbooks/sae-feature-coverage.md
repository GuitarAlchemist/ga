# OPTIC-K SAE feature-coverage lens

Reads an **ix-optick-sae** training artifact and answers: is the learned dictionary
actually being *used*, does the per-feature manifest *agree* with the artifact's
declared metrics, is the top-k sparsity what the model claims, and which voicings
sit *isolated* in SAE feature space. Uses IX's DuckDB vector extension
(`ix.duckdb_extension`) — no new GA dependency.

## Why

The SAE (sparse autoencoder, trained in the `ix` sibling) decomposes each OPTIC-K
voicing into a sparse combination of `dict_size` learned feature atoms. Two things
silently go wrong with SAEs: **dead features** (atoms that never fire — wasted
dictionary) and **producer/consumer drift** (the committed `feature_manifest.jsonl`
no longer matches the artifact it shipped with). This lens makes both visible, and
adds an `ix_kdist` pass to surface voicings whose feature decomposition is unlike
any other's — rare harmonic structure, or a suspect row.

GA only **reads** the artifact; `ix-optick-sae` produces it (cross-repo contract
`docs/contracts/2026-05-02-optick-sae-artifact.contract.md`). This is the GA-side
coverage view, not a re-train.

## Run

```powershell
# Newest complete artifact dir under state/quality/optick-sae/<date>/:
pwsh Scripts/sae-feature-coverage.ps1

# A specific dated run, smaller kdist sample:
pwsh Scripts/sae-feature-coverage.ps1 -ArtifactDir state/quality/optick-sae/2026-06-14 -Sample 2000

# Non-default extension build:
pwsh Scripts/sae-feature-coverage.ps1 -Extension ../ix/crates/ix-duck-ext/ix.duckdb_extension
```

Prereqs: `duckdb` on PATH (v1.0+) and the built `ix.duckdb_extension`
(`pwsh ../ix/crates/ix-duck-ext/build.ps1`). The runner fails non-zero (never
"clean") if a prereq is missing, DuckDB errors, or no artifact dir holds all three
required files together.

A "complete" artifact dir holds **all three**: `optick-sae-artifact.json`,
`feature_manifest.jsonl`, and `feature_activations.parquet` (the dense
`N_voicings × dict_size` matrix). Dirs with only the JSON/manifest (no parquet) are
skipped — the IX section needs the per-voicing vectors.

## Pipeline

`Scripts/sae-feature-coverage.sql` loads the IX extension and runs:

- **Reconcile** — recomputes coverage from `feature_manifest.jsonl` and checks it
  against the artifact's declared `metrics` (`dead_features_pct`, `alive`,
  `dict_size`). `consistent = false` → exit 4 (a real producer/pairing bug).
- **Coverage** — `dead_pct`, alive count, activation-frequency spread.
- **Decoder-norm by liveness** — dead atoms should have small decoder norm.
- **Top-k sparsity** (parquet) — active features per voicing should equal
  `k_sparse`; a different `mean_active` means parquet ≠ declared model.
- **`ix_kdist`** (k=5) over sampled feature vectors — local density / isolation.

## Outputs (committed as the record, under `<artifact-dir>/coverage/`)

| File | What |
|---|---|
| `sae-coverage-<date>.md` | Human report: reconcile, coverage, sparsity, kdist |
| `sae-top-features.json` | 50 most-active alive features (idx, count, decoder norm) |
| `sae-dead-features.json` | Never-fired features by decoder norm |
| `sae-isolated-voicings.json` | Top-200 voicings by kdist (ordinal, active count) |

## Reading it

- **`consistent = false`** → stop. The manifest and artifact disagree; the lens
  exits 4. Re-pair the files or re-run the trainer.
- **High `dead_pct`** (≫ a few %) → the dictionary is over-sized for the corpus;
  smaller `dict_size` would lose nothing.
- **`mean_active` ≠ `k_sparse`** → the parquet wasn't produced by the declared
  top-k model. A pairing or version bug.
- **High `ix_kdist` voicings** → inspect them (ordinal = row in `optick.index` scan
  order). Either genuinely rare structure (interesting) or an embedding artifact.

The exit code is the verdict: `0` clean, `2` prereq/no-artifact, `3` could-not-run
(no reconcile verdict emitted), `4` reconcile inconsistent.
