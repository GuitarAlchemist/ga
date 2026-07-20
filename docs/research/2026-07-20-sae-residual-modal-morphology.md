---
id: 2026-07-20-sae-residual-modal-morphology
date: 2026-07-20
status: concluded
domain: embeddings
question: Is the harmonically-unexplained residual of the OPTIC-K SAE real structure or noise — and if real, what is it?
answer: "Real, and identifiable. 441 of 747 selective features are unexplained by any harmonic attribute, yet they are coherent (intra-feature similarity 0.872 vs 0.721 random) and MORE cleanly partition-localized than the explained ones (purity 0.695 vs 0.633). They concentrate in MODAL (25% vs 10%) and MORPHOLOGY (10% vs ~0%) — partitions for which we had built no attribute vocabulary. The gap is our vocabulary, not the SAE."
hypotheses:
  - claim: The residual is either polysemantic noise, or fingering-geometry ("playability") concepts our harmonic attributes could not name.
    refuted_if: Residual coherence is indistinguishable from random voicing sets (⇒ noise), or geometry attributes explain most of it.
    outcome: BOTH REFUTED. Not noise (coherence well above random, p10 clears random's p90). Not geometry either — crude geometry attributes rescued only 2 of 441. The real locus is MODAL + MORPHOLOGY.
tools: [pyarrow, safetensors, OPTK metadata reader, optick.index compact vectors]
artifacts: state/research/optick-sae-feature-atlas/
validators: [random-baseline-control, partition-localization-cross-check]
confidence: medium-high
supersedes: null
superseded_by: null
---

# The SAE residual is modal and morphological, not noise

**Date:** 2026-07-20
**Type:** research — third in the OPTIC-K SAE line ([study 0'](2026-07-19-optick-sae-feature-atlas.md) → [utilization sweep](2026-07-19-sae-dictionary-utilization-sweep.md) → this)
**Question:** Study 0' scored features against attributes we already had names for and filed the rest as "uninterpretable." Is that residual real structure or noise — and if real, what is it?

## TL;DR

**Real, coherent, and locatable.** On the regenerated (AuxK) artifact, **441 of 747**
selective features are unexplained by any harmonic attribute. They are **not
noise**: intra-feature similarity 0.872 vs **0.721** for random voicing sets, with
their p10 (0.790) clearing random's p90 (0.738) — essentially every residual
feature is genuinely coherent. They are also **more** cleanly partition-localized
than the explained features (purity 0.695 vs 0.633), and they concentrate in
**MODAL (25% vs 10%)** and **MORPHOLOGY (10% vs ~0%)**.

**The limitation was our vocabulary, not the model.** Every attribute we scored
with (pitch-class set, ICV, cardinality, root, instrument) is *STRUCTURE*
vocabulary — which is why 88% of "explained" features are STRUCTURE features.
MODAL is the largest partition in the trained slice (40 of 124 dims) and we never
built a single attribute for it.

**Corollary — do not optimize partition purity.** Purity measures agreement with
the partition *hypothesis*, not correctness; the residual has *higher* purity
than the explained set while being *less* nameable. Purity is a thermometer.

## 1. Question

Study 0' characterized features by matching them to known attributes. Features
matching known attributes teach us nothing new — the only place new musical
knowledge can live is the features matching *nothing we can already name*. That
residual was measured and discarded as failure. Is anything there?

## 2. Hypothesis

- **Claim:** The residual is either (a) polysemantic noise, or (b) fingering
  geometry / "playability" concepts our harmonic vocabulary cannot express.
- **Refuted if:** residual coherence ≈ random (⇒ noise), or geometry attributes
  explain most of it.
- **Outcome:** **both refuted** — see §4.

## 3. Method (reproducible)

On the regenerated artifact `state/quality/optick-sae/2026-07-20/` (dict 1024,
k 32, AuxK 0.10 — see the utilization sweep study). Selective features only
(`strong_support ≥ 10`, `freq ≤ 0.2`) → 747.

1. **Attribute scoring.** Best-by-lift precision over each feature's
   strong-activation set (activation ≥ 50% of the feature's own max), for
   **harmonic** attributes (pc-set, ICV, cardinality, bass PC, instrument) and
   **geometry** attributes derived from the fret diagram (span, min fret, open
   count, sounding-string count, max repeated fret).
   Residual := best harmonic precision < 0.5.
2. **Coherence control.** Mean pairwise similarity of a feature's strong voicings
   using the OPTK compact vectors, vs **random voicing sets of comparable size**
   (the control that separates "coherent" from "noise").
3. **Partition localization.** Dominant OPTIC-K partition of each decoder atom
   (`argmax_partition ‖w[partition]‖² / ‖w‖²`) from `sae_weights.safetensors`,
   compared between explained and residual groups.

## 4. Evidence

**Attribute scoring** — 747 selective; **138** harmonically explained (P ≥ 0.8);
**441** residual (P < 0.5). Geometry rescued only **2** of the 441 (P ≥ 0.8,
lift ≥ 2); the 20 apparent `nopen` hits are near chance and are rejected by the
lift filter.

**Coherence (higher = tighter cluster)**

| group | n | mean | p10 | p90 |
|---|---|---|---|---|
| harmonically explained | 60 | 1.064 | 0.980 | 1.131 |
| **residual** | 60 | **0.872** | 0.790 | 0.969 |
| random voicing sets (control) | 60 | 0.721 | 0.704 | 0.738 |

Residual sits ~44% of the way from random to fully-explained; its p10 exceeds the
control's p90 ⇒ **not noise**.

**Partition localization**

| group | n | dominant partition | mean partition-purity |
|---|---|---|---|
| explained | 138 | STRUCTURE 88%, MODAL 10%, SYMBOLIC 1% | 0.633 |
| **residual** | 441 | STRUCTURE 64%, **MODAL 25%**, **MORPHOLOGY 10%**, SYMBOLIC 1% | **0.695** |

Residual features are *more* cleanly localized yet *less* nameable — the decisive
evidence that the gap is vocabulary, not model quality.

## 5. Verdict

**CONCLUDED.** The residual is **real, coherent structure concentrated in MODAL
and MORPHOLOGY** — partitions for which no attribute vocabulary existed. Both
hypothesis branches are refuted: it is neither noise nor (crudely-measured)
fingering geometry.

Two consequences:

1. **Build modal + morphological attribute vocabulary**, then re-score. GA already
   owns the modal machinery (`ModalFamily`, `AtonalModalFamiliesConfig`, modal set
   classes) — the attributes are constructible from existing domain code, not
   research-grade unknowns. This is the highest-value next step and would likely
   convert a large share of the 441.
2. **Do not optimize `feature_partition_purity`.** It measures agreement with the
   partition hypothesis. The residual has *higher* purity than the explained set
   while being *less* interpretable — proof that purity and nameability are not
   the same axis. Read it; never target it.

**Confidence: medium-high** — the coherence result has a proper random control and
the partition split is large (2.5× MODAL enrichment). **Caveats:** "explained"
is defined by *our* attribute list, so the 138/441 split is vocabulary-relative,
not absolute; geometry attributes were crude (diagram-derived) and a richer
morphology vocabulary might rescue more than 2; coherence used the compact
similarity vectors, which are partition-weighted, so absolute values are not
cosines in [-1,1] — only the between-group comparison is meaningful.

- **One-way-door check:** none. Read-only analysis over a committed artifact.

## 6. Next

1. **Modal attribute vocabulary** (mode/scale context implied by a voicing) +
   richer morphology (stretch, hand position, finger independence) → re-score the
   441. This is the concrete follow-up.
2. If a large share resolves, the SAE has *earned* a real claim: it discovers modal
   and playability structure the hand-designed partitions assert but never named.
   If it does not resolve, that is the signal to stop investing in the SAE.
