# Musical Embedding Schema (v1.3)

> [!IMPORTANT]
> This version (v1.3) has been superseded by **v1.3.1**. 
> v1.3.1 adds Spectral Entropy (index 108) and Salient Chroma weighting.
> See `OPTIC-K_Embedding_Schema_v1.3.1.md`.

**Version**: 1.3 "OPTIC-K-v1.3"
**Date**: 2026-01-11
**Total Dimension**: 108

<!-- 
Implemented by (Legacy):
- GA.Business.Core.Fretboard.Voicings.Search.GpuVoicingSearchStrategy (at 108-dim)
- GA.Business.Core.Fretboard.Voicings.Search.CpuVoicingSearchStrategy (at 108-dim)
-->
**Constraint**: Append-only over v1.2.1. Indices 0–95 are unchanged.

Guitar Alchemist – Canonical Vector Space (OPTIC/K-aligned + SPECTRAL).

---

## 0. Summary

### 0.1 Macro-Partitions & Weights

| Section | Dims | Index | Weight | Description |
|---|---|---|---|---|
| **IDENTITY** | 6 | 0–5 | **N/A** | Hard Filter (Chord, Scale, Voicing). NOT used in similarity. |
| **STRUCTURE** | 24 | 6–29 | **0.45** | OPTIC/K Core: pitch-class set invariants. |
| **MORPHOLOGY** | 24 | 30–53 | **0.25** | Physical realization (fretboard geometry). |
| **CONTEXT** | 12 | 54–65 | **0.20** | Temporal motion + harmonic function. |
| **SYMBOLIC** | 12 | 66–77 | **0.10** | Technique + lineage/style tags. |
| **EXTENSIONS** | 18 | 78–95 | **0.00** | Derived features. Informational only for similarity. |
| **SPECTRAL** | 12 | 96–107 | **0.00** | DFT-based geometry. Informational only for similarity. |

**Total: 108 dims**

### 0.2 Backward Compatibility
- v1.2.1 vectors (96d) → zero-pad indices 96–107.
- Indices 0–95 are frozen.
- New dims default to `0.0` if data unavailable.

### 0.3 Similarity Formula (REQUIRED)

**Algorithm**: Weighted Partition Cosine

```
Similarity(A, B) = Σ (weight[p] × cosine(normalize(A[p]), normalize(B[p])))
```

Where:
- `p` ∈ {STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC}
- `normalize(v)` = v / ||v|| (L2 norm). If ||v|| = 0, return zero vector.
- IDENTITY, EXTENSIONS, and SPECTRAL are excluded from direct weighting.

---

## 1. Fundamental Definitions

### 1.1 Pitch-Class Set (PCS)
A set of integers in {0..11} representing pitch classes (C=0, C#=1, ..., B=11).

### 1.2 Complement (Comp)
For a PCS S: `Comp(S) = {0..11} \ S`

### 1.3 Interval Class Vector (ICV)
A 6-dimensional vector counting interval classes in the PCS. (See v1.2.1 schema for details).

### 1.4 Discrete Fourier Transform (DFT)
Per Lewin's Lemma: `ICV = |DFT(chroma)|²`.
DFT provides **spectral geometry** beyond simple interval counts.

**Formula**:
```
DFT[k] = Σ chroma[n] × exp(-2πi × k × n / 12)
```
Where `chroma` is the 12-element activation vector of pitch classes.

---

## 2. PARTITIONS 0–6 (Indices 0–95)

See `OPTIC-K_Embedding_Schema_v1.2.1.md` for definitions of:
- IDENTITY (0–5)
- STRUCTURE (6–29)
- MORPHOLOGY (30–53)
- CONTEXT (54–65)
- SYMBOLIC (66–77)
- EXTENSIONS (78–95)

---

## 3. SPECTRAL GEOMETRY (96–107) — NEW in v1.3

DFT-based features for harmonic navigation.
Divided into **Magnitudes** (periodicities) and **Phases** (position on circle).

**Universal Rule**: ALL formulas are wrapped in `clamp01()`.

### 3.1 Fourier Magnitudes (96–101)
Measures "how much" of each periodicity exists in the set.
**Normalization**: `|DFT[k]| / sqrt(N)` (where N is cardinality).

| Index | k | Name | Musical Meaning |
|---|---|---|---|
| 96 | 1 | Chromatic Clumping | Pitches clustered together (chromatic scale) |
| 97 | 2 | Whole-Tone Structure | Whole-tone scale affinity (ic2, ic6) |
| 98 | 3 | Diminished Structure | Minor-third cycle affinity (diminished chords) |
| 99 | 4 | Augmented Structure | Major-third cycle affinity (augmented chords) |
| 100 | 5 | Diatonic Structure | **Fifths cycle affinity (Key-ness)** |
| 101 | 6 | Tritone Structure | Tritone symmetry (Dom7, Lydian) |

### 3.2 Fourier Phases (102–107)
Measures "where" the set sits on each cycle.
**Normalization**: `(arg(DFT[k]) + π) / (2π)` → maps [-π, π] to [0, 1].

| Index | k | Name | Musical Meaning |
|---|---|---|---|
| 102 | 1 | Phase k=1 | Position of chromatic cluster |
| 103 | 2 | Phase k=2 | Position on whole-tone cycle |
| 104 | 3 | Phase k=3 | Position on diminished cycle |
| 105 | 4 | Phase k=4 | Position on augmented cycle |
| 106 | 5 | Phase k=5 | **Position on circle of fifths** (Tonal center) |
| 107 | 6 | Phase k=6 | Position on tritone cycle |

### 3.3 Musical Use Cases
- **Diatonicness**: High `Mag[k=5]` indicates strong tonality.
- **Voice-Leading**: `|PhaseA[k] - PhaseB[k]|` measures spectral distance.
- **Modulation**: Moving along `Phase[k=5]` corresponds to modulating through circle of fifths.

---

## 4. Implementation Contract

Same as v1.2.1, with additions:

### 4.1 DFT Computation
- Compute `chroma` vector (1.0 for present PCs, 0.0 otherwise).
- Compute complex DFT for `k=1..6`.
- Store `Magnitude / sqrt(N)` and `NormalizedPhase`.
- If `N=0`, all values are 0.0.

### 4.2 Clamp Rule
- Magnitudes naturally fall around `[0, 1]` due to normalization, but must be clamped.
- Phases are strictly mapped to `[0, 1]`.

---

## 5. Reference

See `GA.Business.Core.AI.Musical.Schema.EmbeddingSchema` for constants.
