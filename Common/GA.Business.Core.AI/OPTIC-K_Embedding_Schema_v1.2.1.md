# Musical Embedding Schema (v1.2.1)

**Version**: 1.2.1 "OPTIC-K-v1.2.1"
**Date**: 2026-01-11
**Total Dimension**: 96
**Constraint**: Append-only over v1.1. Indices 0–77 are unchanged.

Guitar Alchemist – Canonical Vector Space (OPTIC/K-aligned).

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
| **EXTENSIONS** | 18 | 78–95 | **0.00** | Derived features. Contribute via parent partitions (see §0.3). |

**Total: 96 dims**

### 0.2 Backward Compatibility
- v1.1 vectors (78d) → zero-pad indices 78–95.
- Indices 0–77 are frozen.
- New dims default to `0.0` if data unavailable.

### 0.3 Similarity Formula (REQUIRED)

**Algorithm**: Weighted Partition Cosine

```
Similarity(A, B) = Σ (weight[p] × cosine(normalize(A[p]), normalize(B[p])))
```

Where:
- `p` ∈ {STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC}
- `normalize(v)` = v / ||v|| (L2 norm). If ||v|| = 0, return zero vector.
- IDENTITY is excluded (used for hard filtering only).
- EXTENSIONS are excluded from direct weighting (they're derived from parent partitions).

**Extension Handling**: Extensions (78–95) are **informational only** for similarity. They are computed for interpretability and filtering but do not contribute additional weight. If you need extensions in similarity, add them as a 5th weighted partition with explicit weight.

---

## 1. Fundamental Definitions

### 1.1 Pitch-Class Set (PCS)
A set of integers in {0..11} representing pitch classes (C=0, C#=1, ..., B=11).

### 1.2 Complement (Comp)
For a PCS S: `Comp(S) = {0..11} \ S`
Example: S = {0, 4, 7} (C major triad) → Comp(S) = {1, 2, 3, 5, 6, 8, 9, 10, 11}

### 1.3 Interval Class Vector (ICV)
A 6-dimensional vector counting interval classes in the PCS.



| IC | Interval | Example |
|---|---|---|
| 1 | m2 / M7 | C-C# |
| 2 | M2 / m7 | C-D |
| 3 | m3 / M6 | C-Eb |
| 4 | M3 / m6 | C-E |
| 5 | P4 / P5 | C-F |
| 6 | Tritone | C-F# |

**Computation**:
```
for each unordered pair (p1, p2) in PCS:
    d = min(|p1 - p2|, 12 - |p1 - p2|)
    ICV[d - 1] += 1  // d ∈ {1..6}
```

**Normalization**: Raw counts, NOT normalized. Cosine handles magnitude.

### 1.4 Root Pitch Class (rootPC)
The pitch class of the chord root, if defined.

**Gating Rule**:
- If object type is atonal PCS, symmetric set, or quartal stack with no defined root: `rootPC = UNDEFINED`.
- If `rootPC = UNDEFINED`: all root-dependent features (81, 82, 91, 92, 95) MUST be `0.0`.

---

## 2. IDENTITY Subspace (0–5) — Hard Filter

One-hot encoding. NOT used in similarity scoring.

| Index | Object Type |
|---|---|
| 0 | Chord |
| 1 | Scale |
| 2 | Voicing |
| 3 | Shape |
| 4 | Interval Set |
| 5 | Pitch-Class Set |

**Gating Rule**: EXTENSIONS (78–95) are ONLY populated when:
- `IDENTITY[2] = 1` (Voicing), OR
- `IDENTITY[3] = 1` (Shape with realized pitches)

For all other identity types, indices 78–95 MUST be `0.0`.

---

## 3. STRUCTURE Subspace (6–29) — OPTIC/K Core

### 3.1 Index Mapping
| Index | Meaning | OPTIC/K Axis |
|---|---|---|
| 6–17 | Pitch-Class Chroma (12d) | O (Octave), P (Permutation) |
| 18 | Cardinality | C |
| 19–24 | Interval Class Vector (6d) | T (Transposition), I (Inversion) |
| 25 | Complementarity (K) | K |
| 26–29 | Tonal Properties | Tonic-ness, Dominant-pull, Tension, Stability |

### 3.2 Complementarity (K) — Index 25
**Formula**:
```
K = clamp01(cosine(ICV(PCS), ICV(Comp(PCS))))
```

Where:
- `ICV()` computed per §1.3
- `Comp()` computed per §1.2
- `cosine(a, b) = dot(a, b) / (||a|| × ||b||)`
- If either ICV is zero-vector, K = 0.0

---

## 4. EXTENSIONS Subspace (78–95)

**Universal Rule**: ALL formulas are wrapped in `clamp01()`.

### 4.1 Context Dynamics (78–79)

| Index | Name | Formula |
|---|---|---|
| 78 | Harmonic Inertia | `clamp01(stability × (1 - tension))` |
| 79 | Resolution Pressure | `clamp01(0.7 × tension + 0.3 × (1 - stability))` |

### 4.2 Textural Features (80–82)

| Index | Name | Formula | Gate |
|---|---|---|---|
| 80 | Doubling Ratio | `clamp01((N - uniquePCs) / max(1, N))` | - |
| 81 | Root Doubled | `1.0 if count(rootPC in pcs) > 1 else 0.0` | rootPC defined |
| 82 | Top Note Relative | `clamp01(((topPC - rootPC + 12) % 12) / 11.0)` | rootPC defined |

### 4.3 Relational (83)

| Index | Name | Formula |
|---|---|---|
| 83 | Smoothness Budget | `clamp01(0.5×[80] + 0.7×(1-[85]) - 0.3×[88])` |

### 4.4 Spectral Color (84–89)
**Constants**: `minMidi=40, maxMidi=88, lowThresh=52, highThresh=76`

| Index | Name | Formula |
|---|---|---|
| 84 | Mean Register | `clamp01((mean(p) - minMidi) / (maxMidi - minMidi))` |
| 85 | Register Spread | `clamp01(stddev(p) / 12.0)` |
| 86 | Low End Weight | `clamp01(count(p < lowThresh) / max(1, N))` |
| 87 | High End Weight | `clamp01(count(p > highThresh) / max(1, N))` |
| 88 | Local Clustering | `clamp01(count(diff(sort(p)) ≤ 2) / max(1, N-1))` |
| 89 | Roughness Proxy | `clamp01(Σ(lowWeights for close intervals) / max(1, N-1))` |

### 4.5 Extended Texture (90–95)

| Index | Name | Formula | Gate |
|---|---|---|---|
| 90 | Bass-Melody Span | `clamp01((max(p) - min(p)) / 48.0)` | - |
| 91 | Third Doubled | `1.0 if count(3rd PC) > 1 else 0.0` | rootPC defined |
| 92 | Fifth Doubled | `1.0 if count(5th PC) > 1 else 0.0` | rootPC defined |
| 93 | Open Position | `1.0 if span > 12 else 0.0` | - |
| 94 | Inner Voice Density | `clamp01(count(lowThresh < p < highThresh) / max(1, N))` | - |
| 95 | Omitted Root | `1.0 if rootPC ∉ pcs else 0.0` | rootPC defined; else 0.0 |

---

## 5. Implementation Contract

### 5.1 Clamp Rule
**Every derived dimension (78–95) MUST be wrapped in `clamp01()`.**

### 5.2 Identity Gating
```
if IDENTITY not in {Voicing, Shape}:
    indices[78:96] = 0.0
```

### 5.3 Root Gating
```
if rootPC is UNDEFINED:
    indices[81, 82, 91, 92, 95] = 0.0
```

### 5.4 Zero-Vector Handling
If a partition slice is all zeros, its normalized form is a zero vector, contributing 0 to similarity.

---

## 6. Reference

See `GA.Business.Core.AI.Embeddings.EmbeddingSchema` for constants.

**Version**: `OPTIC-K-v1.2.1`
