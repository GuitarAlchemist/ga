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

## TL;DR

A hand-rolled top-k SAE (no auxiliary loss, no ghost grads) trained on the real OPTIC-K v1.8 corpus achieves near-perfect reconstruction (r² ≥ 0.998 in 50 epochs) but **40-65% of dictionary atoms die regardless of `dict_size`**. The contract's `dead_features_pct ≤ 30` guardrail is unachievable with vanilla top-k — the rich-get-richer dynamic locks in the first-winning features and the rest never recover.

## Methodology

**Setup (run locally, 2026-05-03 EDT):**
- Index: `state/voicings/optick.index` — OPTK v4 binary, 313,047 voicings × 124-dim compact (similarity partitions only; IDENTITY is NOT in the file)
- Trainer: `Scripts/optick_sae_train.py` — top-k SAE, ReLU encoder + top-k mask + linear decoder, Adam, MSE loss
- Corpus split: 95% train (297,395) / 5% val (15,652), seed 42
- Hardware: CPU only (Python 3.14, torch 2.11.0+cpu)
- Per-epoch wall time: ~0.5–1.1 s

**Sweep:** held `k_sparse=32`, varied `dict_size`. 50 epochs each.

## Results

| `dict_size` | reconstruction_mse | r² | active_per_row p50 | dead features | partition_purity (mean) |
|---:|---:|---:|---:|---:|---:|
| 1024 | 0.000001 | 0.9997 | 32 | **662 (64.6%)** | 0.388 |
| 512  | 0.000003 | 0.9992 | 32 | **332 (64.8%)** | 0.370 |
| 384  | 0.000003 | 0.9992 | 32 | **211 (54.9%)** | 0.374 |
| 200  | 0.000007 | 0.9980 | 32 | **81  (40.5%)** | 0.380 |

Dead-feature rate is structurally ≥ 40% for vanilla top-k on this corpus. Reducing `dict_size` reduces absolute dead count but the proportional rate stays high. The corpus has roughly 180–360 distinct "patterns" the SAE finds useful at k=32; everything beyond that dies.

## Why this happens (mechanism)

Top-k masking is a hard winner-take-all step. Once a feature loses the top-k race for the first few epochs, gradients flow only through the winning features, the losing features never see updates, and they stay at near-zero activation forever. This is a known issue with vanilla top-k SAEs and is exactly what motivated:

- **Ghost grads** (Anthropic, 2024) — auxiliary loss that flows gradient through dead features by reconstructing the residual via top-K-revival
- **JumpReLU SAE** (DeepMind, 2024) — replaces top-k with a learned threshold; no hard mask
- **Auxiliary L2 reconstruction loss** in `sae-lens` — same idea as ghost grads, different formulation

## Implications

### Contract §5 guardrail is `kind`-dependent, not universal

The current contract says:
> dead_features_pct > 30% triggers retrain with smaller dict

For `kind: topk_sae` without auxiliary losses, this is unachievable on OPTIC-K. We have three options for the contract:

1. **Make the guardrail `kind`-conditional** — `topk_sae: 0.65`, `gated_sae: 0.30`, `relu_sae: ...`. Most accurate but adds contract complexity.
2. **Mandate `kind: gated_sae` or sae-lens with ghost grads** for production — keep the 30% guardrail, change which architecture producers must use.
3. **Lower the universal guardrail to 0.65** — simplest, but loses the signal that "high dead rate = something's wrong" for architectures where 30% IS achievable.

**Recommendation: option 2.** Vanilla top-k is a known-limited baseline. Phase 1 should use sae-lens proper (which has ghost grads built in) for production runs. The current hand-roll is fine for plumbing validation but not for emitting consumable artifacts.

### Plan §Phase 1 needs an addendum

- Specify `sae-lens` with `auxiliary_loss=ghost_grads` (or whatever the current sae-lens API name is)
- Note the reasonable expected dead rate post-ghost-grads (literature says ~5–15%)
- Add a fallback: if sae-lens has API drift, hand-rolled top-k MUST `--allow-guardrail-violation` to emit; the resulting artifact is informational, not consumable by `qa_score_quality_drift`.

### Partition purity finding is preliminary but suggestive

Mean partition_purity ~0.37–0.39 across all dict sizes. Random-baseline for 6 partitions of unequal size is roughly 0.32 (mass-weighted by partition fraction). So features ARE concentrating somewhat — but only weakly. Could be:
- Genuine cross-partition correlations the schema doesn't separate (which would be the *interesting* finding)
- Artifact of the L2-normalized + per-partition-pre-weighted compact format collapsing partition structure
- Just a top-k SAE artifact — features that activate on similar voicings end up sharing patterns across partitions

Phase 3 manual feature interpretation will tell the story. For now the SAE finds patterns, those patterns are mildly partition-respecting, and we shouldn't over-interpret 0.37.

## What changes for the May 19 Phase 1 agent

The agent's brief currently says:
> Use sae-lens library (Joseph Bloom et al.) - field-standard PyTorch SAE tooling.

Good — that's already pointing at the right tool. The new things the agent should know:

1. **Don't fall back to hand-rolled top-k** without `--allow-guardrail-violation` — the artifact won't pass schema validation.
2. **Expected dead-features rate with sae-lens + ghost grads on OPTIC-K v1.8** is ~5–15% (literature) but we haven't measured locally.
3. **Reconstruction MSE will be much lower than 0.05** — this corpus is easy to reconstruct (r² ≥ 0.998 with even bad dead-rate). The MSE guardrail is loose; the dead-rate guardrail is the binding one.
4. **Compact dim is 124, not 240** — the index file holds similarity partitions only (STRUCTURE 24, MORPHOLOGY 24, CONTEXT 12, SYMBOLIC 12, MODAL 40, ROOT 12). IDENTITY etc. are NOT in the file. Contract field `partitions_used` should reflect this — already did in PR #73.

## Reproducibility

```bash
python -X utf8 Scripts/optick_sae_train.py --epochs 50 --dict-size 1024
python -X utf8 Scripts/optick_sae_train.py --epochs 50 --dict-size 512
python -X utf8 Scripts/optick_sae_train.py --epochs 50 --dict-size 384
python -X utf8 Scripts/optick_sae_train.py --epochs 50 --dict-size 200
```

The trainer enforces guardrails by default — runs above will refuse to emit artifacts. Add `--allow-guardrail-violation` to emit anyway (for diagnostic sweeps).

## References

- `Scripts/optick_sae_train.py` — the trainer used here
- `Scripts/optick_sae_smoke_read.py` — OPTK v4 binary reader (used to validate the format before writing the trainer)
- `docs/contracts/2026-05-02-optick-sae-artifact.contract.md` — contract this finding affects
- `docs/plans/2026-05-02-arch-optick-sae-plan.md` — plan that needs an addendum
