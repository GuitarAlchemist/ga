# Musical Embedding Schema (v1.3.1)

**Version**: 1.3.1 "OPTIC-K-v1.3.1"
**Date**: 2026-01-11
**Total Dimension**: 109
**Constraint**: Append-only over v1.3. Indices 0–107 are unchanged.

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
| **SPECTRAL** | 13 | 96–108 | **0.00** | DFT-based geometry. Informational only for similarity. |

**Total: 109 dims**

### 0.2 Scoring Presets Policy

Since SPECTRAL is weight 0.0 by default, use these policies:

**1. Tonal/Search Preset (Default)**
- Use weights as defined above. Spectral features are informational only (for sorting/filtering).

**2. Atonal/Navigation Preset**
- Use when exploring harmonic spaces (set-class navigation, voice-leading).
- **STRUCTURE**: 0.35, **SPECTRAL**: 0.15, **MORPHOLOGY**: 0.20, **CONTEXT**: 0.20, **SYMBOLIC**: 0.10.
- Or custom weights based on user intent.

---

## 1. Fundamental Definitions (Updated)

### 1.4 DFT & Chroma Types

**Structural Chroma (Binary)**
- `1.0` if PC present, `0.0` otherwise.
- Used for: Scale, Pitch-Class Set, Interval Set.

**Salient Chroma (Weighted)**
- `Weight = Count + Significance` (e.g., Bass/Melody weights).
- Used for: **Voicing**, **Structure** (with defined pitches).
- Captures doublings and registral emphasis in the spectrum.

**Gating Rule**:
- If object Identity is **Voicing** or **Shape**, compute DFT on **Salient Chroma**.
- If object Identity is **Chord**, **Scale**, **PCS**, compute DFT on **Structural Chroma**.

---

## 3. SPECTRAL GEOMETRY (96–108) — Updated v1.3.1

### 3.1 Fourier Magnitudes (96–101)
*Same indices as v1.3.*
**Rule**: Magnitudes depend on Chroma type (Salient vs Structural).
**Normalization**: `|DFT[k]| / sqrt(Energy)` where `Energy = Σ(chroma_n²)`.

### 3.2 Fourier Phases (102–107)
*Same indices as v1.3.*
**Phase Distance Contract (Crucial)**:
When comparing phases, use **Wrap-Around Distance**:
```
Dist(φ1, φ2) = min(|φ1 - φ2|, 1.0 - |φ1 - φ2|)
```
Do NOT use simple Euclidean distance on phases.

### 3.3 Spectral Entropy (108) — NEW
Measures the "organization" or "peakiness" of the harmonic spectrum.

**Formula**:
1. Power Spectrum: `P[k] = |DFT[k]|²` for `k=0..6` (include DC/k=0).
2. Probability Mass: `p_k = P[k] / ΣP`.
3. Entropy: `H = -Σ (p_k * log2(p_k))`.
4. Max Entropy is `log2(7) ≈ 2.807`.
5. **Output**: `1.0 - (H / MaxEntropy)` (Clamped [0,1]). 
   - **High (1.0)** = Organized/Pure (e.g. single interval cycle).
   - **Low (0.0)** = Chaotic/Noisy (e.g. random chromatic cluster).

| Index | Name | Formula |
|---|---|---|
| 108 | Spectral Entropy | `1.0 - (Entropy / log2(7))` |

---

## 4. Implementation Contract

<!-- 
Implemented by:
- GA.Business.Core.AI.Musical.Schema.EmbeddingSchema
- GA.Business.Core.AI.Musical.Generation.MusicalEmbeddingGenerator
- GA.Business.Core.AI.Musical.Partitions.IdentityVectorService
- GA.Business.Core.AI.Musical.Partitions.TheoryVectorService
- GA.Business.Core.AI.Musical.Partitions.MorphologyVectorService
- GA.Business.Core.AI.Musical.Partitions.ContextVectorService
- GA.Business.Core.AI.Musical.Partitions.SymbolicVectorService
- GA.Business.Core.Fretboard.Voicings.Search.GpuVoicingSearchStrategy (Similarity)
- GA.Business.Core.Fretboard.Voicings.Search.CpuVoicingSearchStrategy (Similarity)
-->

Same as v1.3, plus:
- **Salient Chroma**: For voicings, construct chroma by summing weights of midi notes.
    - `Weight(note) = 1.0` (base)
    - Optional: `+0.5` for Bass, `+0.5` for Melody (Top).
- **Entropy**: Derived from full spectrum (k=0..6).
