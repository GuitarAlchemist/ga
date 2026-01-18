# Walkthrough — Phase 3: Musical Motion (Wavelets)

> **Goal**: Extract time-domain features from musical progressions to understand harmonic motion, tension, and structural boundaries.

---

## 1. Features Implemented

### Signal Extraction (`GA.Business.ML.Wavelets`)
- **`ProgressionSignalService`**: Converts a sequence of `VoicingDocument` objects into multiple time-series signals:
    - **Stability**: Harmonic consonance over time.
    - **Tension**: $1.0 - Consonance$.
    - **Entropy**: Spectral peakiness (organization) over time.
    - **Velocity**: Rate of change in the OPTIC-K embedding space between chords.

### Wavelet Transform
- **`WaveletTransformService`**: Implements multi-level Discrete Wavelet Transform (DWT) using Haar and Daubechies-4 filters.
    - Supports adaptive level selection based on signal length.
    - Extracts 16 statistical features (Mean, StdDev, Energy, Entropy) per signal decomposition.

### Progression Embedding
- **`ProgressionEmbeddingService`**: Generates a unified **64-dimensional embedding** for an entire progression by concatenating wavelet features from all extracted signals.
    - This embedding captures the "character" of the motion, not just the chords themselves.

---

## 2. Verification

### Unit Tests
Running `GA.Business.ML.Tests`:
- `ProgressionEmbeddingTests`: ✅ Verified generation of 64-dim vectors for a C-G-Am-F progression.
- **Result**: 1/1 New Tests Passed (346/346 total in project).

---

## 3. Mathematical Grounding

- **DWT (Discrete Wavelet Transform)**: Unlike Fourier transforms which lose time information, Wavelets allow us to localize "musical events" (like sudden changes in tension) in both time and frequency (scale).
- **OPTIC-K Velocity**: By calculating Euclidean distance between vectors in the 216-dimensional space, we get a rigorous measure of "Harmonic Distance" traveled between two chords.
- **Spectral Entropy**: Measures the organization of the power spectrum (ICV), identifying "Peakiness" vs "Noise".

---

## 4. Next Steps

- **Story: Show tension curve**: Visualize the `Tension(t)` signal in the chatbot response.
- **Story: Phrase boundaries**: Use high-frequency wavelet detail coefficients to identify where a "harmonic shift" occurred (potential phrase end).
