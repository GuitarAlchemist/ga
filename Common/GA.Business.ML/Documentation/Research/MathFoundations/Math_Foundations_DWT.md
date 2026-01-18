# Discrete Wavelet Transform (DWT) — Mathematical Foundations

> This document provides the mathematical foundation for multi-resolution temporal analysis used in OPTIC-K progression embeddings.

---

## 1. Overview: DWT vs. DFT

| Transform | Domain | Captures | Resolution |
|-----------|--------|----------|------------|
| **DFT** | Static | Global spectral geometry | Frequency only |
| **DWT** | Dynamic | Localized oscillations | Time **and** frequency |

**Key insight**: DFT treats chords as *static objects*; DWT treats progressions as *dynamic signals*.

---

## 2. Multi-Resolution Signal Decomposition

The DWT decomposes signals through a **filter bank** — a cascade of high-pass and low-pass filters.

### 2.1 Decomposition Structure

```
Signal x[n]
    ↓
┌───────┴───────┐
↓               ↓
Low-pass      High-pass
(Approx)       (Detail)
  cA₁           cD₁       ← Level 1
    ↓
┌───┴───┐
↓       ↓
cA₂    cD₂                ← Level 2
  ↓
 ...
```

### 2.2 Coefficient Types

| Coefficients | Frequency | Captures | OPTIC-K Interpretation |
|--------------|-----------|----------|------------------------|
| **Approximation** ($cA_L$) | Low | Large-scale trends | Phrase-level dynamics, "slow trend" |
| **Detail** ($cD_1$) | High | Local changes | Harmonic rhythm, voice-leading |
| **Detail** ($cD_2$) | Medium | Medium-scale | Transitional patterns |
| **Detail** ($cD_3$) | Lower | Larger changes | Section-level shifts |

### 2.3 OPTIC-K Signal Sources

DWT is applied to **derived scalar signals**, not raw 216D vectors:

| Signal | Source | Meaning |
|--------|--------|---------|
| `entropy(t)` | Spectral entropy | Tension/organization |
| `velocity(t)` | $\theta$(chord$_t$, chord$_{t+1}$) | Voice-leading speed |
| `barycenter_drift(t)` | Spectral center delta | Harmonic gravity shift |
| `tonal_magnetism(t)` | $|F_5|(t)$ | Key pull strength |

---

## 3. Mathematical Properties

### 3.1 Complexity

| Algorithm | Time Complexity |
|-----------|-----------------|
| FFT | $O(N \log N)$ |
| **DWT** | $O(N)$ |

DWT is computationally superior for real-time applications.

### 3.2 Perfect Reconstruction

The DWT is invertible — original signal can be perfectly reconstructed:

$$x[n] = \text{IDWT}(\text{DWT}(x[n]))$$

### 3.3 Locality

Unlike DFT, wavelets have **compact support** — they are localized in both time and frequency, enabling:
- Detection of transient events
- Streaming analysis without waiting for full sequence
- Phrase boundary detection

---

## 4. Utility for Machine Learning

### 4.1 Progression Classification

| Style | Wavelet Signature |
|-------|-------------------|
| **Jazz** | High fine-scale detail, frequent modulation |
| **Classical** | Phrase-based structure, periodic coarse features |
| **Rock** | Repetitive, stable patterns, low detail energy |

**Feature vector**: `[OPTIC-K mean/std over progression] + [Wavelet coefficients]`

### 4.2 Tension Curve Modeling

$$\text{Tension}[t] = \alpha \cdot \text{Entropy}[t] + \beta \cdot \text{DetailEnergy}[t] + \gamma \cdot \text{KeyDistance}[t]$$

Combines:
- **Global** harmonic distance from tonic (Phase Sphere)
- **Local** rate of change (DWT detail coefficients)

### 4.3 Predictive Generation

For next-chord prediction, DWT provides multi-scale context:
- **Approximation**: "Where is the progression heading?"
- **Detail**: "What small-scale pattern is currently active?"

### 4.4 Phrase Boundary Detection

High detail coefficients indicate rapid harmonic changes → segment boundaries.

```csharp
var boundaries = FindPeaks(detailEnergy, threshold: 0.7);
```

---

## 5. Utility for Chatbots

### 5.1 Real-Time Latency

| Mode | Target Latency | Wavelet | Levels |
|------|----------------|---------|--------|
| **Interactive Chat** | < 50 ms | db4 | Adaptive (≤3) |
| **Live Performance** | < 20 ms | Haar | ≤ 2 |

### 5.2 Interpretability for NLG

Wavelet coefficients enable richer narrative explanations:

> *"This Dm7 acts as a **fast flicker** in the local ii-V pattern, while contributing to a **slow trend** toward the relative major tonal center."*

### 5.3 Streaming Analysis

Locality allows computation on a streaming basis — chatbot provides live analysis without waiting for the entire progression.

---

## 6. Implementation Recommendations

### 6.1 Wavelet Family Selection

| Wavelet | Use Case | Notes |
|---------|----------|-------|
| **db4** (default) | General musical structure | Best balance of smoothness and localization |
| **Haar** | Boundary detection | Ultra-fast, captures step-like changes |
| **db8/sym8** | Expressive phrases | Smoother, more compute |

### 6.2 Adaptive Decomposition Levels

$$L = \min\left(3, \lfloor \log_2(T) \rfloor - 2\right)$$

| T (chords) | $\log_2(T)$ | L |
|------------|-------------|---|
| 8 | 3 | 1 |
| 16 | 4 | 2 |
| 32 | 5 | 3 |
| 64 | 6 | 3 |

**Rationale**: Short progressions at level 3 produce mostly boundary artifacts.

### 6.3 Semantic Mapping

| Level | Musical Interpretation |
|-------|------------------------|
| Approximation | Phrase-level slow trend |
| Detail L1 | Harmonic rhythm |
| Detail L2 | Local voice-leading |
| Detail L3 | Very fast flicker |

---

## 7. Connection to OPTIC-K

| Component | Role |
|-----------|------|
| **Phase Sphere (DFT)** | Static harmonic geometry |
| **Wavelet Features (DWT)** | Dynamic multi-resolution structure |

Together they enable:
- Sequence-aware RAG retrieval
- Similarity with temporal context
- Classification, generation, and prediction tasks

---

## References

- Daubechies, I. (1992). Ten Lectures on Wavelets
- Mallat, S. (1989). A Theory for Multiresolution Signal Decomposition
- Strang, G. & Nguyen, T. Wavelets and Filter Banks
