# OPTIC-K Applications Guide

> Practical applications of DFT and DWT spectral techniques in Guitar Alchemist.

---

## Overview

| Technique | Domain | Primary Use |
|-----------|--------|-------------|
| **DFT** | Static | Per-voicing indexing, similarity search |
| **DWT** | Dynamic | Progression analysis, real-time feedback |

---

## Part 1: DFT Applications (Static Voicings)

### 1.1 Voicing Indexing in MongoDB

Each voicing in the database receives a 216-dimensional OPTIC-K embedding computed via DFT.

**Pipeline**:
```
Voicing → Pitch-class set → DFT → Phase Sphere coordinates → Embedding → MongoDB
```

**Benefits**:
- Transposition-invariant similarity (magnitudes)
- Key-aware retrieval (phases)
- Sub-50ms embedding generation

### 1.2 Semantic Voicing Search

**Query types supported**:
| Query | Implementation |
|-------|----------------|
| "Find voicings similar to x32010" | Cosine similarity on OPTIC-K embeddings |
| "Show jazz-style Dm7 voicings" | Semantic tags + embedding filtering |
| "What chords fit in C major?" | Phase $\angle F_5$ filtering |

### 1.3 Chord Substitution Recommendations

Using spectral distance to find functionally similar voicings:

$$\text{Similarity}(A, B) = \cos(\theta) = \frac{\vec{A} \cdot \vec{B}}{|\vec{A}||\vec{B}|}$$

**Example**: C6 and Am7 have high spectral similarity due to shared pitch classes.

### 1.4 Tension/Consonance Scoring

DFT-derived features enable automatic scoring:
- **Spectral entropy** → harmonic complexity
- **$|F_5|$ magnitude** → diatonicity
- **Consonance** → sensory roughness estimate

---

## Part 2: DWT Applications (Progressions & Tablature)

### 2.1 Progression Classification

Train ML models to classify harmonic styles from wavelet features:

| Style | Signature Pattern |
|-------|-------------------|
| Jazz | High fine-scale detail, frequent key changes |
| Classical | Phrase-based structure, periodic patterns |
| Rock | Repetitive, stable, low detail energy |
| Neo-Soul | Rich extensions, smooth voice-leading |

**Feature vector**: `OPTIC-K aggregates + Wavelet coefficients`

### 2.2 Phrase Boundary Detection

Detect section changes in tablature by analyzing DWT detail coefficient spikes:

```csharp
var boundaries = FindPeaks(detailEnergy, threshold: 0.7);
```

**Use case**: Auto-segment a song into intro, verse, chorus, bridge.

### 2.3 Tension Curve Generation

Model musical tension as a function of time:

$$\text{Tension}[t] = \alpha \cdot \text{Entropy}[t] + \beta \cdot \text{DetailEnergy}[t] + \gamma \cdot \text{KeyDistance}[t]$$

**Applications**:
- Visualization in UI (tension graph under tablature)
- Composition assistance ("add tension here")
- Practice recommendations ("focus on this transition")

### 2.4 Next-Chord Prediction

For AI-assisted composition, DWT provides multi-scale context:

| Level | Information |
|-------|-------------|
| Approximation | Where the progression is heading |
| Detail L1 | Current harmonic rhythm |
| Detail L2 | Local voice-leading pattern |

### 2.5 Real-Time Chatbot Feedback

**Interactive chat mode** (< 50ms latency):
- Small windows (8-32 chords)
- Ring buffer for streaming analysis
- DWT on scalar signals only

**Example chatbot response**:
> *"This Dm7 acts as a fast transitional chord in a ii-V-I pattern, contributing to the slow trend toward G major resolution."*

---

## Part 3: Combined Applications

### 3.1 Context-Aware Voicing Retrieval

Combine static similarity with dynamic context:

```
Current progression context (DWT) + Query voicing (DFT) → Ranked recommendations
```

### 3.2 Style-Matched Substitutions

Find substitutions that match both:
- **Harmonic function** (DFT spectral similarity)
- **Stylistic fit** (DWT progression patterns)

### 3.3 Learning Path Generation

Analyze student's playing (DWT) and recommend voicings to practice (DFT retrieval):

1. Detect current skill level from progression analysis
2. Identify gaps in voicing vocabulary
3. Retrieve appropriately challenging voicings

---

## Implementation Status

| Application | Status | Location |
|-------------|--------|----------|
| Voicing indexing (DFT) | ✅ Implemented | `MusicalEmbeddingGenerator.cs` |
| Semantic search (DFT) | ✅ Implemented | `FileBasedVectorIndex.cs` |
| MongoDB seeding | ✅ Implemented | `GaChatbot/Program.cs` |
| Progression classification (DWT) | 📋 Proposed | `Spectral_RAG_Implementation_Plan.md` |
| Phrase boundary detection (DWT) | 📋 Proposed | Future |
| Tension curves (DWT) | 📋 Proposed | Future |
| Real-time chatbot (DWT) | 📋 Proposed | Future |

---

## References

- [Math_Foundations_DFT.md](MathFoundations/Math_Foundations_DFT.md) — DFT theory
- [Math_Foundations_DWT.md](MathFoundations/Math_Foundations_DWT.md) — DWT theory
- [Spectral_RAG_Implementation_Plan.md](Spectral_RAG_Implementation_Plan.md) — DWT implementation plan
