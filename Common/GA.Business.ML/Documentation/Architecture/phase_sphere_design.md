# Phase Sphere: Spectral Geometry for Pitch-Class Sets

## Overview

The **Phase Sphere** is a geometric framework for understanding harmonic relationships through Fourier analysis of pitch-class sets. While interval-class vectors describe the *shadow* of harmonic structure, the phase sphere reveals its true *orientation* in spectral space.

---

## 1. From Fourier Coefficients to Rotating Phasors

### The DFT of a Pitch-Class Set

Given a pitch-class set $X \subset \mathbb{Z}_{12}$, we encode it as a 12-vector $x[n]$ (binary or weighted) and compute its DFT:

$$F_k = \sum_{n=0}^{11} x[n] \cdot e^{-2\pi i k n / 12}$$

Each $F_k$ is a **complex number** expressible as a phasor:

$$F_k = |F_k| \cdot e^{i\phi_k}$$

### Musical Interpretation

| Component | Meaning |
|-----------|---------|
| $k$ | Which interval cycle (periodicity) being measured |
| $\|F_k\|$ | How strongly the set supports that cycle |
| $\phi_k$ | Where on that cycle the set is located |

### Example: The Circle of Fifths ($k=5$)

- $\|F_5\|$ = "diatonicness" — how well the set aligns with fifth-stacked structures
- $\phi_5$ = key-region on the fifths circle

Two sets can be **equally diatonic** (same $\|F_5\|$) but rotated by a tritone in fifth-space — that is **pure phase difference**.

---

## 2. The Phase Sphere Construction

### Spectral Vector

For $N=12$, the meaningful Fourier components are $k=1...6$ (higher indices are conjugates).

Define the **spectral vector**:

$$S(X) = (|F_1|e^{i\phi_1}, |F_2|e^{i\phi_2}, \ldots, |F_6|e^{i\phi_6})$$

This is a point in $\mathbb{C}^6$ (equivalently $\mathbb{R}^{12}$).

### Normalization

Normalize to unit length:

$$\hat{S}(X) = \frac{S(X)}{\|S(X)\|}$$

Now **all pitch-class sets live on the surface of a high-dimensional unit sphere** — the Phase Sphere.

### Geometric Properties

| Property | Description |
|----------|-------------|
| Dimension | Surface of 11-sphere in $\mathbb{R}^{12}$ |
| Transposition | Rigid rotation: $F_k(X+t) = e^{-2\pi i k t / 12} F_k(X)$ |
| Distance | Angle between normalized spectral vectors |

---

## 3. Phase Sphere vs. Prime Form

### What Prime Form Sees

- Transposition equivalence
- Inversion equivalence  
- Interval content (Forte numbers)

### What Phase Sphere Sees

- Interval content (via magnitudes)
- **Spectral orientation** (via phases)
- **Continuous movement** between harmonies
- **Transposition as rotation**

> *"Prime forms label the cities. The phase sphere is the terrain between them."*

---

## 4. Voice-Leading as Geodesics

### Spectral Distance

Given two pitch-class sets $A, B$, compute their normalized spectra $\hat{S}(A), \hat{S}(B)$.

The **spectral distance** is the angle between them:

$$\theta = \arccos(\Re(\hat{S}(A) \cdot \overline{\hat{S}(B)}))$$

| Angle | Harmonic Relationship |
|-------|----------------------|
| Small | Similar harmonic "color" |
| Large | Radical harmonic change |

### Implication for Composition

Voice-leading becomes finding **short geodesics on the phase sphere**.

Two wildly different pc-sets in Forte space can be **neighbors in spectral space** if their orientations match.

---

## 5. Z-Related Sets: Antipodal Twins

### Lewin's Lemma

Z-related sets share the same $|F_k|$ for all $k$ — identical interval-class vectors.

On the phase sphere, they lie on the **same latitude** (same magnitudes).

But their **phases differ** — they are different points on the same shell, often **nearly antipodal**.

### Musical Phenomenon

Z-related sets:
- Sound "equally complex"
- Pull harmony in **opposite directions**
- Are *spectral twins looking in opposite ways*

---

## 6. Maximally Even Sets as Phase Attractors

### Spectral Poles

Maximally even sets maximize specific $|F_k|$, placing them near **polar positions** on harmonic axes:

| Set Type | Dominant Component | Position |
|----------|-------------------|----------|
| Diatonic | Large $\|F_5\|$ | Fifths-axis pole |
| Whole-tone | Large $\|F_2\|$ | Tritone-axis pole |
| Diminished | Large $\|F_3\|$ | Minor-third pole |
| Augmented | Large $\|F_4\|$ | Major-third pole |

### Gravitational Wells

These become **spectral attractors**. Progressions drifting toward them feel like **resolving**, even in atonality.

---

## 7. Implementation in OPTIC-K v1.3.1

### Current Schema (Indices 96-108)

```
SPECTRAL GEOMETRY (96-108)
├── Magnitudes k=1..6 (96-101): Cycle strengths
├── Phases k=1..6 (102-107): Orientations  
└── Spectral Entropy (108): Organization measure
```

### Proposed Enhancements

#### 7.1 PhaseSphereService

```csharp
public class PhaseSphereService
{
    public Complex[] ComputeSpectralVector(int[] pitchClasses);
    public Complex[] NormalizeToSphere(Complex[] spectralVector);
    public double SpectralDistance(int[] setA, int[] setB);
    public bool AreZRelated(int[] setA, int[] setB, double tolerance = 1e-6);
}
```

#### 7.2 Enhanced Embedding Features (v1.4 Roadmap)

| Index | Feature | Description |
|-------|---------|-------------|
| 109 | PhaseCoherence | How aligned the phases are |
| 110 | SpectralLatitude | Total spectral energy |
| 111 | FifthsAxisProjection | Diatonicness direction |
| 112 | TritoneAxisProjection | Symmetric division |

---

## References

- Lewin (1959): "Re: Intervallic Relations Between Two Collections"
- Amiot (2016): *Music Through Fourier Space*
- Tymoczko (2011): *A Geometry of Music*

> *"The phase sphere is the true harmonic space of atonal music."*
