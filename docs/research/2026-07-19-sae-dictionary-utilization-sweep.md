---
id: 2026-07-19-sae-dictionary-utilization-sweep
date: 2026-07-19
status: concluded
domain: embeddings
question: Does reducing dict_size fix the OPTIC-K SAE's dictionary under-utilization found in study 0'?
answer: "No — refuted. Reducing dict_size makes utilization WORSE. The lever is AuxK ghost-grads (already in ix, unused by the production artifact): at dict_size 1024 it takes the effective dictionary from 379 → 747 features (+97%) and partition purity 0.531 → 0.613, at no meaningful reconstruction cost."
hypotheses:
  - claim: dict_size 1024 is oversized for the concept count, so shrinking it (→512/256) will raise the fraction of features doing real work.
    refuted_if: A smaller dict_size yields a LOWER effective_dict_size fraction, or fails to beat the dict-1024 baseline.
    outcome: REFUTED — dict 512 → 26.4% and dict 256 → 33.2% effective, both far below dict 1024's 37.0% (AuxK off) / 73.0% (AuxK on).
tools: [ix-optick-sae trainer, effective_dict_size metric (ix PR #235)]
artifacts: state/research/sae-dictionary-utilization-sweep/
validators: [exact-baseline-reproduction]
confidence: high
supersedes: null
superseded_by: null
---

# SAE dictionary utilization — is `dict_size` the problem?

**Date:** 2026-07-19
**Type:** research (controlled experiment) — follow-up to [study 0'](2026-07-19-optick-sae-feature-atlas.md)
**Question:** Does reducing `dict_size` fix the under-utilization study 0' found (400/820 "alive" features near-dead)?

## TL;DR

**No — the hypothesis is refuted, and the real fix already exists.** Shrinking
`dict_size` makes utilization *worse* (512 → 26.4%, 256 → 33.2% effective).
The actual lever is **AuxK ghost-grads**, which landed in ix (#50) *after* the
production artifact was trained and has therefore never been used for it. Holding
everything else fixed, turning AuxK on takes the **effective dictionary from 379
→ 747 features (+97%)** and **partition purity 0.531 → 0.613 (+15%)**, at a
negligible reconstruction cost (R² 0.9996 → 0.9991).

**Actionable:** regenerate the production SAE artifact with the current trainer.
No code change required — the capability is already merged and on by default
(`--aux-alpha 0.10`).

---

## 1. Question

Study 0' found the SAE's dictionary is badly under-used: 400 of 820 "alive"
features fire strongly on <10 voicings, leaving an effective dictionary of ~379
of 1024. The obvious suspect was an oversized `dict_size`. If true, the fix is a
cheap hyperparameter change; if false, we need a different lever. Nobody should
retrain the production artifact on a guess.

## 2. Hypothesis

- **Claim:** `dict_size` 1024 is oversized; shrinking it will raise the fraction
  of features doing real work.
- **Refuted if:** a smaller `dict_size` yields a *lower* effective fraction.
- **Prior art:** Gao et al. 2024 (arXiv:2406.04093) introduces both the TopK SAE
  *and* the auxiliary loss for dead features — i.e. the literature's answer to
  dead features is an aux loss, not a smaller dictionary. That framing is what
  made AuxK worth including as a control arm.

## 3. Method (reproducible)

Four arms, **one variable at a time**, on the real corpus. All arms match the
production artifact's hyperparameters exactly (`--epochs 50 --batch-size 4096
--lr 0.001 --seed 42 --k-sparse 32`) against
`state/voicings/optick.index` (313,047 voicings, `OPTIC-K-v1.8`):

```
python crates/ix-optick-sae/python/train.py --index <optick.index> \
  --output-dir <out> --artifact-id <id> --k-sparse 32 \
  --epochs 50 --batch-size 4096 --lr 0.001 --seed 42 \
  --dict-size {1024|512|256} [--aux-alpha 0]      # 0 disables AuxK; default 0.10
```

**Metric:** `effective_dict_size` = features that are alive, *not* near-dead
(strong support ≥10 voicings at ≥50% of the feature's own max) and *not*
always-on (freq ≤0.2). Emitted by the trainer as of **ix PR #235**; thresholds
match study 0' so the numbers are directly comparable.

## 4. Evidence

`state/research/sae-dictionary-utilization-sweep/sweep-results.json`

| Arm | R² | dead% | alive | **effective** | **eff%** | purity |
|---|---|---|---|---|---|---|
| dict 1024, **AuxK ON** (0.10) | 0.9991 | 4.88 | 974 | **747** | **73.0** | **0.613** |
| dict 1024, AuxK OFF (0) | 0.9996 | 19.92 | 820 | 379 | 37.0 | 0.531 |
| dict 512, AuxK ON | 0.9993 | 26.17 | 378 | 135 | 26.4 | 0.462 |
| dict 256, AuxK ON | 0.9984 | 14.45 | 219 | 85 | 33.2 | 0.405 |

**Setup validation — the AuxK-OFF arm reproduces production exactly.** The
2026-06-14 production artifact reports R² 0.999559, `dead_features_pct` 19.92,
`alive` 820, `feature_partition_purity_mean` 0.5314 — and study 0' measured its
effective dictionary at 379. The AuxK-OFF arm reproduces **all five** figures.
That confirms the harness replicates production and that AuxK is the *only*
differing variable in the ON/OFF comparison.

## 5. Verdict

**CONCLUDED. Hypothesis refuted; a better lever identified.**

- **`dict_size` reduction is counterproductive.** 512 and 256 both land well
  below 1024 on effective utilization *and* on partition purity. Capacity was
  never the binding constraint — dead features were.
- **AuxK is the lever, and it is already merged.** Same seed, same everything
  else: effective dictionary **379 → 747 (+97%)**, purity **0.531 → 0.613**,
  reconstruction essentially unchanged (R² 0.9996 → 0.9991; MSE stays ~1e-6).
- **The production artifact is simply stale.** It predates ix #50, so it has
  never had AuxK applied. Regenerating it is a pure win with no code change.

**Confidence: high** — single-variable A/B plus an exact five-figure reproduction
of the production baseline.

**Caveats:** single seed (42); the ~192 near-dead features remaining at dict 1024
+ AuxK mean feature-splitting is *reduced, not solved*; purity is a proxy metric —
whether the +97% features are *interpretable* needs study 0's characterization
re-run on the new artifact.

- **One-way-door check:** none. Regenerating the SAE creates a **new dated
  artifact** with `links.supersedes`; the existing one is immutable and retained.
  It does **not** touch OPTIC-K dimensions or require a re-index.

## 6. Next

1. **Regenerate the production SAE artifact** with the current trainer (default
   `--aux-alpha 0.10`) — the actionable output of this study.
2. Re-run study 0's characterization on the new artifact: do the extra ~370
   effective features yield more *interpretable* detectors (PC-set / ICV classes),
   or just more features?
3. Optional: seed replication (3 seeds) to bound the variance on `effective_dict_size`.
