---
id: 2026-07-19-optick-sae-feature-atlas
date: 2026-07-19
status: concluded         # open | active | concluded | superseded
domain: embeddings
question: Are the alive features of the OPTIC-K TopK-SAE interpretable as nameable musical concepts?
answer: Yes, partially — two disjoint concept classes (92 exact-PC-set detectors ⊂; 59 genuine transposition-invariant ICV/chord-quality detectors), with heavy feature-splitting and ~half the dictionary near-dead. Not a clean one-feature-one-concept atlas.
hypotheses:
  - claim: A substantial fraction of the 820 alive SAE features are monosemantic — each maximally activated by voicings sharing one nameable music-theoretic property (a pitch-class set, an interval-content signature, a chord quality, or a root).
    refuted_if: The top-activating voicings of most sampled features share no GA-computable musical property above chance, OR features are dominated by always-on / polysemantic behaviour.
    outcome: Partially supported — supported for ~2 disjoint classes among the 379 SELECTIVE features (24% pc-set, ~16% genuine ICV), but ~half of alive features are near-dead, so not "a substantial fraction of the 820".
tools: [pyarrow, msgpack, ga_chord_to_set, OptickIndexReader, paper-search, fable-5]
artifacts: state/research/optick-sae-feature-atlas/
validators: [fable-5]     # 3 adversarial passes; final gate = CONCLUDE-OK. tars consistency cross-check = optional follow-up
confidence: high          # every load-bearing number independently re-derived by Fable 5
supersedes: null
superseded_by: null
---

# OPTIC-K SAE Feature Atlas — do the sparse features name musical concepts?

**Date:** 2026-07-19
**Type:** research (not a commitment to build) — study 0' under `docs/research/README.md`
**Question:** Are the alive features of the OPTIC-K TopK-SAE interpretable as nameable musical concepts (pitch-class set, ICV, chord quality, root)?

## TL;DR

**Concluded — yes, partially.** The OPTIC-K TopK-SAE *is* interpretable: it has
learned **two disjoint classes of nameable musical concept** — **92** exact
pitch-class-set detectors and **59** genuine transposition-invariant ICV /
chord-quality detectors (feat 40 a *perfect* P=R=1.0 detector; feat 830 spans all
24 forms) — but with heavy **feature-splitting** (median recall ~0.1–0.23) and
**~half the dictionary near-dead** (400/820 fire strongly on <10 voicings). It is
**not** a clean one-feature-one-concept atlas. Reaching this took five analysis
passes and **three adversarial Fable 5 reviews**, each of which overturned the
prior reading: top-K purity said "49% + band-structured", precision said "24%
pc-set, ICV was an artifact", and measuring ICV *on its own terms* (after Fable 5
caught that a base-rate-lift race structurally hides ICV) restored the ICV class
as real. That reversal chain is the study's main methodological lesson — top-K
inspection misleads in **both** directions. Byproducts: recovered the
silently-dropped feature→voicing mapping, found the parquet was contract-non-
conformant (→ ix fix), and surfaced a `quality_inferred` Forte-label data bug.

---

## 1. Question

OPTIC-K is a 240-dim hand-designed musical embedding. A TopK-SAE was trained on
its compact 124-dim form to find a sparse, hopefully-monosemantic dictionary.
The whole promise of an SAE is *interpretability* — but nobody has checked whether
these features actually correspond to musical concepts a theorist could name.
Until we know that, the SAE is an unaudited black box feeding the QA drift
detector (`qa_score_quality_drift`) and a deferred retrieval reranker. The person
in pain is anyone who wants to *trust* or *steer* the embedding via its features.

## 2. Hypothesis

- **Claim:** A substantial fraction of the 820 alive features are monosemantic —
  each maximally activated by voicings sharing one nameable, GA-computable property.
- **Refuted if:** top-activating voicings of most sampled features share no
  GA-computable property above chance, or features are dominated by always-on /
  polysemantic behaviour.
- **Prior art** (via `/paper-search`, see §Appendix): the method is **settled and
  must be reused, not reinvented** — Gao et al. 2024 (arXiv:2406.04093) *is* our
  TopK-SAE architecture and defines the eval axes; feature characterization is by
  **top-activating examples** (Bricken 2023, Templeton 2024) with **auto-interp**
  (Bills 2023) for scale. Our "partition purity" instinct has a direct analogue in
  **Karvonen et al. 2024 (arXiv:2408.00113, board-game models)** — treat OPTIC-K's
  known partitions (PC-set, ICV, cardinality, ROOT) as the ground-truth concept
  collection and score feature↔partition alignment. Mandatory guardrails:
  **Chanin 2024 (feature absorption, arXiv:2409.14507)** and **Tian 2025 (feature
  sensitivity, arXiv:2509.23717)** — top-activating examples systematically
  over-state monosemanticity. **Novelty:** SAEs on *symbolic* music-theoretic
  voicing embeddings appear unpublished; the closest neighbour (Paek et al. 2025,
  arXiv:2510.23802) works on continuous audio latents + perceptual acoustics.

## 3. Method (reproducible)

**Inputs** (snapshot `2026-06-14`, `input.optick_index_sha` =
`sha256:89354bcb…4bd267`, `OPTIC-K-v1.8`):
- `state/quality/optick-sae/2026-06-14/feature_activations.parquet` — dense
  297,395 × 1024 float activation matrix (training split only).
- `state/quality/optick-sae/2026-06-14/feature_manifest.jsonl` — per-feature
  `{feature_idx, activation_count, is_alive, decoder_norm}` (no labels, no top voicings).
- `state/voicings/optick.index` — OPTK v4 mmap, 313,047 voicings; position →
  `OptickMetadata(Diagram, Instrument, MidiNotes[], QualityInferred)` via
  `Common/GA.Business.ML/Search/OptickIndexReader.cs::GetMetadata(i)`.

**The feature→voicing mapping (the crux).** The parquet is `df.to_parquet(index=False)`
of `model(X_train)` in X_train order, and `X_train` is a **seeded 5% holdout**
(`ix/crates/ix-optick-sae/python/train.py:268-279`): `np.random.seed(42);
idx=arange(313047); np.random.shuffle(idx); train_idx=idx[15652:]`. Therefore
**parquet row j → optick.index position `train_idx[j]`** — *not* position j.
Recovered and verified exactly: `len(train_idx)==297395` (match), row 0 → position
112807. Materialized as `state/research/optick-sae-feature-atlas/row-index-map.parquet`
(+ `.provenance.json`). *A naive positional id column would mislabel every feature —
this is why the durable fix is to emit the id at the producer.*

**Pass 1 — activation structure** (no mapping needed):
```
python  # per-feature activation stats over the parquet, by 64-col batches
        # → state/research/optick-sae-feature-atlas/{pass1-activation-stats.json, per-feature-stats.csv}
```
**Pass 2 — musical characterization** (Level B, the interpretability pass):
for a sample of alive features, take top-K activating parquet rows → optick_row via
the map → `OptickMetadata.MidiNotes` → pitch-classes → set-class / ICV via GA MCP
(`ga_chord_to_set`, `ga_icv_neighbors`). Score feature↔partition alignment
(Karvonen-style); flag absorption (Chanin) and low sensitivity (Tian).
**Verdict gate:** cross-check the interpretation claims with an independent model
(Fable 5) + tars before `concluded`.

## 4. Evidence

**Pass 1 (done)** — `pass1-activation-stats.json`:
- **k-sparsity confirmed:** active-features-per-voicing is 28–32 (median 32);
  279,894/297,395 rows fire exactly 32 (the TopK k). The SAE behaves as specified.
- **Alive/dead:** 820 alive, 204 dead (19.92% — under the contract's 30% retry gate).
- **Activation-frequency long tail (alive features):** p50 ≈ 32 voicings
  (freq 0.0001), p90 0.070, p95 0.176, p99 0.923, p100 1.000. → **568 rare** alive
  features (<0.1% of voicings) and **8 always-on** (≥95%).
- **Red flag:** the 8 always-on features (e.g. idx 776, 1004 fire on 100% of
  voicings) are non-monosemantic by construction — they carry a shared/DC
  component, not a distinctive concept. These must be excluded or explained, not
  counted as "interpretable".

**Contract-conformance finding (byproduct).** The artifact contract
(`docs/contracts/2026-05-02-optick-sae-artifact.contract.md` §`feature_activations_parquet`,
line 151) specifies the parquet as `(voicing_id, feature_id, activation)` rows —
i.e. a `voicing_id` column was **required by contract from the start**. The
producer emitted neither the id nor that long format (it wrote a wide dense
`f0..f1023` matrix, `index=False`). So the parquet was **silently non-conformant
to its own contract**, which is the root cause of the missing mapping. The ix
producer fix (branch `feat/optick-sae-emit-optick-row-id`) adds an `optick_row`
column (stable optick.index position, valid relative to the recorded
`optick_index_sha`), satisfying the *intent* of `voicing_id`. The remaining
reconciliation — the contract's declared name (`voicing_id`) and long-vs-wide
format — is a **v0.1.x draft** question left for cross-repo coordination before
the v1.0 freeze (per CLAUDE.md: do not freeze draft contracts unilaterally).

**Pass 2 / Level B (done — first sample, 26 of 820 alive features)** —
`pass2-feature-characterization.json`. Row→voicing resolved via the id map →
`OptickMetadata.MidiNotes` (parsed from the OPTK v4 msgpack metadata directly in
Python; parser verified against `OptickIndexReader.cs`). For each feature's top-25
activating voicings, scored the "purity" (fraction sharing the modal value) of
five music attributes: exact pitch-class set, **ICV shape** (transposition-
invariant interval-class vector, computed in-Python), cardinality, root (bass PC),
instrument. **The best-explaining attribute is band-structured:**

| Frequency band | n | mean best-purity | dominant attribute |
|---|---|---|---|
| rare (<0.1%) | 12 | **0.997** | exact **pitch-class set** |
| mid (0.1–10%) | 8 | 0.845 | **ICV shape** (transposition-invariant) |
| high (>10%) | 6 | 0.933 | **instrument** |

- **Rare → monosemantic PC-set detectors.** e.g. feat 173 (support 105) / 484
  (54) / 817 (77) fire on voicings sharing an exact pitch-class set (pc_set purity
  ~1.0). Caveat: 6 of the 12 rare features have **support = 1** (fire on a single
  voicing) — trivially pure, effectively over-specific/near-dead; must be
  bucketed separately.
- **Mid → transposition-invariant quality detectors (the interesting finding).**
  feat 176 (freq 2.45%): pc_set purity 0.24 but **ICV purity 1.00** — one chord
  *quality* across all transpositions. (feat 715 looked like a clean tetrachord
  detector here but **Pass 3 refuted it** — it fires on 76% of voicings; always-on.
  Its "Forte 4-21" label is also a data bug — see Pass 3.) These are the abstract,
  nameable concepts an SAE is hoped to surface — distinct from the embedding's
  root-carrying dims (the ICV path, per CLAUDE.md).
- **High → instrument/register + always-on.** feat 265 exhibits the **Tian
  sensitivity trap** the prior-art flagged: pc_set purity 1.00 on its top-25 yet
  it fires on **95% of all voicings** → the top-K purity is a recall proxy that
  over-states monosemanticity. Precision/sensitivity (does it fire *only* on the
  attribute) is the missing half and the reason this stays a pass, not a verdict.

**Pass 3 (done — full population, 820 features, precision + independent
validation)** — `pass3-precision.json`. Fixes Pass 2's recall-only metric per the
prior-art guardrails: musical attributes were precomputed for **all 297,395
voicings** (`attrs.npz`), and per feature the interpretation is scored by
**precision over its strongly-activating set** (activation ≥ 50% of the feature's
max), with a **base-rate lift** (chance-adjusted; instrument base rate is 0.9517,
so "instrument" purity is near-meaningless — correcting Pass 2's "high→instrument").

- **~half the alive features are near-dead:** 400/820 fire strongly on <10
  voicings (memorization-scale units, not detectors); 41 are always-on (freq >20%).
  The effective dictionary is ~379 selective features, not 820.
- **Robust exact-PC-set detectors:** among the 379 selective features, **92 (24%)**
  reach precision ≥0.8 at lift ≥2 — median lift **938×** chance. That is the
  defensible "monosemantic detector" figure: **24% of selective features / 11% of
  alive** — *not* the ~49% an unfiltered top-K purity suggested.
- **Transposition-invariant ICV detectors — confirmed, small but genuine.**
  Independent review verified feat 176 (one Xm13 quality across 5 keys), 758
  (aug-family pentachord, 3 transpositions), and **830 (Forte 3-8 across 5 pc-sets
  AND 3 instruments — textbook)**. These are the novel, most interesting class.
- **Data bug surfaced (independent review):** a voicing labelled `quality_inferred
  = "Forte 4-21"` is actually **Forte 4-9** ({4,5,10,11}, ICV [2,0,0,0,2,2]; 4-21
  is [0,2,4,6]). Defect in OPTIC-K's `quality_inferred` producer, not this study's
  code (ICV independently recomputed). → follow-up ticket candidate.

**Pass 5 (done — ICV measured on its own terms; the blocking fix)** —
`pass5-icv-specific.json`. Pass 4's *best-by-lift* attribution was shown (by
Fable 5) to be **structurally incapable of ever selecting ICV**: the icv/pc_set
base-rate ratio is 35:1, but one ICV class spans ≤24 pc-sets, so pc_set wins the
lift race even for a perfect transposition-invariant detector. So "0/379 ICV
best-lift" was a metric artifact, not a finding. Fix: score **ICV's own**
precision/recall over each selective feature's strong-activation set, plus
`pc_spread` = # distinct pc-sets among the strong voicings sharing the modal ICV
(≥3 ⇒ genuine transposition-invariance, not a single pc-set that trivially has one
ICV).

- **166/379** selective features reach icv precision ≥0.8; **59 span ≥3 pc-sets** —
  a genuine, previously-hidden class of transposition-invariant chord-quality
  detectors. **feat 40: icv P=1.0, R=1.0** (a *perfect* detector, 6 forms);
  **feat 830: P=1.0 across all 24 forms**; feat 686 (ns 3040, P 0.82, R 0.52,
  24 forms) is a robust high-support exemplar.
- **Overlap (Fable 5):** the 92 pc-set detectors are a **subset** of the 166; the
  **59 genuine ICV detectors are disjoint from the 92** ⇒ two truly distinct
  concept classes. Lead the ICV class with **59**, not 166 (the other 107 earn ICV
  precision trivially on 1–2 pc-sets).
- **Top-K misleads both ways:** feat 176 looked ICV-pure on its top-20 but has
  icv P = 0.13 over its full strong set — as much a caution as feat 715 was.

**Independent validation (Fable 5, three adversarial passes).** A different model
re-derived every count from the raw artifacts. Pass A: REFUTED the unfiltered
"49%" (trivial-support inflation) and feat 715 (always-on; caught Pass 2 applying
its Tian guardrail inconsistently). Pass B: REFUTED the "ICV is a top-K artifact"
claim as a base-rate-race artifact — ICV was *untested, not refuted*. Pass C
(final gate): verified Pass 5 exactly and returned **CONCLUDE-OK** for the
corrected two-class conclusion, with caveats folded into §5.

## 5. Verdict

**CONCLUDED — Answer: yes, partially.** The OPTIC-K TopK-SAE *is* interpretable:
it has learned **two disjoint classes of nameable musical concept**, but with
heavy feature-splitting and ~half the dictionary under-utilized. It is **not** a
clean one-feature-one-concept atlas.

- **Utilization:** effective dictionary ≈ **379 selective** features — 400/820
  "alive" features are near-dead (strong support <10), 41 are always-on (freq >20%).
- **Class 1 — exact pitch-class-set detectors:** 92/379 at pc_set precision ≥0.8;
  **40** also reach recall ≥0.25 (median recall 0.23 ⇒ feature-splitting: one
  pc-set concept spread across several features).
- **Class 2 — transposition-invariant ICV / chord-quality detectors (disjoint
  from Class 1):** **59/379** at icv precision ≥0.8 spanning ≥3 pc-sets — genuine
  abstract musical primitives (feat 40 perfect P=R=1.0; feat 830 spans all 24
  forms). This class was hidden by Pass 4's base-rate-lift race and is **real, not
  a top-K artifact** — the single most important correction, forced by independent
  review.

**Confidence: high** — every load-bearing number was independently re-derived by a
different model (Fable 5) across three passes; the one load-bearing inference (a
disjoint genuine ICV-detector class exists) is data-supported (CONCLUDE-OK).

**Caveats (disclosed, per Fable 5):** (1) Class 2's headline is the **59** genuine
detectors, not the 166 (the other 107 earn ICV precision trivially on 1–2 pc-sets);
(2) ICV splitting is *heavier* than pc-set — median icv recall 0.097, only 14/59
reach R≥0.25; (3) the **50%-of-max** strong-activation threshold is a single
unswept analysis choice; (4) 2/59 features have support <30 (wide error bars);
`icv_lift` uses the fixed mean ICV base rate, so **precision**, not lift, is the
effective gate.

**Follow-ups (do not block conclusion):** threshold sweep + Chanin absorption +
tars consistency cross-check would harden Class 2; the `quality_inferred =
"Forte 4-21"` → actually Forte 4-9 data bug is a spun-off ticket against the
OPTIC-K producer.

- **One-way-door check:** none triggered. This study *reads* OPTIC-K; it must not
  change dimensions or the schema. Any recommendation to do so is a separate,
  signed-off plan.

## 6. Next

1. **Pass 2 / Level B** — resolve top-activating voicings per feature to musical
   descriptors; compute Karvonen-style feature↔partition alignment; apply Chanin +
   Tian guardrails; sample-label with auto-interp.
2. **Durable producer fix (ix)** — emit `optick_row` in `feature_activations.parquet`
   so the mapping is explicit, not re-derived. (Cross-repo; re-emits via
   `links.supersedes`.)
3. **Conclude** only after an independent model re-checks the interpretation claims.

## Appendix — prior-art citations

Gao et al. 2024 (arXiv:2406.04093, TopK SAE + eval); Bricken et al. 2023 (Towards
Monosemanticity); Templeton et al. 2024 (Scaling Monosemanticity); Cunningham et
al. 2023 (arXiv:2309.08600); Bills et al. 2023 (auto-interp); Karvonen et al. 2024
(arXiv:2408.00113, ground-truth-partition scoring — the "partition purity"
analogue); Chanin et al. 2024 (arXiv:2409.14507, absorption); Tian et al. 2025
(arXiv:2509.23717, sensitivity); Paek et al. 2025 (arXiv:2510.23802, closest
neighbour — audio latents).
