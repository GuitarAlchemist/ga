---
id: 2026-07-19-sae-dictionary-width-utilization
date: 2026-07-19
status: concluded
domain: embeddings
question: Does increasing the OPTIC-K SAE dictionary width raise the *effective* dictionary (features doing distinctive work), or does it only add near-dead and split features?
answer: "WITHDRAWN — the study is invalid. All 17 runs left `--batch-size` unpinned at the default 256 while the production baseline used 4096; rerunning at bs=4096 reproduces the June baseline bit-for-bit and dissolves the reported residual. The headline inverts at production batch size: AuxK, which this study called a regression worth reverting, actually cuts dead features 19.92% → 4.88%. Retained as a near-miss record — acting on it would have reverted PR #50, removing a real improvement. Standing caveat: `effective_dict_pct` has a built-in interior optimum and must be fixed or dropped before reuse."
hypotheses:
  - claim: effective_dict_size grows sub-linearly with dict_size and plateaus, because the corpus contains a bounded number of real concepts (2509 distinct pc-sets over a 124-dim input); effective_dict_pct therefore falls monotonically as width grows.
    refuted_if: effective_dict_pct stays flat or rises across the sweep, i.e. added capacity is absorbed into genuinely distinctive features rather than near-dead ones.
  - claim: near_dead_count grows super-linearly with width, reproducing the "larger expansion -> higher dead fraction" result from the LLM-SAE literature in a symbolic-music setting.
    refuted_if: near_dead_count stays proportionally constant across widths.
tools: [ix-optick-sae, paper-search]
artifacts: state/research/sae-dictionary-utilization-sweep/  # shares the sweep study's artifacts; no separate dir
validators: [fable-5]
confidence: high  # high confidence that the study is INVALID; see answer
supersedes: null
superseded_by: null
---

# SAE dictionary width vs. effective utilization on OPTIC-K

**Date:** 2026-07-19
**Type:** research (not a commitment to build)
**Question:** Does increasing the OPTIC-K SAE dictionary width raise the effective dictionary, or only add near-dead and split features?

## TL;DR

**The study is invalid and its conclusions are withdrawn.** Every one of its 17 runs left
`--batch-size` unpinned at the default 256; the production baseline it compared against used 4096.
Rerunning at bs=4096 reproduces the June baseline bit-for-bit, dissolving the "unexplained 30pp
residual" entirely. Worse, the headline claim inverts at the production batch size: AuxK, which
this study called a regression worth reverting, actually **cuts dead features 19.92% → 4.88% and
raises purity** at bs=4096. Reverting PR #50 on this evidence would have removed a real improvement.

The single most important caveat for future work: `effective_dict_pct` — the metric introduced by
`648d945` specifically to measure dictionary utilization — has a **built-in interior optimum**
(`ALWAYS_ON_FREQ` inflates at small widths, the absolute `MIN_STRONG_SUPPORT` floor bites at large
ones), so it would show a hump for an equally-good model family. Fix or drop it before reuse.

---

## 1. Question

Study 0' ([2026-07-19-optick-sae-feature-atlas](2026-07-19-optick-sae-feature-atlas.md)) found the
2026-06-14 artifact's 1024-wide dictionary contains only ~379 features doing distinctive work —
400 of 820 "alive" features fire strongly on fewer than 10 voicings, and 41 are always-on. It also
found heavy feature splitting (median recall 0.1–0.23), meaning single musical concepts are smeared
across many features.

That is a measurement, not an explanation. The open decision it blocks: **is `dict_size=1024` the
wrong width?** If utilization improves at some other width, the atlas is fixable by retraining and
a future rebuild should adopt that width. If it doesn't, splitting is intrinsic to the embedding
geometry and no retraining will produce a clean one-feature-one-concept atlas — which would redirect
effort away from SAE tuning entirely.

Worth an experiment rather than a guess because a full training run costs ~70 seconds.

## 2. Hypothesis

- **Claim:** `effective_dict_size` grows sub-linearly and plateaus; `effective_dict_pct` falls
  monotonically with width. The OPTIC-K input is only 124-dim and the corpus holds 2509 distinct
  pitch-class sets, so the number of real concepts is bounded well below 4096.
- **Refuted if:** `effective_dict_pct` is flat or rising across the sweep.
- **Secondary claim:** `near_dead_count` grows super-linearly with width.
- **Refuted if:** near-dead share stays proportionally constant.

**Prior art (checked before running — rigor rule #3):**

- Chanin et al. 2024, *A is for Absorption* ([arXiv:2409.14507](https://arxiv.org/abs/2409.14507)) —
  establishes feature splitting and absorption in LLM SAEs, and reports directly against the naive
  fix: **"varying SAE sizes or sparsity is insufficient to solve the absorption issue."**
- Gao et al. 2024, *Scaling and evaluating sparse autoencoders* — TopK SAE scaling laws.
- Bricken et al. 2023 / Templeton et al. 2024 — feature splitting as dictionary width grows.
- Broader survey finding: smaller expansion factors give broader, entangled features; larger
  expansions encourage splitting **at the cost of a substantially higher dead fraction**.

So the *direction* of this result is largely predicted by existing work. This study is therefore a
**replication in a novel regime**, not a discovery: prior art measures SAEs over LLM residual
streams (thousands of dims, open-ended concept inventory). OPTIC-K is 124-dim symbolic music-theory
embeddings with a closed, enumerable concept inventory — a setting where the plateau should be
sharply visible rather than asymptotic. A null result here (utilization flat across width) would be
a genuine surprise *against* the literature and the more interesting outcome.

## 3. Method (reproducible)

Sweep `--dict-size` only; everything else pinned to the 2026-06-14 baseline (`k=32`, `seed=42`,
`epochs=50`, `held-out-pct=0.05`).

```bash
for D in 256 512 1024 2048 4096; do
  python ix/crates/ix-optick-sae/python/train.py \
    --index   ga/state/voicings/optick.index \
    --output-dir <out>/d$D \
    --artifact-id sweep-d$D \
    --dict-size $D --k-sparse 32 --seed 42 --epochs 50
done
```

Dependent variables, read from each run's `optick-sae-artifact.json` `features_summary`:
`effective_dict_size`, `effective_dict_pct`, `near_dead_count`, `always_on_count`, `alive`,
plus `metrics.reconstruction_mse` and `purity_mean` as reconstruction/quality guardrails.

- **Inputs:** `ga/state/voicings/optick.index` — 183,773,176 bytes, 313,047 voicings, OPTK v4-pp-r
  (dim 124), mtime 2026-04-19. Read-only; no rebuild, so no mmap lock conflict with GaApi.
- **Environment:** ix branch `feat/optick-sae-effective-dict` at commit `648d945` (which adds the
  `effective_dict_*` metrics this study depends on); torch 2.11.0+cpu, numpy 2.4.2, pyarrow 24.0.0,
  Python 3.14.
- **Note:** the `effective_dict_*` metrics are *new in this commit* and unvalidated against a
  second implementation. Smoke-tested on the synthetic 5k corpus (`effective_dict_pct = 79.39`,
  `near_dead = 190`, `always_on = 7`) — plausible, but the sweep is simultaneously their first
  real-data exercise. Treat metric-artifact risk as live; study 0' was reversed twice by exactly
  this class of error.

### ⚠️ Confound discovered mid-run: AuxK postdates the baseline

The 2026-06-14 artifact — the one study 0' analysed, and the source of the "400 of 820 near-dead,
effective dictionary ~379" finding — was trained **without AuxK ghost gradients**. AuxK landed in
`d731cac` on **2026-07-18** (PR #50), a month later. Confirmed by log format: the baseline's epoch
lines carry no `aux=` term; every run in this sweep does (`--aux-alpha` defaults to 0.10).

AuxK exists specifically to revive dead features — it perturbs the exact dependent variable this
study measures. Two consequences:

1. **The width comparison remains valid.** All five sweep runs share one code version, so the
   trend across `dict_size` is internally consistent.
2. **Cross-referencing sweep numbers to study 0's 379 is invalid.** Different training objective.
   Any claim of the form "utilization improved/worsened since the atlas" is unsupported.

This also means **study 0's central utilization finding was measured on an artifact predating the
mitigation for the very problem it identified.** The `d1024` point in this sweep is therefore an
incidental re-measurement of study 0's headline claim under current code.

**Added control run** to isolate the AuxK effect, holding width fixed at the baseline's 1024:

```bash
python train.py --index <optick.index> --output-dir <out>/d1024-noaux \
  --artifact-id sweep-d1024-noaux --dict-size 1024 --k-sparse 32 \
  --seed 42 --epochs 50 --aux-alpha 0
```

`d1024` vs `d1024-noaux` isolates AuxK; the sweep isolates width. Neither alone would have been
interpretable.

## 4. Evidence

### 4.1 Width sweep, seed 42 (n=1 per condition)

| dict_size | aux | dead % | purity_mean | purity_p10 | r² | eff_dict_pct |
|---:|---:|---:|---:|---:|---:|---:|
| 256 | 0.10 | 68.4 | 0.4226 | 0.2651 | 0.9998 | — |
| 512 | 0.10 | 75.4 | 0.4207 | 0.2689 | 0.9999 | — |
| 1024 | 0.10 | 69.6 | 0.5196 | 0.2858 | 0.9999 | — |
| 2048 | 0.10 | 63.2 | 0.5949 | 0.3309 | 1.0000 | — |
| 4096 | 0.10 | 8.0 | 0.7442 | 0.4657 | 0.9995 | 85.91 |
| 8192 | 0.10 | 18.4 | 0.7154 | 0.4256 | 0.9997 | 73.50 |

`eff_dict_pct` is available only where the run wrote an artifact. `DEAD_FEATURES_PCT_GUARDRAIL = 30.0`
made every run above 30% dead exit 3 before artifact write, so those rows are read from
`training.log`. **Harness note:** the guardrail is correct for production but hostile to a sweep,
which deliberately visits bad regimes. A `--no-guardrail` (measure-don't-ship) flag would make
width sweeps first-class.

### 4.2 Seed replicates — the sweep's headline was an artifact

The dead% series above is non-monotonic (512 worse than 256) and contains a 55-point step between
2048 and 4096. That is what large run-to-run variance looks like when sampled once. Three
additional seeds at the two widths bracketing the step:

| dict_size | seed 42 | seed 1 | seed 2 | seed 3 | mean excl. 42 |
|---:|---:|---:|---:|---:|---:|
| 2048 dead % | **63.2** | 21.0 | 18.7 | 14.5 | **18.1** |
| 4096 dead % | 8.0 | 8.6 | 7.2 | 8.0 | **7.9** |
| 2048 purity | **0.5949** | 0.6885 | 0.6792 | 0.6998 | **0.6892** |
| 4096 purity | 0.7442 | 0.7654 | 0.7379 | 0.7532 | **0.7502** |

**Seed 42 draws a ~3× outlier at width 2048** (63.2% dead vs 14.5–21.0% for three other seeds;
purity 0.59 vs 0.68–0.70). At 4096 all four seeds agree tightly (7.2–8.6%).

So the "55-point cliff at 4096" reported by the seed-42 sweep is **mostly an initialization
artifact.** The real 2048→4096 improvement is ~18% → ~8% dead: a genuine and useful advantage,
but roughly a fifth of the apparent effect, and with no threshold character.

### 4.3 ⚠️ Seed 42 is the pinned production seed

`--seed 42` is the default, is what the 2026-06-14 production artifact was trained with, is what
study 0' analysed, and is what this sweep pinned in order to match the baseline. It is now known to
draw pathologically at at least one width. Consequences:

- Any single-seed result on this corpus — including study 0's — carries unquantified init variance.
- The AuxK comparison in §4.4 was originally run *only* at seed 42, so its magnitude was not
  established independently of the seed that misbehaved. That is what §4.4 re-tests.

### 4.4 AuxK, paired at fixed width and seed

Seed-42 pairs (original, now suspect):

| width | dead % aux=0.10 | dead % aux=0 | purity aux=0.10 | purity aux=0 |
|---:|---:|---:|---:|---:|
| 1024 | 69.6 | 48.6 | 0.5196 | 0.5176 |
| 4096 | 8.0 | 2.1 | 0.7442 | 0.7673 |

Direction is consistent — disabling AuxK lowers dead features and leaves purity unchanged or
slightly better — which is the opposite of AuxK's stated purpose (ghost gradients exist to *revive*
dead features). AuxK landed 2026-07-18 in `d731cac` (PR #50).

Seed-independent re-test, width 1024, four independent seeds:

| seed | dead % aux=0.10 | dead % aux=0 | Δ | purity aux=0.10 | purity aux=0 |
|---:|---:|---:|---:|---:|---:|
| 42 | 69.6 | 48.6 | **−21.0** | 0.5196 | 0.5176 |
| 1 | 71.4 | 50.5 | **−20.9** | 0.5051 | 0.4918 |
| 2 | 73.6 | 50.2 | **−23.4** | 0.5011 | 0.5006 |
| 3 | 73.8 | 51.2 | **−22.6** | 0.5114 | 0.5130 |
| **mean** | **72.1** | **50.1** | **−22.0** | 0.5093 | 0.5058 |

**The effect replicates across every seed with a tight spread (−20.9 to −23.4).** Enabling AuxK
costs ~22 percentage points of dead features at width 1024 and buys no measurable purity (Δ purity
is within ±0.014 and changes sign across seeds). Reconstruction is unaffected (r² = 1.0000 in all
six runs).

This is the opposite of the mechanism's purpose. AuxK ghost gradients are meant to revive dead
features; as configured (`--aux-alpha 0.10`, `--aux-k 64`) they roughly double the dead fraction.

**Correction to §4.3:** seed 42 is *not* generally pathological. At width 1024 it is the best of the
four seeds (69.6 vs 71.4/73.6/73.8), and at 4096 all seeds agree. Its outlier behaviour is specific
to width 2048. The §4.3 warning stands for that width and for single-seed designs generally, but it
does **not** undermine the seed-42 AuxK pair, which now sits squarely in line with the other three.

### 4.6 The §4.5 residual is real, not seed noise

With AuxK off at width 1024, all four seeds land at 48.6–51.2% dead (mean 50.1%). The 2026-06-14
baseline, same width, same no-AuxK condition, reported **19.9%**. That ~30-point gap is consistent
across every seed tested, so it is not initialization variance. **A second, unidentified change
between the June baseline and current `main` roughly doubled the dead-feature rate**, independent
of AuxK. This is now the largest open question from this study — larger in magnitude than the AuxK
effect itself.

### 4.5 Unexplained residual

`d1024-noaux` (seed 42) = 48.6% dead. The 2026-06-14 baseline, same width, no AuxK, seed 42 = 19.9%.
AuxK explains 69.6 → 48.6. **Nothing here explains 48.6 → 19.9.** A second change between the June
baseline and current `main` is unaccounted for. Candidates not yet checked: a changed `dead`
threshold or definition, torch version, corpus load path. Until identified, no claim of the form
"utilization changed since the atlas" is supportable.

## 5. Verdict

**Answer: the study is invalid as designed. Every run was executed at an unpinned batch size the
production pipeline never uses. Do not act on §4.**

### 5.1 The root cause — an unpinned parameter

§3 pinned `k`, `seed`, `epochs`, `held-out-pct`. It did **not** pin `--batch-size`.
`train.py:688` defaults it to **256**. The 2026-06-14 baseline artifact records
`"batch_size": 4096`, explicitly passed. Every number in §4 was therefore produced at 1/16th the
production batch size — a config that has never shipped.

Mechanism, visible in the logs: bs=256 gives 16× more optimizer steps per epoch. Its epoch-5 loss
(0.000002) is ~100× further converged than bs=4096's epoch-5 (0.000209), placing it deep in the
TopK "rich get richer" regime where features die.

### 5.2 What the independent review established

The `dead` measurement is **unchanged** since bd7a727 — `train.py:389-397`, `ever_active |= (a > 0).any(dim=0)`
over the training split, threshold `> 0` (scale-free). Not a definitional artifact.

Rerunning `dict=1024, aux=0, seed=42, epochs=50` at **batch 4096** under HEAD reproduces the June
baseline **bit-for-bit**: `mse=1.41e-06, r2=0.999559, dead=19.92%, purity_mean=0.531448,
near_dead=400, always_on=41, effective_dict=379` — identical to the baseline artifact *and* to
study 0's derived counts, with a matching per-epoch loss trajectory.

### 5.3 Claim-by-claim

| claim | verdict | reason |
|---|---|---|
| A — 4096 beats 1024 | **WEAKENED** | Real within bs=256. But at production config (bs=4096, aux on) width **1024 already gives 4.88% dead, effective_dict 747** — better than this study's 4096-at-bs256. No data at 4096-width/4096-batch, so "retrain wider" is unsupported at the operating point. Purity leg separately confounded (5.4). |
| B — AuxK is a regression (PR #50) | **REFUTED** | At bs=4096 the sign **flips**: aux-on gives 4.88% vs 19.92% dead, purity 0.531 → 0.613. The 4-seed replication was real but bs=256-only. Reverting PR #50 on this evidence would have removed a genuine improvement. |
| C — seed 42 outlier at 2048 | **WEAKENED** | Numbers stand, but n=4 cannot separate "outlier" from heavy-tailed init variance, and the observation lives entirely inside the anomalous bs=256 regime. |
| D — unexplained ~30pp residual | **DISSOLVED** | It was the batch size. Proven bit-for-bit, not argued. Also exonerates torch version and corpus load path. |
| E — hypotheses refuted, width beats Chanin | **REFUTED (both legs)** | See 5.4–5.5. |

### 5.4 My own metric has the artifact it was built to detect

The "interior optimum" in H1's refutation rests on **two** within-config points (85.91 @4096,
73.50 @8192) — which show only a *fall*, consistent with H1. The "rise" was inferred from nothing:
guardrail exit-3 destroyed every lower-width artifact.

Worse, `effective_dict_pct` (added in `648d945`, first exercised by this study) has a **built-in
interior hump**. With `k=32` fixed, `ALWAYS_ON_FREQ=0.2` sits only 1.6× above the enforced mean
firing rate at width 256, mechanically inflating always-on counts at small widths; while the
*absolute* `MIN_STRONG_SUPPORT=10` floor bites as 1/width at large widths. **An equally-good model
family would show an interior optimum on this metric.** This is the same structural-blindness class
that reversed study 0' twice — reproduced by the very metric introduced to measure it.

Purity is also confounded: `purity_mean` (`train.py:437-439`) averages over **all** atoms including
dead ones. With aux=0, dead atoms keep their random-orthogonal init, whose purity measures ~0.329.
Alive-only purity is ~0.69 (1024) vs ~0.79 (4096) — so roughly half the reported 0.51-vs-0.75 gap
is dead-atom dilution, i.e. the purity "advantage" substantially re-expresses the dead% difference
rather than adding independent evidence.

### 5.5 The prior-art framing was a category error

Chanin 2024 claims width does not fix **absorption/splitting**. This study measured **dead% and
purity**. Splitting was never measured across widths. Chanin's prediction was not tested, let alone
overturned. The "symbolic music behaves differently from LLM residual streams" framing is
unsupported.

### 5.6 Reconstruction is saturated

r² = 1.0000, mse ≈ 0 in every 1024 run — a 124-dim input with k=32 adaptive atoms reconstructs
trivially under any config. Every "at unchanged reconstruction quality" statement in §4 carries no
information, and the dead%/purity differences measure **optimizer dynamics, not representational
necessity**.

### 5.7 Independent finding worth keeping

PR #50's AuxK is **buggy regardless**: `auxk_reconstruct` (`train.py:170`) adds `b_dec` to its
output while the target is the ~zero-mean residual (`train.py:347`). Gao et al. reconstruct the
residual without the bias. Worth fixing on its own merits — but note it *helps* at bs=4096 even
with the bug, so this is a correctness fix, not a regression revert.

- **Confidence:** high in the invalidation (bit-for-bit reproduction); the original claims are
  withdrawn, not merely doubted.
- **Independent validation:** adversarial review, Fable 5 — which found the batch-size confound,
  the built-in metric hump, the purity dilution, and the Chanin category error. Four of five
  headline claims refuted or dissolved. Recorded per rigor rule #4.
- **One-way-door check:** the study recommends **no** width, dict-size, or schema change. The
  proposed "retrain at 4096" would have been a production change founded on a non-production config.

## 6. Next

1. **Duplicate work to reconcile:** `ga/state/research/sae-dictionary-utilization-sweep/sweep-results.json`
   is a sweep run the *same evening at 20:48* (this doc: 23:02) at bs=4096. It should have been found
   before spending 17 runs. Check `state/research/` before starting any study.
2. **Rerun properly** if the width question is still wanted: pin `--batch-size 4096`, sweep width,
   report alive-only purity, and fix or drop `effective_dict_pct` first (5.4).
3. **Fix the AuxK `b_dec` bug** (5.7) as an independent correctness issue.
4. **Harness:** add a measure-don't-ship flag so guardrail exit-3 stops destroying sweep artifacts,
   and make `train.py` record every training parameter into the artifact so an unpinned default
   cannot silently differ from the baseline again.

- **Independent validation:** required before this leaves `active` — rigor rule #4. Fable 5 must
  adversarially review, with specific attention to whether `effective_dict_pct` is structurally
  capable of moving across widths at all (the failure mode that invalidated two passes of study 0').

## 6. Next

_(pending)_
