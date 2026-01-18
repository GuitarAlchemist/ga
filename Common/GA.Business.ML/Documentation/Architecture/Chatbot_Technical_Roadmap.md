# Guitar Alchemist Chatbot — Technical Roadmap

> **A Harmonic Intelligence System for Guitarists**

This roadmap describes how to build a production-grade AI chatbot for guitarists using **OPTIC-K harmonic embeddings**, **Phase-Sphere geometry**, and **wavelet-based temporal analysis**, with full support for **tablature, scores, voicings, and progression analysis**.

---

## Design Philosophy

| Component | Role |
|-----------|------|
| **OPTIC-K** | Provides musical truth |
| **Wavelets** | Provides musical motion |
| **LLM** | Provides language, explanation, and planning |
| **Database** | Provides reality |

> No hallucinated chords. No fake theory. Everything is grounded in geometry.

---

## Phase 1 — Build the Harmonic Truth Layer

*Creates the musical coordinate system.*

### 1.1 Implement OPTIC-K Embedding Engine

Create a service that takes any musical object and outputs its OPTIC-K embedding.

**Inputs**:
- Pitch-class sets
- Chord symbols
- Guitar voicings (string/fret)
- Scale definitions

**Outputs** (109-D OPTIC-K vector):

| Partition | Content |
|-----------|---------|
| IDENTITY | Object type encoding |
| STRUCTURE | Interval Class Vector |
| SPECTRAL | DFT magnitudes + phases |
| MORPHOLOGY | String layout, boxes, spans |
| CONTEXT | Harmonic function, tonal gravity |
| SYMBOLIC | Tags |

**Implementation**:
- [x] DFT of pitch-class sets
- [x] Phase Sphere normalization
- [ ] Lewin duality (ICV ↔ |DFT|² consistency)

---

### 1.2 Implement Phase Sphere Geometry

Add a `PhaseSphereService`:

| Function | Description |
|----------|-------------|
| `Normalize()` | Normalize spectral vectors |
| `GeodesicDistance()` | Compute distance on sphere |
| `ComputePhi5()` | Fifths-axis phase (key encoding) |
| `ComputeBarycenter()` | Centroid of progressions |
| `ComputeVelocity()` | Voice-leading cost rate |

> This is the **harmonic physics engine**.

---

### 1.3 Build the Guitar Knowledge Base

Create a database of real musical objects:

| Field | Content |
|-------|---------|
| ID | Chord / scale / voicing identifier |
| PitchClasses | `[0, 4, 7]` |
| Fingering | `x32010` |
| Embedding | 109-D OPTIC-K vector |
| Metadata | Difficulty, box shape, register |
| Tags | `jazz`, `blues`, `open-chord`, `drop-2` |

> This is your **ground truth**.

---

## Phase 2 — Support Guitar Input (Tablature & Score)

*Makes the chatbot guitarist-native.*

### 2.1 Guitar Tablature Parser

**Parse formats**:
- ASCII tabs
- Guitar Pro / MusicXML (future)

**Convert to**:
- Per-string pitch
- Per-chord pitch-class sets
- Per-event timing

**Generate**:
- OPTIC-K embeddings per chord
- Spectral velocity over time
- Barycenter drift

**Enables**:
- *"Analyze this riff"*
- *"Why does this progression feel tense?"*
- *"What key is this drifting toward?"*

---

### 2.2 Score Parser (Optional)

**Parse**:
- MusicXML
- MIDI

**Extract**:
- Vertical harmonies
- Voice-leading paths
- Phrase boundaries

Map to OPTIC-K exactly like tabs.

---

## Phase 3 — Add Musical Motion via Wavelets

*Turns harmony into a dynamical system.*

### 3.1 Extract Harmonic State Signals

From the progression, compute time series:

| Signal | Meaning |
|--------|---------|
| `|F₅|(t)` | Diatonic stability |
| `φ₅(t)` | Key drift |
| `entropy(t)` | Harmonic temperature |
| `velocity(t)` | Voice-leading cost |

---

### 3.2 Apply Discrete Wavelet Transform

Use **db4** or **Haar** on each signal.

**Decompose into**:

| Level | Captures |
|-------|----------|
| Approximation | Phrase-level harmonic drift |
| Detail 1 | Harmonic rhythm |
| Detail 2 | Voice-leading motion |
| Detail 3 | Chromatic flicker |

Store wavelet coefficients as **temporal features**.

> Now the system knows: **where** harmony is, **how** it's moving, **at what scale**.

---

## Phase 4 — Retrieval Engine (Spectral RAG)

*Makes the chatbot smart without hallucinating.*

### 4.1 Vector Database

Store all guitar voicings and chords as:
- OPTIC-K embeddings
- Tags
- Difficulty
- Instrument layout

**Support**:
- Vector similarity search
- Metadata filters

---

### 4.2 Multi-Partition Similarity Scoring

Implement OPTIC-K weighted similarity:

$$\text{score} = \sum_{p} w_p \cdot \cos(\vec{A}_p, \vec{B}_p)$$

| Partition | Weight (Tonal) | Weight (Atonal) |
|-----------|----------------|-----------------|
| IDENTITY | 0.05 | 0.05 |
| STRUCTURE | 0.45 | 0.60 |
| SPECTRAL | 0.20 | 0.25 |
| MORPHOLOGY | 0.15 | 0.05 |
| CONTEXT | 0.10 | 0.00 |
| SYMBOLIC | 0.05 | 0.05 |

**Presets**: Tonal/Guitarist, Atonal/Exploratory, Jazz/Functional

---

## Phase 5 — The Chat Orchestrator

*This is the brain.*

### 5.1 User Request Pipeline

**Example query**:
> *"Give me a jazzier version of this Dm7, playable in 5th position"*

**Pipeline**:

1. **Parse intent**
   - target = Dm7
   - adjective = jazzier
   - constraints = 5th position, guitar

2. **Find base OPTIC-K embedding**

3. **Select preset** (Jazz)

4. **Apply filters** (guitar, box, difficulty)

5. **Retrieve top K** from vector DB

6. **Re-rank** using true OPTIC-K scoring + Phase Sphere distance

7. **Generate deterministic explanations**:
   - What tones changed
   - What spectral axes increased
   - What voice-leading improved

---

### 5.2 LLM as Narrator

The LLM receives:
- User request
- List of retrieved voicings
- Explanation facts

**LLM responsibilities**:
- Choose the best 3–5 options
- Explain why they work
- Suggest next steps

> [!CAUTION]
> The LLM **never invents chords**. It only talks about what OPTIC-K retrieved.

---

## Phase 6 — Advanced Guitarist Features

*Once the core works, add these.*

### 6.1 "Play and Analyze"

User pastes a riff or progression.

**System shows**:
- Tonal drift visualization
- Tension curve
- Suggested smoother paths
- Modulation targets

---

### 6.2 "Where Can I Go From Here?"

Given a voicing:
- Compute nearby chords on Phase Sphere
- Filter by playability
- Return musically plausible next steps

---

### 6.3 Style Learning

Use wavelet features to learn:
- Jazz vs. Rock vs. Metal
- Stable vs. wandering harmony
- Chromatic vs. diatonic language

---

## Summary: What You End Up With

A chatbot that can:

| Capability | Implementation |
|------------|----------------|
| ✅ Understand guitar shapes | Morphology partition |
| ✅ Understand harmonic function | Context + Spectral |
| ✅ Understand motion over time | DWT on scalar signals |
| ✅ Explain in musical language | LLM narrator |
| ✅ Never hallucinate voicings | Retrieval-only architecture |
| ✅ Suggest playable, intelligent options | Filtered vector search |

> This is not a chatbot that *talks about* music.
> 
> It is a chatbot that **lives inside harmonic space**.
> 
> And guitarists will feel it.

---

## Implementation Status

| Phase | Status | Location |
|-------|--------|----------|
| 1.1 OPTIC-K Engine | ✅ Done | `MusicalEmbeddingGenerator.cs` |
| 1.2 Phase Sphere | ✅ Done | `SpectralRagOrchestrator.cs` |
| 1.3 Knowledge Base | ✅ Done | MongoDB + `VoicingEntity` |
| 2.1 Tab Parser | ✅ Done | `TabAnalysisService.cs` |
| 2.2 Score Parser | 🔄 Partial | `MIDI` detection implemented |
| 3.1 Harmonic Signals | ✅ Done | `ProgressionSignalService.cs` |
| 3.2 DWT | ✅ Done | `WaveletTransformService.cs` |
| 4.1 Vector DB | ✅ Done | `FileBasedVectorIndex.cs` |
| 4.2 Similarity Scoring | ✅ Done | `EmbeddingSchema.cs` weights |
| 5.1 Orchestrator | ✅ Done | `ProductionOrchestrator.cs` |
| 5.2 LLM Narrator | ✅ Done | `GroundedNarrator` integrated |
| 6.x Advanced | ✅ Done | `AdvancedTabSolver.cs`, `ModulationAnalyzer.cs` |

---

## References

- [OPTIC-K_Applications.md](OPTIC-K_Applications.md) — Practical applications
- [Spectral_RAG_Implementation_Plan.md](Spectral_RAG_Implementation_Plan.md) — DWT integration
- [MathFoundations/Math_Foundations_DFT.md](MathFoundations/Math_Foundations_DFT.md) — DFT theory
- [MathFoundations/Math_Foundations_DWT.md](MathFoundations/Math_Foundations_DWT.md) — DWT theory
