# Integrating phase-based similarity into OPTIC-K retrieval — deep-research synthesis

**Status: research note, 2026-07-22. Deep-research harness (5-angle fan-out → 15 sources → 3-vote adversarial verification → synthesis; 104 agents, 0 errors on the completed run). Extends the 2026-07-04 phase-alignment note and operationalizes its deferred §6 retrieval wiring. Companion to the shipped operator (`SpectralPhaseAlignment`, ga#579) and its IX reference implementation (GuitarAlchemist/ix#252).**

---

## Question

`SpectralPhaseAlignment` (ga#579) gives a closed-form, transposition/inversion-aware similarity that separates the homometric (Z-related) pairs and major/minor chirality the interval-class vector cannot. How should it become an actual retrieval feature over the ~313k-voicing OPTIC-K corpus? Re-rank vs index; closed-form vs learned; how to evaluate.

## Verdict

The mathematics is **settled and fully closed-form — not a research gamble.** Cosine-over-magnitude is *provably* blind to exactly the confusions that matter; the fix is a **two-stage retriever** (magnitude-cosine top-K for recall → closed-form phase re-rank for disambiguation). A specialised phase-aware ANN index is not worth building. One higher-leverage finding upgrades the plan: **coefficient products** turn chirality/homometry discrimination into *indexable* scalar features, not just a re-rank pass.

## Verified findings (all merged 3-0 unless noted)

### 1. Magnitude-only cosine is provably blind — and it is the crystallographic phase-retrieval problem
DFT magnitudes are transposition- **and** inversion-invariant (they encode only the set class), but homometric sets are *defined* by sharing equal magnitude on all Fourier coefficients — equivalently the same Patterson autocorrelation = interval vector = squared magnitude spectrum (Wiener–Khinchin on ℤ/12). "The musical concept of Z-related sets coincides with the crystallographic notion of homometric sets." Because the DFT is lossless, the residual discriminating information sits **entirely in the phases**.
*Sources:* Amiot & Yust, MCM 2022 (Springer, 10.1007/978-3-031-07015-0_23); Jedrzejewski & Johnson, *Z-relation and homometry in musical distributions*; Amiot, arXiv:1304.6608.

### 2. Transposition is a pure rotation on phase — closed-form, trivial group
Transposition by `x` multiplies each Fourier component by a unit vector, shifting the k-th phase by exactly `k·x mod 12`; the n-th phase equals the chord's pitch-class sum (Yust / Tymoczko). The transposition group over ℤ/12 is a trivial 12-element search, so the offset is recovered algebraically. **Candid caveat from the sources:** over a 12-element group "closed-form saves little vs a 12-way search — the real payoff is chirality disambiguation, not transposition speed."
*Sources:* Yust, *Fourier Phase Space* (Princeton PDF); Yust, *Fourier Phase and Pitch-Class Sum* (MCM 2019); Yust, *Schubert's Harmonic Language and Fourier Phase Space*, JMT 59(1), 2015.

### 3. Coefficient products = indexable, chirality-aware invariant signatures (the upgrade)
Transposition-invariant **scalar** signatures come in closed form as *products of Fourier coefficients whose indices partition 12* — e.g. `a₂·a₃·a₇` for the diatonic (2+3+7=12); Amiot & Yust, Proposition 3. Crucially, **inversion conjugates every coefficient, which negates the imaginary part of a coefficient product while preserving the real part** — a direct closed-form handle on major/minor chirality. Unlike ga#579's query-time max-over-group operator, these products are **precomputable per voicing and directly indexable** (real part = T/I-invariant magnitude-like coordinate; imaginary part = signed chirality coordinate). Limit: inversionally-symmetric sets yield real products (the non-chiral case, correctly out of scope).
*Source:* Amiot & Yust, MCM 2022 (Springer). Independently derivable (A ↦ −A conjugates every `X_k`).

### 4. Architecture — re-rank, don't build a bespoke phase-aware index
Recommended production shape for research-question (a): **magnitude-cosine top-K → closed-form phase re-rank / coefficient-product disambiguation.** Rationale: (i) the transposition group is a trivial 12-way search, so a specialised invariant index buys almost nothing; (ii) a low-order phase pair (k=3, 5 — Yust's Tonnetz-embedding coordinates) already discriminates; (iii) learned equivariant embeddings measurably discard task-relevant information relative to the exact closed-form features. A learned D₁₂-equivariant embedding (Music102-style, parameter-efficient — arXiv:2410.18151) remains a viable *longer-term* alternative, deferred.

## ⚠ Open discrepancy to reconcile before wiring

The web sources cite **"19 homometric pairs at N=12"** (first homometric *triple* at N=16). But ga#579's test **verified 23** by enumerating GA's own `SetClass.Items` (grouped by ICV, degenerate classes excluded) — matching the 2026-07-04 note's canon and Forte. This is a **counting-convention conflict** (subset-pairs vs TnI set-class pairs, or a source miscount), **not** a reason to distrust the code-verified 23. The evaluation harness (below) must pin the exact inventory first; the code-verified 23 is the stronger evidence.

## Evaluation methodology (research-question (d))

Do **not** score with top-K overlap alone. Build a labelled probe set from the ground-truth inventory and measure the two blind-spot metrics directly:
- **Homometric (Z-pair) confusion rate** — fraction of Z-pair members retrieved as mutually "identical". Requires the reconciled pair inventory (23 per ga#579, pending the 19-vs-23 reconciliation).
- **Chirality confusion rate** — major/minor (and general non-inversionally-symmetric) pairs scored as identical.
- A/B vs the ICV path on the voicing corpus: top-K overlap **plus** the two confusion metrics (per the 2026-07-04 note §7 acceptance criteria — now literature-backed).

## Integration path (unchanged one-way-door posture)

Query-time arithmetic + optional precomputed coefficient-product scalars over the SPECTRAL partition as stored. No schema change, no re-index for the re-rank path; the coefficient-product features, if indexed, are a **new dimension addition** deferred to the next corpus rebuild (same posture as the ICV / phase wiring). The re-rank path ships now; the indexed-feature path is the rebuild-gated upgrade.

## Sources

Amiot & Yust, "Fourier Phase and Pitch-Class Sum" / MCM 2022 (Springer 10.1007/978-3-031-07015-0_23); Yust, *Schubert's Harmonic Language and Fourier Phase Space*, JMT 59(1), 2015; Yust, *Fourier Phase Space* (Princeton); Jedrzejewski & Johnson, *Z-relation and homometry in musical distributions*; Amiot, arXiv:1304.6608; Music102 (D₁₂-equivariant transformer), arXiv:2410.18151. (A latency data point on closed-form vs learned re-rankers surfaced in an earlier partial run but rested on an unresolved arXiv id and is deliberately omitted here pending a citation check.)
