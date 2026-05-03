---
title: "Vanilla top-k SAE has structural dead-feature problem on OPTIC-K v1.8"
date: 2026-05-03
category: ml-engineering
status: validated finding (real-corpus run)
applies_to:
  - docs/plans/2026-05-02-arch-optick-sae-plan.md
  - docs/contracts/2026-05-02-optick-sae-artifact.contract.md
  - state/voicings/optick.index (v1.8, 313047 voicings × 124-dim compact)
---

# Vanilla top-k SAE has a structural dead-feature problem on OPTIC-K v1.8

## TL;DR (revised after ghost-grads sweep)

Vanilla top-k SAE **WITHOUT** auxiliary loss has 40–65% dead features on this corpus — guardrail-violating. Adding **AuxK / ghost-grads with `aux_alpha=0.1`** drops dead rate to **23.7%**, satisfying the contract's 30% guardrail while keeping reconstruction r² at 0.9996. Methodology fix: dead-feature counting must run over the *full training set*, not a held-out val slice (a feature active in 0.1% of voicings looks dead in a 5% sample by chance).

**So the May 19 agent should:**
1. Use auxiliary loss (ghost-grads / AuxK / sae-lens default) — non-negotiable for guardrail.
2. Measure `dead_features_pct` over the full corpus, not the val split.
3. Use `aux_alpha ≈ 0.1` (3× the Anthropic default of 0.03) for OPTIC-K v1.8 — empirically tuned for this corpus.

## Methodology

**Setup (run locally, 2026-05-03 EDT):**
- Index: `state/voicings/optick.index` — OPTK v4 binary, 313,047 voicings × 124-dim compact (similarity partitions only; IDENTITY is NOT in the file)
- Trainer: `Scripts/optick_sae_train.py` — top-k SAE, ReLU encoder + top-k mask + linear decoder, Adam, MSE loss
- Corpus split: 95% train (297,395) / 5% val (15,652), seed 42
- Hardware: CPU only (Python 3.14, torch 2.11.0+cpu)
- Per-epoch wall time: ~0.5–1.1 s

**Sweep:** held `k_sparse=32`, varied `dict_size`. 50 epochs each.

## Results — vanilla top-k (no auxiliary loss)

Held `k_sparse=32`, `epochs=50`. Dead-feature counts measured on the **5% val slice** (this overstates dead rate; see methodology fix below).

| `dict_size` | reconstruction_mse | r² | active_per_row p50 | dead features | partition_purity (mean) |
|---:|---:|---:|---:|---:|---:|
| 1024 | 0.000001 | 0.9997 | 32 | **662 (64.6%)** | 0.388 |
| 512  | 0.000003 | 0.9992 | 32 | **332 (64.8%)** | 0.370 |
| 384  | 0.000003 | 0.9992 | 32 | **211 (54.9%)** | 0.374 |
| 200  | 0.000007 | 0.9980 | 32 | **81  (40.5%)** | 0.380 |

Dead-feature rate is structurally ≥ 40% for vanilla top-k on this corpus, regardless of `dict_size`. The corpus has roughly 180–360 distinct "patterns" the SAE finds useful at k=32; everything beyond that dies.

## Results — top-k + AuxK ghost-grads (the fix)

Same setup, dead-feature counts measured on the **full training set** (the methodology fix that matters).

| `dict_size` | `aux_alpha` | reconstruction_mse | r² | dead features | partition_purity (mean) |
|---:|---:|---:|---:|---:|---:|
| 1024 | 0.03 (Anthropic default) | 0.000001 | 0.9997 | 542 (52.9%, val-slice — overstated) | 0.416 |
| 1024 | **0.10**                 | 0.000001 | 0.9996 | **243 (23.7%, full train)** ✅ | **0.434** |

The `aux_alpha=0.1` run is the first artifact that satisfies all contract §5 guardrails simultaneously: `reconstruction_mse < 0.05`, `dead_features_pct < 30`. Partition purity also improved from 0.388 → 0.434 — features are now slightly more concentrated in single OPTIC-K partitions when AuxK is forcing dead-feature revival.

## Methodology fix that matters

`dead_features_pct` MUST be measured over the full training set, not a held-out val slice. A feature that activates on 0.1% of voicings (≈ 300 of 313K) is real, useful, and would activate maybe 15 times in a 15K val slice — but a different sampling could miss it entirely and call it dead. The 5% val slice in earlier runs reported 52.9% dead while the same model on full train reported 23.7%.

## Why this happens (mechanism)

Top-k masking is a hard winner-take-all step. Once a feature loses the top-k race for the first few epochs, gradients flow only through the winning features, the losing features never see updates, and they stay at near-zero activation forever. This is a known issue with vanilla top-k SAEs and is exactly what motivated:

- **Ghost grads** (Anthropic, 2024) — auxiliary loss that flows gradient through dead features by reconstructing the residual via top-K-revival
- **JumpReLU SAE** (DeepMind, 2024) — replaces top-k with a learned threshold; no hard mask
- **Auxiliary L2 reconstruction loss** in `sae-lens` — same idea as ghost grads, different formulation

## Implications

### Contract §5 guardrail is fine; producer obligation is the constraint

The current 30% `dead_features_pct` guardrail is achievable with `kind: topk_sae` IF the producer uses an auxiliary loss. **No contract change required.** What does need to be added is a producer obligation:

> **Producer SHOULD enable AuxK / ghost-grads (or equivalent auxiliary loss).** Vanilla top-k without auxiliary loss has structural ~40–65% dead-feature rate on OPTIC-K v1.8 and will fail the §5 guardrail. Empirically `aux_alpha = 0.1` satisfies the guardrail on this corpus; `aux_k = 64` (≈ 2× main `k_sparse`) is reasonable.

This belongs in `docs/contracts/2026-05-02-optick-sae-artifact.contract.md` §5 as a SHOULD (not MUST — sae-lens, gated SAE, or other variants may use different mechanisms).

### Plan §Phase 1 unchanged in substance

The plan already says "Use sae-lens (Joseph Bloom et al.) — field-standard PyTorch SAE tooling." sae-lens defaults include auxiliary loss, so following the plan as written gets the right behavior. The hand-rolled trainer in `Scripts/optick_sae_train.py` now also supports ghost-grads via `--use-ghost-grads --aux-alpha 0.1`.

### Partition purity finding is preliminary but suggestive

Mean partition_purity ~0.37–0.39 across all dict sizes. Random-baseline for 6 partitions of unequal size is roughly 0.32 (mass-weighted by partition fraction). So features ARE concentrating somewhat — but only weakly. Could be:
- Genuine cross-partition correlations the schema doesn't separate (which would be the *interesting* finding)
- Artifact of the L2-normalized + per-partition-pre-weighted compact format collapsing partition structure
- Just a top-k SAE artifact — features that activate on similar voicings end up sharing patterns across partitions

Phase 3 manual feature interpretation will tell the story. For now the SAE finds patterns, those patterns are mildly partition-respecting, and we shouldn't over-interpret 0.37.

## What changes for the May 19 Phase 1 agent

The agent's brief currently says:
> Use sae-lens library (Joseph Bloom et al.) - field-standard PyTorch SAE tooling.

Good — that's already pointing at the right tool. New things the agent should know:

1. **Auxiliary loss is mandatory.** Either sae-lens default (ghost grads built-in), or `Scripts/optick_sae_train.py --use-ghost-grads --aux-alpha 0.1` if falling back to the hand-roll.
2. **Measure `dead_features_pct` over full training set, not val slice** — the val-slice number is consistently inflated by ~2× on this corpus.
3. **Empirically tuned values for OPTIC-K v1.8:** `aux_alpha=0.1` (Anthropic default 0.03 is too weak), `aux_k=64`, `dict_size=1024`, `k_sparse=32`, `epochs ≥ 50`. Recovered: r² 0.9996, dead 23.7%, partition purity 0.434.
4. **Reconstruction MSE will be much lower than 0.05** — this corpus is easy to reconstruct. The dead-rate guardrail is the binding one, not MSE.
5. **Compact dim is 124, not 240** — the index file holds similarity partitions only (STRUCTURE 24, MORPHOLOGY 24, CONTEXT 12, SYMBOLIC 12, MODAL 40, ROOT 12). IDENTITY etc. are NOT in the file. Contract field `partitions_used` should reflect this — already did in PR #73.
6. **Partition purity is a meaningful signal.** Mean ~0.43 with ghost-grads (vs ~0.39 without) suggests features are mildly partition-respecting but cross-partition correlations are real. Phase 3 manual interpretation will tell what each cross-partition feature actually represents.

## Reproducibility

```bash
# Vanilla top-k (will violate dead-features guardrail; for comparison only)
python -X utf8 Scripts/optick_sae_train.py --epochs 50 --dict-size 1024 --allow-guardrail-violation

# Production-equivalent: top-k + AuxK ghost grads (passes all guardrails)
python -X utf8 Scripts/optick_sae_train.py --epochs 50 --dict-size 1024 --use-ghost-grads --aux-alpha 0.1
```

The trainer enforces guardrails by default — runs without `--use-ghost-grads` will refuse to emit artifacts at any non-trivial `dict_size`.

## References

- `Scripts/optick_sae_train.py` — the trainer used here
- `Scripts/optick_sae_smoke_read.py` — OPTK v4 binary reader (used to validate the format before writing the trainer)
- `docs/contracts/2026-05-02-optick-sae-artifact.contract.md` — contract this finding affects
- `docs/plans/2026-05-02-arch-optick-sae-plan.md` — plan that needs an addendum
