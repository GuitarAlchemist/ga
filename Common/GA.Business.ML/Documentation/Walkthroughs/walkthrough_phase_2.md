# Walkthrough — Phase 2: Guitar Input (Tablature Parser)

> **Goal**: Enable the chatbot to analyze ASCII tablature input ("Analyze this riff").

---

## 1. Features Implemented

### Tablature Parser (`GA.Business.ML.Tabs`)
- **`TabTokenizer`**: Parses multi-line ASCII strings into vertical slices (beats).
- **`TabToPitchConverter`**: Maps fret numbers + string tuning (Standard E) to pitch classes.
- **`TabAnalysisService`**: Coördinates parsing and generates OPTIC-K embeddings for each slice.

### Chatbot Integration
- **`TabAwareOrchestrator`**: Intercepts user messages using heuristics (`IsTablature`).
- **Input Pipeline**: 
  - Standard text → RAG
  - Tab block → `TabAnalysisService`
- **Output**: "I analyzed the riff and found N harmonic events."

---

## 2. Verification

### Unit Tests
Running `GA.Business.ML.Tests`:
- `TabTokenizerTests`: ✅ Verified slicing of 6-string tabs.
- `TabAnalysisServiceTests`: ✅ Verified embedding generation for "Smoke on the Water" riff.
- **Result**: 12/12 Tests Passed.

### Manual Verification
Input:
```
e|-----------------|
B|-----------------|
G|---0---0---------|
D|---2---0---0-----|
A|---3---2---2-----|
E|-----------3-----|
```
Result: Identified C Major, G/B, and G Major chords.

---

## 3. Key Decisions

- **Heuristic Detection**: Detects tabs by looking for multiple lines starting with string notes or containing dashes/pipes.
- **Standard Tuning Default**: MVP assumes E Standard (E-A-D-G-B-e). Future phases will support custom tunings.
- **Orchestrator Pattern**: Used decorator/chain-of-responsibility pattern for `TabAwareOrchestrator` wrapping `SpectralRagOrchestrator`.
