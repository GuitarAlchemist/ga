# Spectral RAG Chatbot — Specifications

> A Harmonic Intelligence System for Guitarists

## Overview

Build a production-grade AI chatbot for guitarists using OPTIC-K harmonic embeddings, Phase-Sphere geometry, and wavelet-based temporal analysis. The system supports tablature, scores, voicings, and progression analysis.

## Design Philosophy

| Component | Role |
|-----------|------|
| **OPTIC-K** | Provides musical truth |
| **Wavelets** | Provides musical motion |
| **LLM** | Provides language, explanation, and planning |
| **Database** | Provides reality |

> ⚠️ **Core Constraint**: No hallucinated chords. No fake theory. Everything is grounded in geometry.

---

## Users

**Primary**: Guitarists seeking chord voicings, progression analysis, and music theory assistance.

**Secondary**: Music educators, composers, and songwriters.

---

## Goals

1. Enable natural language queries about chords and progressions
2. Provide playable guitar voicing suggestions
3. Analyze tablature and scores for harmonic content
4. Explain music theory in accessible terms
5. Never hallucinate musical content

---

## High-Level Features

### F1: Voicing Search
- Query by chord name, style, position
- Return ranked alternatives with explanations
- Filter by playability constraints

### F2: Progression Analysis
- Parse ASCII tablature
- Extract harmonic structure
- Identify key center and modulations

### F3: Tension Curve Visualization
- Compute tension from spectral features
- Detect phrase boundaries
- Show harmonic motion over time

### F4: Next-Chord Suggestions
- Given current voicing, suggest where to go
- Rank by Phase Sphere proximity
- Filter by voice-leading cost

### F5: Style Classification
- Classify progressions by style (Jazz, Rock, Classical)
- Use wavelet features for pattern recognition

---

## Non-Functional Requirements

| Requirement | Target |
|-------------|--------|
| Embedding latency | < 5ms per voicing |
| Search latency | < 200ms end-to-end |
| Wavelet processing | < 50ms for 32 chords |
| Hallucination rate | 0% for chord content |
| Voicing database | 1M+ indexed voicings |

---

## Out of Scope (This Track)

- Real-time audio input
- MIDI controller integration
- Mobile app
- Multi-language support

---

## Dependencies

- MongoDB with voicing data
- OPTIC-K embedding engine (GA.Business.ML)
- LLM backend (Ollama or cloud)
- Vector index (file-based or dedicated DB)

---

## Related Documents

- [Technical Roadmap](../../Common/GA.Business.ML/_docs/Chatbot_Technical_Roadmap.md)
- [Backlog with Acceptance Criteria](../../Common/GA.Business.ML/_docs/Chatbot_Backlog.md)
- [Math Foundations - DFT](../../Common/GA.Business.ML/_docs/MathFoundations/Math_Foundations_DFT.md)
- [Math Foundations - DWT](../../Common/GA.Business.ML/_docs/MathFoundations/Math_Foundations_DWT.md)
