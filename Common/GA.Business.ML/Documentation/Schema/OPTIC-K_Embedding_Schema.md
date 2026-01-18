# Musical Embedding Schema (v1.3.1)

**Guitar Alchemist – Canonical Vector Space (OPTIC/K-aligned)**

This document defines the 109-dimensional embedding format (v1.3.1).

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
| **EXTENSIONS** | 18 | 78–95 | **0.00** | Derived features (v1.2.1). |
| **SPECTRAL** | 13 | 96–108 | **0.00** | Spectral geometry (v1.3.1). |

**Total: 109 dims**

### 0.3 Similarity Formula (REQUIRED)

**Algorithm**: Weighted Partition Cosine

```
Similarity(A, B) = Σ (weight[p] × cosine(normalize(A[p]), normalize(B[p])))
```

Where:
- `p` ∈ {STRUCTURE, MORPHOLOGY, CONTEXT, SYMBOLIC}
- `normalize(v)` = v / ||v|| (L2 norm). If ||v|| = 0, return zero vector.
- IDENTITY is excluded (used for hard filtering only).
- EXTENSIONS and SPECTRAL are excluded from similarity weighting.

---

## 1. Versioning

`EmbeddingSchemaVersion = "OPTIC-K-v1.3.1"`

See `OPTIC-K_Embedding_Schema_v1.3.1.md` for full field definitions.

### Implementation Classes
- **Schema Definition**: `GA.Business.Core.AI.Musical.Schema.EmbeddingSchema`
- **Orchestration**: `GA.Business.Core.AI.Musical.Generation.MusicalEmbeddingGenerator`
- **IDENTITY**: `GA.Business.Core.AI.Musical.Partitions.IdentityVectorService`
- **STRUCTURE**: `GA.Business.Core.AI.Musical.Partitions.TheoryVectorService`
- **MORPHOLOGY**: `GA.Business.Core.AI.Musical.Partitions.MorphologyVectorService`
- **CONTEXT**: `GA.Business.Core.AI.Musical.Partitions.ContextVectorService`
- **SYMBOLIC**: `GA.Business.Core.AI.Musical.Partitions.SymbolicVectorService`
- **Search/Similarity**: `GA.Business.Core.Fretboard.Voicings.Search.GpuVoicingSearchStrategy`, `GA.Business.Core.Fretboard.Voicings.Search.CpuVoicingSearchStrategy`
