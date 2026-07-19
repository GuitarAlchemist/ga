---
id: 2026-07-19-optick-sae-feature-atlas
date: 2026-07-19
status: active            # open | active | concluded | superseded
domain: embeddings
question: Are the alive features of the OPTIC-K TopK-SAE interpretable as nameable musical concepts?
hypotheses:
  - claim: A substantial fraction of the 820 alive SAE features are monosemantic — each maximally activated by voicings sharing one nameable music-theoretic property (a pitch-class set, an interval-content signature, a chord quality, or a root).
    refuted_if: The top-activating voicings of most sampled features share no GA-computable musical property above chance, OR features are dominated by always-on / polysemantic behaviour.
tools: [pyarrow, ix_pca, ix_silhouette, ga_chord_to_set, ga_icv_neighbors, OptickIndexReader, paper-search, fable-5]
artifacts: state/research/optick-sae-feature-atlas/
validators: []            # to fill at conclusion — Fable 5 + tars cross-check
confidence: medium        # directional (26-feature sample); active pending scale-up + validation
supersedes: null
superseded_by: null
---

# OPTIC-K SAE Feature Atlas — do the sparse features name musical concepts?

**Date:** 2026-07-19
**Type:** research (not a commitment to build) — study 0' under `docs/research/README.md`
**Question:** Are the alive features of the OPTIC-K TopK-SAE interpretable as nameable musical concepts (pitch-class set, ICV, chord quality, root)?

## TL;DR

**Open — active, not concluded.** The SAE is real and reconstructs near-perfectly
(R² 0.9996), with 820/1024 alive features. Two things had to be established
before any interpretability verdict is possible, and both are now done:
(1) **prior art** — the method is settled (reuse TopK-SAE top-activating-example
analysis + Karvonen-2024 ground-truth-partition scoring; guard against Chanin
absorption + Tian low-sensitivity); the *application* to symbolic music-theoretic
concepts appears unpublished. (2) **The feature→voicing mapping was missing and
non-trivial** — the activation parquet dropped the voicing id after a seeded 5%
train/val split, so row *j* ≠ voicing *j*. Recovered exactly (proof below). The
one early red flag: **8 always-on features** (fire on ≥95% of all voicings) —
a known SAE pathology, trivially non-monosemantic.

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
  *quality* across all transpositions. feat 715 is a clean **Forte 4-21 tetrachord**
  detector (ICV 1.00). These are exactly the abstract, nameable music-theoretic
  concepts an SAE is hoped to surface — and they're distinct from the embedding's
  root-carrying dims (the ICV path, per CLAUDE.md).
- **High → instrument/register + always-on.** feat 265 exhibits the **Tian
  sensitivity trap** the prior-art flagged: pc_set purity 1.00 on its top-25 yet
  it fires on **95% of all voicings** → the top-K purity is a recall proxy that
  over-states monosemanticity. Precision/sensitivity (does it fire *only* on the
  attribute) is the missing half and the reason this stays a pass, not a verdict.

## 5. Verdict

**Interim (confidence: medium, directional):** the hypothesis is **provisionally
supported** on a 26-feature sample — most alive features *are* explained by a
single nameable music-theoretic attribute — with the important refinement that
**which** attribute is frequency-band-structured: rare features detect exact
pitch-class sets, mid-frequency features detect transposition-invariant ICV
*shapes* (chord qualities), high-frequency features mostly detect instrument, and
a handful are always-on (non-monosemantic). This is a sharper claim than the
hypothesis anticipated (it predicted "one property"; reality is "one property,
but the property class shifts with feature frequency").

**Not yet `concluded`** — three gaps remain: (a) the sample is 26/820; (b) the
purity metric is recall-only and over-states monosemanticity for always-on
features (Tian) — need a precision/sensitivity metric + Chanin absorption check;
(c) no independent-model validation yet. Per protocol, `concluded` requires
Fable 5 / tars re-checking the interpretation claims.

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
