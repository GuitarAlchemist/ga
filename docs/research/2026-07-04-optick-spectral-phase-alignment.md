# Transposition-aligned similarity via phase correlation on OPTIC-K's SPECTRAL partition

**Status: research note, v0.1 — theory verified numerically; integration is query-time only (no re-index, no one-way door). Retrieval wiring stays deferred until the corpus rebuild, same caveat as the ICV path (docs/solutions/architecture/2026-06-19-…-t-invariance.md).**

---

## 1. Problem

OPTIC-K's documented answer to transposition-agnostic "same-shape" retrieval is the **ICV path** (`IcvNeighborsSkill` / Grothendieck). The interval-class vector is transposition-invariant — but it is provably blind in two musically serious ways:

1. **Chirality.** The ICV is inversion-invariant (TnI), so it **cannot distinguish a major triad from a minor triad** — `{0,4,7}` and `{0,3,7}` share ICV `<001110>`. "Same shape" collapses the single most salient qualitative distinction in tonal music.
2. **Homometry.** The 23 Z-related pairs of 12-TET (46 set classes, exhaustively confirmed by the 2026-06-19 invariant sweep) share ICVs while being non-equivalent — any ICV-based similarity scores them 1.0.

Meanwhile the embedding **already stores the information that resolves both**: the SPECTRAL partition (dims 96–107, v1.3) carries the six Fourier magnitudes `|X_k|/√N` *and* the six phases `(arg X_k + π)/2π` of the pitch-class chroma — but is marked *"EXCLUDED from similarity scoring (informational only)"*. This note shows the phases turn SPECTRAL into a **transposition-aligned similarity** that fixes both blind spots, in closed form, at query time.

## 2. Setup

For a pitch-class set `A ⊆ Z₁₂` with chroma `x[n] = 1_{n∈A}`, the DFT is `X_k = Σₙ x[n] e^(−2πikn/12)`. OPTIC-K stores, for `k = 1…6`: magnitude `m_k = |X_k|/√N` (dims 96–101) and phase `φ_k = arg X_k` (dims 102–107, quantized to `[0,1]` via `(φ+π)/2π`; float32 quantization error ≲ 4·10⁻⁷ rad — negligible below). Conjugate symmetry (`X_k = X̄_{12−k}`) makes `k ≤ 6` complete for real chroma.

## 3. Results

**Theorem 1 (equivariance — DFT shift theorem on Z₁₂).** Transposition `T_t: n ↦ n+t` maps `X_k ↦ X_k·e^(−2πikt/12)`. Hence magnitudes are Tₙ-invariant and phases transform **linearly and predictably**: `φ_k ↦ φ_k − 2πkt/12`. *Proof:* substitute `n' = n−t` in the DFT sum. ∎

**Theorem 2 (Lewin's lemma ⇒ homometry blindness).** `ICV(A)` and `(|X_k|²)_k` determine each other (the ICV is the autocorrelation of the chroma; its DFT is `|X|²` — Wiener–Khinchin on Z₁₂). Hence **any** similarity computed from ICV or magnitudes alone is constant on homometry classes: the 23 Z-pairs are indistinguishable *by construction*, not by accident. Likewise inversion `I: n ↦ −n` conjugates the DFT (`X_k ↦ X̄_k`), preserving magnitudes — chirality blindness is the same phenomenon. ∎

**Theorem 3 (alignment operator).** Define, for stored features of two sets:

```
S(A,B) = max_{t∈Z₁₂}  Σ_{k=1..6} w_k · m_k(A) m_k(B) · cos( φ_k(A) − φ_k(B) + 2πkt/12 )
         ───────────────────────────────────────────────────────────────────────────
                              Σ_k w_k · m_k(A) m_k(B)
```

with any non-negative weights `w_k`. Then: (i) `S` is Tₙ-invariant in both arguments; (ii) `S(A, T_t A) = 1` exactly whenever the denominator is non-zero, and the argmax `t*` **recovers the aligning transposition** (phase correlation, as in image registration); (iii) cost is 72 multiply-adds from stored dims — no re-embedding, no 12-fold query expansion. *Proof:* (i)–(ii) from Theorem 1: the phase deltas of a true transposition satisfy `Δφ_k = 2πkt/12` simultaneously for all k, making every cosine 1 at `t* = −t mod 12`. ∎

**Zero-denominator convention (the operator is NOT total — Codex review, ga#513).** `Σ_k w_k m_k(A)m_k(B) = 0` occurs in two real cases and each needs an explicit branch (`denom < ε`):

- **Both spectra null on k=1…6** — exactly the empty set and the chromatic aggregate. These are transpositionally trivial (fixed by every `T_t`), so define `S := 1` with `t* = Z₁₂` (all transpositions align).
- **Disjoint non-trivial support** — e.g. the augmented triad `{0,4,8}` (support `{3,6}`) vs the diminished-7th `{0,3,6,9}` (support `{4}`): the sets share **no** periodicity content, so define `S := 0` with no `t*` (maximally dissimilar in shape space; no alignment is meaningful). Disjoint support requires both sets to be transpositionally symmetric (support confined to divisors' multiples), so the cases are few and exhaustively enumerable by the Tier-2 sweep — the acceptance criteria in §7 must include this enumeration.

**Theorem 4 (chirality and TnI on demand).** Inversion conjugates the DFT, i.e. negates phases. Hence `S_TnI(A,B) = max( S(A,B), S(A, B̄) )` with `B̄` = phase-negated features gives set-class (TnI) matching **when wanted**, while plain `S` preserves chirality. The Tₙ/TnI distinction becomes a query flag instead of a hard-coded loss of information. ∎

**Separation corollary.** Z-related pairs are by definition not TnI-equivalent, so their phase profiles are non-congruent under both alignment and conjugated alignment: `S` and `S_TnI` separate them, while ICV cannot (Theorem 2).

## 4. Numerical verification (stdlib DFT, this session)

| Pair | ICV/magnitude similarity | S | t* | S conjugated |
|---|---|---|---|---|
| C maj `{0,4,7}` vs D maj `{2,6,9}` | 1.0 | **1.0000** | 10 (recovers T₂ alignment) | — |
| C maj vs C min `{0,3,7}` | 1.0 (blind) | **0.5714** | 9 | **1.0000** at t*=7 (TnI-equivalent, correctly) |
| 4-Z15 `{0,1,4,6}` vs 4-Z29 `{0,1,3,7}` | 1.0 (blind) | **0.3333** | 3 | 0.6667 |
| 6-Z17 `{0,1,2,4,7,8}` vs 6-Z43 `{0,1,2,5,6,8}` | 1.0 (blind) | **0.4000** | — | 0.4000 |
| 1000 random `(set, transposition)` pairs | — | **1.0000 for 1000/1000** | — | — |

Every claim of §3 holds numerically; magnitudes were confirmed equal (`= ICV information`) in each blind-spot case.

## 5. Musical reading

- `t*` is not a by-product — it is the **answer to a guitarist's actual question**: "find voicings with this shape *and tell me how to transpose them into my key*". The ICV path returns neighbors; `S` returns neighbors **with the aligning transposition attached**.
- Chirality preserved: major ≠ minor under plain `S` (0.57), equivalent under the explicit TnI flag — the user chooses, the math no longer chooses for them.
- Per-coefficient weights `w_k` inherit Quinn's quality semantics already documented in the schema (k=5 diatonicity, k=3 diminished, k=4 augmented, k=6 tritone…): weighting `w₅` up biases alignment toward tonal-quality matching. Tunable without any storage change.

## 6. Integration path (no one-way door)

Everything is **query-time arithmetic over dims 96–107 as stored**:

1. Un-quantize phases (`φ = 2π·p − π`), evaluate `S` (and optionally `S_TnI`) against candidates — e.g. as a re-ranking pass over the existing top-K, or as a `SpectralShapeNeighborsSkill` beside `IcvNeighborsSkill`.
2. **No schema change, no re-index, no dimension change.** The "EXCLUDED from similarity scoring" note stays true for the default cosine; `S` is a separate, explicitly-invoked path.
3. Deferral caveat inherited verbatim from the 2026-06-19 decision: do not wire into live retrieval until the corpus rebuild (the same reason ICV wiring was deferred).

## 7. Acceptance criteria (before any product wiring)

- Extend the Tier-2 sweep (`Tools/GaStructureInvariance`) with `S`: **prove exhaustively** (224 set classes × 12 transpositions) that `S = 1` on transposition orbits and that `t*` recovers the transposition — the sweep infrastructure exists precisely for this.
- Verify separation on **all 23 Z-pairs** (not just the two spot-checked here) and on the major/minor pair, from the real `TheoryVectorService` output (not the idealized 0/1 chroma — voicing chromas may be weighted; Theorem 1/3 hold for any non-negative chroma weighting, but the numbers will differ).
- A/B against the ICV path on the voicing corpus: top-K overlap, plus the two blind-spot metrics (chirality confusions per 100 queries; Z-pair confusions).

## 8. Limits, honestly

- `S` is Tₙ-invariant, **not** octave/voicing-blind beyond what the chroma already collapses — it inherits SPECTRAL's set-level granularity.
- For weighted (non-0/1) chromas, Theorem 2's exact ICV correspondence becomes approximate; Theorems 1, 3, 4 are unaffected.
- Sets fixed by some transposition (e.g. augmented triad, diminished 7th, whole-tone) have `S = 1` at multiple `t*` — return the full argmax set, not one value. The same symmetry is what makes disjoint-support pairs possible (§3, zero-denominator convention) — the two edge cases are the same phenomenon seen from two sides.
- This is applied synthesis: the shift theorem, Lewin's lemma and phase correlation are classical (Lewin 1959; Quinn 2006–07; Amiot, *Music Through Fourier Space*, 2016). The contribution is the closed-form alignment operator over OPTIC-K's *stored* features, the chirality/Z-pair separation guarantees in retrieval terms, and the exhaustive-verification harness this repo uniquely has.
