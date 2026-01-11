# Musical Embedding Schema (v1.1)

**Guitar Alchemist – Canonical Vector Space**

This document defines the 78-dimensional embedding format used by Guitar Alchemist. It distinguishes between the **Vector Partitions** (the semantic axes of the model) and **OPTIC/K Equivalence** (the mathematical theory used to encode musical structure).

---

## 1. Vector Partitions (Macro-Subspaces)

The vector is partitioned into 5 functional areas. The naming has been updated to avoid collision with the OPTIC/K acronym.

| Section | Dimensions | Index range | Description |
| :--- | :--- | :--- | :--- |
| **IDENTITY** | 6 | 0–5 | Defines "What the object is" (Chord, Scale, etc.) |
| **STRUCTURE** | 24 | 6–29 | **OPTIC/K Core**: Encodes pitch-class set invariants. |
| **MORPHOLOGY** | 24 | 30–53 | Physical realization (Fingering, Span, Ergonomics). |
| **CONTEXT** | 12 | 54–65 | Temporal and harmonic motion (Tension, Pull, Resoluton). |
| **SYMBOLIC** | 12 | 66–77 | Cultural/Technique tags (Jazz, Hendrix, Drop-2). |

**Total core musical vector size: 78**

---

## 2. STRUCTURE Subspace (Indices 6–29) — OPTIC/K Implementation

This section implements **OPTIC/K-Equivalence** as defined in the Harmonious Glossary. This relation groups pitch-class sets by deep structural invariants.

### 2.1 The OPTIC/K Invariants

| Component | Equivalence Axis | Mapping to Vector Indices |
| :--- | :--- | :--- |
| **O** | **Octave** | Encoded via **Pitch-Class Chroma** (Octave independent). |
| **P** | **Permutation** | Encoded via **Pitch-Class Chroma** (Order independent). |
| **T** | **Transposition** | Encoded via **Interval Class Vector** (T-invariant). |
| **I** | **Involution** | Encoded via **Interval Class Vector** (I-invariant). |
| **C** | **Cardinality** | Explicitly encoded at **Index 18**. |
| **K** | **Complementarity**| Encoded via **Complementarity Features** (Index 25). |

### 2.2 Structure Index Mapping

| Index | Meaning | Description |
| :--- | :--- | :--- |
| **6–17** | **Pitch-Class Chroma** | 12-dim activation. Covers **O** and **P**. |
| **18** | **Cardinality (C)** | Number of pitch classes / 12.0. |
| **19–24** | **Interval Class Vector (TIC)**| 6-dim T and I invariant structural signature. |
| **25** | **Complementarity (K)** | Proximity to the set's complement in vector space. |
| **26–29** | **Functional Tonal Props**| Tonic-ness, Dominant-pull, Tension, Stability. |

---

## 3. IDENTITY Subspace — Indices 0–5

Standard one-hot or soft one-hot encoding of the object type.

| Index | Meaning |
| :--- | :--- |
| 0 | Chord |
| 1 | Scale |
| 2 | Voicing |
| 3 | Shape |
| 4 | Interval set |
| 5 | Pitch-class set |

---

## 4. MORPHOLOGY Subspace — Indices 30–53

Encodes the physical "shape" of the musical object on the fretboard.

| Range | Meaning |
| :--- | :--- |
| 30–35 | Average fret per string |
| 36–41 | Finger usage histogram |
| 42–45 | Stretch / span |
| 46–49 | Position stability |
| 50–53 | Ergonomic difficulty |

---

## 5. CONTEXT Subspace — Indices 54–65

Encodes movement. If no context is provided, this MUST be zeroed.

| Index | Meaning |
| :--- | :--- |
| 54 | Local tonic distance |
| 55 | Dominant pull |
| 56 | Resolution tendency |
| ... | ... |

---

## 6. SYMBOLIC Subspace — Indices 66–77

Symbolic knowledge tags mapped to numeric weights.

| Range | Type | Examples |
| :--- | :--- | :--- |
| 66–71 | Technique | Drop-2, Shell, Quartal |
| 72–77 | Lineage / Style | Jazz, Neo-Soul, Hendrix |

---

## 7. Versioning

`EmbeddingSchemaVersion = "OPTIC-K-v1.1"`

This version preserves the 78-dim layout of v1 but corrects the semantic alignment of "OPTIC-K" to its proper music-theoretic definition within the Structure subspace.
