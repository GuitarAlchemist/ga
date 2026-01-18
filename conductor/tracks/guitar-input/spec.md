# Specification - Guitar Input (Tablature Parser)

## Overview
The goal is to enable the system to ingest ASCII guitar tablature, parse it into musical structures (sequences of chords/notes), and generate OPTIC-K embeddings for analysis. This is the foundation for the "Analyze this riff" feature.

## Requirements

### 1. ASCII Tab Tokenizer
- **Input:** Standard 6-string ASCII tablature blocks.
- **Handling:**
    - Multiple strings (E, A, D, G, B, e).
    - Fret numbers (0-24).
    - Basic ornaments (h, p, /, \, s, b, v) - initially tokenized but potentially ignored for raw harmonic analysis.
    - Bars/measures (`|`).
- **Output:** A structured representation of "time slices" where each slice contains the fretted notes across the 6 strings.

### 2. Tab-to-Pitch Conversion
- **Logic:**
    - Support Standard Tuning (E2, A2, D3, G3, B3, E4) initially.
    - Convert `(String, Fret)` to MIDI Note and Pitch Class.
    - Aggregate simultaneous notes into `VoicingDocument` objects for the embedding generator.

### 3. Harmonic Analysis
- **Integration:**
    - Pass parsed voicings to `MusicalEmbeddingGenerator`.
    - Generate OPTIC-K vectors for each "slice" or detected chord.
- **Goal:** Identify the harmonic content of a riff.

### 4. User Interaction (Chatbot)
- **Feature:** User pastes a tab block.
- **Response:** The chatbot identifies the chords, the key (if possible via spectral drift), and provides a summary.

## Technical Constraints
- No external heavy dependencies for parsing if possible (keep it lightweight and deterministic).
- Must handle varying whitespace and common tab variations.
