# Discrete Fourier Transform (DFT) — Mathematical Foundations

> This document provides the mathematical foundation for spectral analysis used in OPTIC-K embeddings.

---

## 1. Formal Definition

The DFT transforms a finite sequence of $N$ equally spaced samples into a frequency-domain representation.

### Analysis Equation (Forward DFT)

$$X[k] = \sum_{n=0}^{N-1} x[n] e^{-j\frac{2\pi}{N}kn} \quad \text{for } k=0, 1, \dots, N-1$$

### Inverse DFT (IDFT)

$$x[n] = \frac{1}{N} \sum_{k=0}^{N-1} X[k] e^{j\frac{2\pi}{N}kn} \quad \text{for } n=0, 1, \dots, N-1$$

### Complex Basis Functions

The DFT uses **complex exponentials** as basis functions:

$$e^{-j\theta} = \cos(\theta) - j\sin(\theta)$$

Each frequency component $X[k]$ encapsulates:
- **Magnitude** — strength of frequency component
- **Phase** — timing/offset of frequency component

---

## 2. Theoretical Foundations

| Property | Description |
|----------|-------------|
| **Orthogonality** | Basis functions are orthogonal, forming a basis in $N$-dimensional complex vector space |
| **Change of Basis** | DFT is a rotation from impulse basis (time) to sinusoid basis (frequency) |
| **Periodicity** | Both $x[n]$ and $X[k]$ repeat indefinitely: $x[n] = x[n+N]$ |
| **DTFT Sampling** | DFT samples the continuous DTFT at $N$ equally spaced points |

---

## 3. Core Properties

### 3.1 Linearity

$$\mathcal{F}\{a \cdot x[n] + b \cdot y[n]\} = a \cdot X[k] + b \cdot Y[k]$$

### 3.2 Conjugate Symmetry

For real-valued inputs:
$$X[k] = X^*[N-k]$$

Only half the spectrum needs to be stored or processed.

### 3.3 Convolution Theorem

Circular convolution in time ↔ point-wise multiplication in frequency:

$$x[n] \circledast h[n] \leftrightarrow X[k] \cdot H[k]$$

### 3.4 Parseval's Theorem (Energy Conservation)

$$\sum_{n=0}^{N-1} |x[n]|^2 = \frac{1}{N} \sum_{k=0}^{N-1} |X[k]|^2$$

---

## 4. Fast Fourier Transform (FFT)

| Algorithm | Complexity |
|-----------|------------|
| Direct DFT | $O(N^2)$ |
| **FFT** (Cooley-Tukey, 1965) | $O(N \log N)$ |

The FFT factorizes the DFT matrix into sparse factors, enabling real-time processing.

---

## 5. Limitations and Mitigation

### 5.1 Spectral Leakage

Occurs when signal frequency doesn't align with DFT bins → energy smears across spectrum.

**Mitigation**: Apply **window functions** (Hann, Hamming, Blackman) to taper edges to zero.

### 5.2 Uncertainty Principle

Fundamental trade-off between **time resolution** and **frequency resolution**:

$$\Delta t \cdot \Delta f \geq \frac{1}{4\pi}$$

Higher frequency precision requires more samples → reduced time localization.

---

## 6. Applications

| Domain | Application |
|--------|-------------|
| **Radar** | Pulse compression, Doppler filtering |
| **Medical Imaging** | MRI k-space reconstruction |
| **Audio Processing** | Spectral analysis, filtering, compression |
| **OPTIC-K** | Phase Sphere coordinates, transposition invariance |

---

## 7. Connection to OPTIC-K

In OPTIC-K, the DFT creates the **Phase Sphere** — a geometric coordinate system where:

- **Transposition** = rigid rotation of spectral vector
- **Harmonic similarity** = geometric proximity (cosine distance)
- **Magnitude** ($|X[k]|$) = transposition-invariant features
- **Phase** ($\angle X[k]$) = key-encoding features

### OPTIC-K Frequency Indices

| $k$ | Musical Interpretation |
|-----|----------------------|
| 1 | Chromatic density |
| 2 | Tritone affinity |
| 3 | Augmented affinity |
| 4 | Diminished affinity |
| 5 | **Diatonicity** (major/minor) |
| 6 | Whole-tone affinity |

---

## References

- Cooley, J.W. & Tukey, J.W. (1965). An Algorithm for Machine Calculation of Complex Fourier Series
- Oppenheim, A.V. & Schafer, R.W. Discrete-Time Signal Processing
- Lewin, D. (1959). Re: Intervallic Relations Between Two Collections of Notes
- Quinn, I. (2006). General Equal-Tempered Harmony
