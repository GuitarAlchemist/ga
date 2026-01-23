# Musical Embedding Schema (v1.4.1 - code-aligned)

Version: OPTIC-K-v1.4 (EmbeddingSchema.cs)
Date: 2026-01-21
Total dimension: 216
Defined indices: 0-135
Reserved indices: 136-215

Guitar Alchemist - Canonical vector space (OPTIC/K aligned + spectral).

---

## 0. Summary

### 0.1 Macro partitions and weights (default Tonal preset)

| Section | Dims | Index | Weight | Description |
|---|---|---|---|---|
| IDENTITY | 6 | 0-5 | N/A | Hard filter only, not used in similarity. |
| STRUCTURE | 24 | 6-29 | 0.45 | OPTIC/K core pitch-class set invariants. |
| MORPHOLOGY | 24 | 30-53 | 0.25 | Physical realization (fretboard geometry). |
| CONTEXT | 12 | 54-65 | 0.20 | Harmonic function and temporal motion. |
| SYMBOLIC | 12 | 66-77 | 0.10 | Technique and lineage/style tags. |
| EXTENSIONS | 18 | 78-95 | 0.00 | Derived features, informational only. |
| SPECTRAL | 13 | 96-108 | 0.00 | DFT-based spectral geometry. |
| MODAL | 19 | 109-127 | 0.00 | Modal flavor flags (not yet scored). |
| HIERARCHY | 8 | 128-135 | 0.00 | Structural complexity (partial). |
| RESERVED | 80 | 136-215 | 0.00 | Reserved for future partitions. |

### 0.2 Similarity scoring

Similarity(A, B) = sum(weight[p] * cosine(normalize(A[p]), normalize(B[p]))).

Search presets (from SpectralRetrievalService):

| Preset | Structure | Morphology | Context | Symbolic | Spectral |
|---|---:|---:|---:|---:|---:|
| Tonal | 0.45 | 0.25 | 0.20 | 0.10 | 0.00 |
| Atonal | 0.80 | 0.10 | 0.05 | 0.05 | 0.00 |
| Guitar | 0.20 | 0.70 | 0.05 | 0.05 | 0.00 |
| Jazz | 0.30 | 0.10 | 0.40 | 0.20 | 0.00 |
| Spectral | 0.00 | 0.00 | 0.00 | 0.00 | 1.00 |

Spectral similarity (Spectral preset only) combines:
- Magnitude cosine similarity across 96-101
- Phase alignment across 102-107 using wrap-around distance on [0,1]
- Final spectral similarity = 0.4 * magnitude + 0.6 * phase

---

## 1. IDENTITY (0-5)

One-hot (soft) object type flags. Unknown = all zeros.

| Index | Name |
|---:|---|
| 0 | Chord |
| 1 | Scale |
| 2 | Voicing |
| 3 | Shape |
| 4 | IntervalSet |
| 5 | PitchClassSet |

Composite logic:
- Voicing also sets Chord and PitchClassSet to 1.0
- Chord and Scale also set PitchClassSet to 1.0

---

## 2. STRUCTURE (6-29)

Indices 6-17: Pitch-class chroma (presence). If root is known, add +1.0 to that pitch class.

Index 18: Cardinality
- (uniquePitchClasses / 12.0) * 2.0

Indices 19-24: Interval Class Vector (ICV)
- Parsed from the ICV string, digit by digit

Index 25: Complementarity (K)

Indices 26-29: Tonal properties
- 26: Consonance
- 27: Brightness
- 28: Tonal stability (1.0 if root known, else 0.0)
- 29: Reserved

---

## 3. MORPHOLOGY (30-53)

Indices 30-41: Bass pitch-class one-hot (12 dims)

Indices 42-48: Shape and difficulty features
- 42: Span (normalized)
- 43: Note count (normalized)
- 44: IsRootless (1.0 or 0.0)
- 45-46: Melody pitch class (circular encoding: sin, cos)
- 47: Average fret = (averageFret / 12.0) * 2.0
- 48: Barre required = 2.0 if true, else 0.0

Indices 49-53: Reserved

---

## 4. CONTEXT (54-65)

Indices 54-56: Harmonic function
- 54: Tonic
- 55: Subdominant
- 56: Dominant

Index 57: Stability delta
Index 58: Tension
Index 59: IsResolution (1.0 or 0.0)
Indices 60-65: Reserved for key relationship

---

## 5. SYMBOLIC (66-77)

Binary tag bits from SymbolicTagRegistry.
- 6 dims for technique tags
- 6 dims for style/lineage tags

---

## 6. EXTENSIONS (78-95)

Derived features for voicings. All values are clamped to [0,1]. Root-gated values are 0 if root is undefined.

| Index | Name | Formula (summary) |
|---:|---|---|
| 78 | Harmonic Inertia | stability * (1 - tension) |
| 79 | Resolution Pressure | 0.7 * tension + 0.3 * (1 - stability) |
| 80 | Doubling Ratio | (N - uniquePCs) / max(1, N) |
| 81 | Root Doubled | count(rootPC) > 1 |
| 82 | Top Note Relative | ((topPC - rootPC + 12) % 12) / 11.0 |
| 83 | Smoothness Budget | 0.5 * doublingRatio + 0.7 * (1 - registerSpread) - 0.3 * localClustering |
| 84 | Mean Register | (meanMidi - MinMidi) / MidiRange |
| 85 | Register Spread | stddev / SpreadMax |
| 86 | Low End Weight | count(midi < LowThreshold) / N |
| 87 | High End Weight | count(midi > HighThreshold) / N |
| 88 | Local Clustering | count(diff(sortedMidi) <= CloseIntervalThreshold) / max(1, N - 1) |
| 89 | Roughness Proxy | low-weighted close interval sum / max(1, N - 1) |
| 90 | Bass-Melody Span | (maxMidi - minMidi) / SpanMax |
| 91 | Third Doubled | count(majorThird or minorThird) > 1 |
| 92 | Fifth Doubled | count(perfectFifth) > 1 |
| 93 | Open Position | (span > OpenPositionThreshold) |
| 94 | Inner Voice Density | count(LowThreshold < midi < HighThreshold) / max(1, N) |
| 95 | Omitted Root | rootPC not present |

---

## 7. SPECTRAL (96-108)

### 7.1 DFT and chroma rules

Chroma types:
- Salient chroma (voicing path):
  - Start with chroma[pc] = sum(noteWeight)
  - noteWeight = 1.0 + 0.5 if bass + 0.5 if melody
  - Normalize chroma by max(chroma)
- Structural chroma (PCS path):
  - Binary pitch-class presence (1.0 or 0.0)

DFT definition:
- DFT[k] = sum_{n=0..11} chroma[n] * exp(-2*pi*i*k*n/12), for k = 1..6

### 7.2 Magnitudes and phases (96-107)

For voicings, magnitudes and phases are derived from a unit-sphere normalized DFT:
- spectral = DFT(chroma) for k=1..6
- normalized = spectral / sqrt(sum(|spectral[k]|^2))

Embedding mapping:
- 96-101: |normalized[k]| for k=1..6
- 102-107: phase normalized to [0,1] via (arg + pi) / (2*pi)

### 7.3 Spectral entropy (108)

Entropy uses structural chroma (pitch classes) and k=1..6 only:
- P[k] = |DFT[k]|^2
- p_k = P[k] / sum(P)
- H = -sum(p_k * ln(p_k))
- Output = 1.0 - (H / ln(6))

---

## 8. MODAL FLAVORS (109-127)

Binary flags for modal color (19 dims):

| Index | Mode |
|---:|---|
| 109 | Ionian |
| 110 | Dorian |
| 111 | Phrygian |
| 112 | Lydian |
| 113 | Mixolydian |
| 114 | Aeolian |
| 115 | Locrian |
| 116 | Harmonic Minor |
| 117 | Melodic Minor |
| 118 | Whole Tone |
| 119 | Diminished (Octatonic) |
| 120 | Blues |
| 121 | Pentatonic Major |
| 122 | Pentatonic Minor |
| 123 | Phrygian Dominant |
| 124 | Lydian Augmented |
| 125 | Lydian Dominant |
| 126 | Altered (Super Locrian) |
| 127 | Locrian natural 2 |

---

## 9. HIERARCHY (128-135)

| Index | Name | Notes |
|---:|---|---|
| 128 | Harmonic Complexity Score | Normalized structural depth (0=Note, 1=Polychord) |
| 129-135 | Reserved | Not currently populated |

---

## 10. RESERVED (136-215)

These indices are reserved for future partitions. They are not currently populated.

---

## 11. Implementation references

- EmbeddingSchema constants: Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs
- Generator and spectral mapping: Common/GA.Business.ML/Embeddings/MusicalEmbeddingGenerator.cs
- Phase sphere and DFT: Common/GA.Business.ML/Embeddings/Services/PhaseSphereService.cs
- Similarity presets and spectral similarity: Common/GA.Business.ML/Embeddings/SpectralRetrievalService.cs

