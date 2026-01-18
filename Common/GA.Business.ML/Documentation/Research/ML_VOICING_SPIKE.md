# ML-Informed Voicing Spike (Phase 7.2.1)

## Objective
Research and define the architecture for an ML model that improves the "Naturalness" of generative tablature realization beyond raw physical heuristics.

## Key Research Findings

### 1. Fretting-Transformer (2025)
*   **Architecture**: Encoder-Decoder Transformer.
*   **Input**: MIDI event sequence (pitch, duration, velocity).
*   **Output**: Multi-token sequence representing (String, Fret) pairs.
*   **Innovation**: Uses separate prediction heads for string selection and fret selection, constrained by instrument geometry.

### 2. Tablature as Language (Seq2Seq)
*   Treating guitar tablature as a "target language" allows the model to learn **idiomatic transitions** (e.g. why a player prefers a specific finger slide over a string jump).
*   **Bi-directional context**: Models like BERT (Encoder-only) are excellent for "Naturalness Ranking" (scoring a full sequence), while GPT-style (Decoder-only) models are better for "Generative Realization".

## Proposed Architecture for Guitar Alchemist

### Strategy A: The "Naturalness Ranker" (Discriminative)
*   **Model**: Small Cross-Encoder (Transformer-based).
*   **Input**: `(MIDI Sequence, Generated Tab Sequence)`.
*   **Output**: Scalar score [0, 1] representing "Likelihood a human would play this".
*   **Benefit**: Can be used to re-rank the Top-K results from our existing `AdvancedTabSolver` (Viterbi).

### Strategy B: The "Realization Transformer" (Generative)
*   **Model**: Encoder-Decoder (Seq2Seq).
*   **Input**: MIDI Pitch sequence.
*   **Output**: Structured Tab sequence.
*   **Benefit**: Can find solutions that heuristics might miss (e.g. intentional "open-string ringing" or stylistic quirks).

## Implementation Roadmap (Phase 7.2.2)

1.  **Dataset Collection**:
    *   Target: 100k+ bars of high-quality Guitar Pro / MIDI+Tab pairs.
    *   Sources: MySongBook, Ultimate-Guitar (public domain/CC subsets).
2.  **Feature Encoding**:
    *   Embed MIDI via OPTIC-K (Spectral features).
    *   Embed Tab via Morphology vectors (already implemented).
3.  **Training**:
    *   Train a small **DistilBERT-style** model on the "Naturalness" task (Binary classification: Human vs. Randomized Tab).
4.  **Integration**:
    *   Add `IMlNaturalnessRanker` to `AdvancedTabSolver`.
    *   `TotalCost = PhysicalHeuristicCost + (1.0 - MlNaturalnessScore) * Weight`.

## Conclusion
The **"Naturalness Ranker" (Strategy A)** is the most pragmatic next step. It complements our robust Viterbi solver without requiring a massive generative model shift.
