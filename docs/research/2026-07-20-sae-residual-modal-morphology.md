---
id: 2026-07-20-sae-residual-modal-morphology
date: 2026-07-20
status: concluded
domain: embeddings
question: Is the harmonically-unexplained residual of the OPTIC-K SAE real structure or noise — and if real, what is it?
answer: "Mostly NOT a vocabulary gap. Of 441 unexplained features, 281 (64%) are dominated by STRUCTURE — the one partition we DID have vocabulary for — so vocabulary can account for at most the ~154 MODAL/MORPHOLOGY-dominated ones (~35%). MODAL/MORPHOLOGY enrichment is a modest 1.28x/1.59x against the correct baseline, not the 2.5x an earlier draft claimed. The residual is coherent above a random-voicing baseline but that control cannot separate coherent from polysemantic. Separately and more consequentially: 40 of 124 compact dims (32.3%) are ALWAYS ZERO across the whole corpus."
hypotheses:
  - claim: The residual is either polysemantic noise, or concepts (modal / fingering) our harmonic attribute vocabulary could not name.
    refuted_if: Residual coherence is indistinguishable from a random baseline (⇒ noise), or the residual is dominated by partitions we already had vocabulary for (⇒ not a vocabulary gap).
    outcome: PARTIALLY REFUTED. Noise branch disfavored (coherence 0.872 vs 0.721) but not cleanly refuted — the control was random voicing SETS, not random DIRECTIONS. Vocabulary branch holds for only ~35% of the residual; the STRUCTURE-dominant 64% majority points at feature splitting / absorption / disjunctive features instead.
tools: [pyarrow, safetensors, OPTK metadata reader, optick.index compact vectors, fable-5]
artifacts: state/research/optick-sae-feature-atlas/
validators: [fable-5]
confidence: medium
supersedes: null
superseded_by: null
---

# What is the SAE's unexplained residual? (and: a third of OPTIC-K is dead)

**Date:** 2026-07-20
**Type:** research — third in the OPTIC-K SAE line ([study 0'](2026-07-19-optick-sae-feature-atlas.md) → [utilization sweep](2026-07-19-sae-dictionary-utilization-sweep.md) → this)
**Question:** Study 0' scored features against attributes we already had names for and filed the rest as "uninterpretable." Is that residual real structure or noise — and if real, what is it?

## TL;DR

Two results, and the **second matters more than the first**.

**1. The residual is mostly NOT a vocabulary gap.** Of 747 selective features, 138
are harmonically explained and **441 are residual**. They are coherent above a
random-voicing baseline (0.872 vs 0.721) — so probably not pure noise — but
**281 of the 441 (64%) are dominated by STRUCTURE**, the one partition our
attributes *did* cover, and are unexplained anyway. Vocabulary can therefore
account for at most the ~154 MODAL/MORPHOLOGY-dominated features (~35%). The
MODAL/MORPHOLOGY enrichment is a modest **1.28× / 1.59×** against the correct
baseline (all 747 selective) — an earlier draft of this study claimed 2.5× by
using the *vocabulary-selected* explained group as the denominator, which is a
selection artifact. **Corrected after adversarial review.**

**2. 40 of 124 compact dims (32.3%) are always exactly zero** across all 313,047
voicings. **CONTEXT is 11/12 dead** (one live dimension). **MODAL is 16/40 dead.**
A third of the trained embedding slice carries no information at all. This is an
embedding defect that affects everything downstream — similarity search, RAG
retrieval, and this SAE — and it is far more actionable than anything else here.

**Corollary that survived review: do not optimize `feature_partition_purity`.**
Within STRUCTURE-dominant features only (controlling for partition width),
residual purity is **0.691 (n=281)** vs explained **0.648 (n=122)**, t ≈ 2.7 — so
the purity-vs-nameability dissociation is real and not a width artifact. Purity is
a thermometer; read it, never target it.

## 1. Question

Features matching known attributes teach us nothing new — the only place new
musical knowledge can live is the features matching *nothing we can already name*.
Study 0' measured that residual and discarded it as failure. Is anything there?

## 2. Hypothesis

- **Claim:** the residual is either (a) polysemantic noise, or (b) concepts
  (modal / fingering) our harmonic vocabulary cannot express.
- **Refuted if:** coherence ≈ random (⇒ noise), or the residual is dominated by
  partitions we already had vocabulary for (⇒ not a vocabulary gap).
- **Outcome:** partially refuted — see §5.

## 3. Method (reproducible)

On the regenerated artifact `state/quality/optick-sae/2026-07-20/` (dict 1024,
k 32, AuxK 0.10). Selective features only (`strong_support ≥ 10`, `freq ≤ 0.2`)
→ 747.

1. **Attribute scoring** — best-by-lift precision over each feature's
   strong-activation set (≥50% of its own max), for **harmonic** attributes
   (pc-set, ICV, cardinality, bass PC, instrument) and **geometry** attributes
   from the fret diagram (span, min fret, open count, sounding count, max repeat).
   Residual := best harmonic precision < 0.5; explained := ≥ 0.8.
2. **Coherence control** — mean pairwise similarity of a feature's strong voicings
   over the OPTK compact vectors, vs **random voicing sets** of comparable size.
3. **Partition localization** — dominant partition of each decoder atom
   (`argmax_p ‖w[p]‖²/‖w‖²`) from `sae_weights.safetensors`, **baselined against
   all 747 selective features** (the correction; see §4).
4. **Dead-dimension census** — per-dim `max|v|` over all 313,047 corpus vectors.

## 4. Evidence

**Counts.** 747 selective → 138 explained (P ≥ 0.8), **441 residual** (P < 0.5),
**168 middle band** (0.5 ≤ P < 0.8). Geometry rescued only **2** of the 441; the
20 apparent `nopen` hits all have lift exactly 1.0 (pure base-rate artifact).

**Partition localization — with the correct baseline.**

| group | n | STRUCTURE | MODAL | MORPHOLOGY | SYMBOLIC |
|---|---|---|---|---|---|
| **all selective (baseline)** | 747 | 73.0% | 19.3% | 6.4% | 1.3% |
| residual | 441 | **63.7%** | 24.7% | 10.2% | 1.4% |
| explained | 138 | 88.4% | 10.1% | — | 1.4% |

Enrichment of the residual **vs all-selective**: MODAL **1.28×**, MORPHOLOGY
**1.59×**, STRUCTURE 0.87×. Against the *explained* group it looks like 2.5×, but
that group is mechanically STRUCTURE-heavy *because every scoring attribute is
STRUCTURE vocabulary* — a selection artifact. **The residual is majority
STRUCTURE-dominant (281/441).**

**Coherence** (higher = tighter cluster; partition-weighted vectors, so only the
between-group comparison is meaningful)

| group | n | mean | p10 | p90 |
|---|---|---|---|---|
| explained | 60 | 1.064 | 0.980 | 1.131 |
| residual | 60 | 0.872 | 0.790 | 0.969 |
| random voicing sets | 60 | 0.721 | 0.704 | 0.738 |

**Purity dissociation, width-controlled.** Within STRUCTURE-dominant features only:
residual purity **0.691 (n=281)** vs explained **0.648 (n=122)**, t ≈ 2.7. The
headline gap (0.695 vs 0.633) is inflated by composition, but the effect survives
stratification.

**Dead-dimension census (the consequential finding).** Per-dim `max|v|` over all
313,047 voicings: **40 of 124 compact dims are identically zero (32.3%)**.

| partition | dims | dead | live |
|---|---|---|---|
| STRUCTURE | 24 | 5 | 19 |
| MORPHOLOGY | 24 | 5 | 19 |
| **CONTEXT** | 12 | **11** | **1** |
| SYMBOLIC | 12 | 3 | 9 |
| **MODAL** | 40 | **16** | **24** |
| ROOT | 12 | 0 | 12 |

Independently predicted by source reading: `MorphologyVectorService` writes
nothing past local index 18 (⇒ compact 43–47 dead), and `ModalVectorService`
fills only 33 of its 40 slots (⇒ the MODAL tail dead). CONTEXT having **one** live
dimension means it is effectively not a partition at all.

## 5. Verdict

**CONCLUDED, with the headline corrected after adversarial review.**

- **The residual is not primarily a vocabulary gap.** 64% of it is
  STRUCTURE-dominant — the partition we *did* have vocabulary for — and remains
  unexplained. The leading candidates for that majority are **feature splitting /
  absorption / disjunctive features** (Chanin 2024 absorption, flagged as a
  guardrail in study 0' and still never measured), not missing concepts. Building
  modal + morphology vocabulary is still worth doing, but its expected yield is
  **~150 features, not "a large share of 441."**
- **Noise is disfavored, not refuted.** Beating random *voicing sets* is nearly
  automatic for any linear direction, since a feature's strong set is selected by a
  direction in that same space. The correct control is the strong sets of **random
  directions**; until that is run, "coherent" is not cleanly separated from
  "polysemantic blend of 2–3 clusters," which is exactly what 44%-of-the-way-to-
  explained looks like.
- **Never optimize partition purity** — survives the width confound (above).
- **The real finding is the dead dimensions.** A third of the trained slice is
  structurally zero, CONTEXT is 11/12 dead, and MODAL is 40% dead. This wastes
  similarity-search capacity, distorts the partition weighting, and means SAE
  features "dominated by MODAL" actually live in 24 dims, not 40.

**Confidence: medium** — counts and the dead-dim census are exact and reproducible;
the coherence inference is weakened by its control; enrichment is real but modest.

**Caveats:** "explained" is defined by *our* attribute list, so the 138/441 split is
vocabulary-relative; coherence used a 60-of-441 subsample; the geometry attributes
were badly formulated (see §6) so the "not geometry" branch is under-tested.

- **One-way-door check:** none. Read-only analysis over a committed artifact.

## 6. Next

1. **File the dead-dimension defect** — highest value, independent of SAE work.
2. **Re-score with correct attribute vocabulary.** Source reading (Fable 5) shows
   my attributes were wrong for both partitions:
   - **MORPHOLOGY is mostly pitch classes, not geometry:** dims 0–11 are a *bass
     pitch-class one-hot* (12 of 24), 15–16 melody-PC sin/cos, with only span (12),
     note count (13), avg fret (17) and barre (18) as geometry. My "min fret" and
     "max repeated fret" were off-spec; "open-string count" is not encoded at all.
     Correct attributes: **bassPitchClass, averageFret, melodyPitchClass, barreRequired**.
   - **MODAL is a 33-mode compatibility profile** (per-mode fit score with a
     conflict veto), not a single mode label. Correct attributes:
     **mode-compatibility set / argmax best-fit mode (34 values)**, **modal family
     (ICV family, ~200 values)**, **root-relative PC-set id (≤2048, the ceiling)**.
3. **Run the random-direction control** to settle coherent-vs-polysemantic.
4. **Measure absorption** (Chanin) on the STRUCTURE-dominant residual majority —
   the hypothesis this study's evidence actually points to.

## 7. Closing tests (2026-07-20) — the SAE line is closed

Both tests §6 called for were run. Both point the same way.

**Corrected-vocabulary re-score.** Scoring the 441 residual features against the
attributes Fable 5's source reading identified (bassPC, **melodyPC**, avgFret,
barre, root-relative PC-set id [1015 values], ICV family [117]) — per-attribute
precision, *not* best-by-lift — newly explains **57 of 441 (13%)**: melodyPC 48,
ICV family 9. `melodyPitchClass` was a genuine gap (2 dedicated embedding dims, no
attribute), but the total yield is far below the ~35% ceiling. Even the
root-relative PC-set id (the *maximal-information* attribute, which MODAL is a
deterministic function of) explained none at threshold.

**Random-direction control** — the null §5 said was missing:

| control | n | mean | p10 | p90 |
|---|---|---|---|---|
| residual features | 60 | 0.871 | 0.795 | 0.967 |
| **random directions** | 60 | **0.778** | 0.726 | 0.818 |

Against the correct null the gap largely collapses (earlier random-*voicing-set*
control: 0.721), and residual p10 now **overlaps** random-direction p90. The claim
"essentially every residual feature is genuinely coherent" is dead.

**Verdict: the residual is not a discovery vein.** Correct vocabulary resolves 13%;
coherence is only modestly above a proper null with overlapping distributions. Per
this study's own §6 exit criterion — "if it does not resolve, that is the signal to
stop investing in the SAE" — **this line is closed.** The durable outputs are the
[dead-dimension defect (#552)](https://github.com/GuitarAlchemist/ga/issues/552),
the +97% utilization fix, and "never optimize partition purity."

Evidence: `state/research/optick-sae-feature-atlas/pass9-closing-tests.json`.
