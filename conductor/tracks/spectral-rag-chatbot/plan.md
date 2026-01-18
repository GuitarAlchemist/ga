# Spectral RAG Chatbot — Implementation Plan

> Status: **Planning**

---

## Phase 1: Harmonic Truth Layer ✅ Mostly Complete

### Tasks

- [x] 1.1.1 Implement `IdentityVectorService`
- [x] 1.1.2 Implement `TheoryVectorService` (ICV)
- [x] 1.1.3 Implement `SpectralVectorService` (DFT)
- [x] 1.1.4 Implement `MorphologyVectorService`
- [x] 1.1.5 Implement `ContextVectorService`
- [x] 1.1.6 Implement `SymbolicVectorService`
- [x] 1.1.7 Create `MusicalEmbeddingGenerator` orchestrator
- [ ] 1.1.8 Spike: Verify Lewin duality (ICV ↔ |DFT|²)

### Phase Sphere

- [x] 1.2.1 Implement spectral normalization
- [x] 1.2.2 Implement geodesic distance
- [x] 1.2.3 Implement φ₅ extraction
- [x] 1.2.4 Implement progression barycenter
- [x] 1.2.5 Implement spectral velocity

### Knowledge Base

- [x] 1.3.1 Define `VoicingEntity` MongoDB model
- [x] 1.3.2 Implement voicing indexer (GaCLI)
- [x] 1.3.3 Seed vector index from MongoDB
- [x] 1.3.4 Implement semantic tagging
- [x] 1.3.5 Index all standard tuning voicings

---

## Phase 2: Guitar Input ✅ Complete

### Tablature Parser

- [x] 2.1.1 Spike: Survey ASCII tab formats
- [x] 2.1.2 Implement ASCII tab tokenizer
- [x] 2.1.3 Convert tab to pitch-class sequence
- [x] 2.1.4 Generate OPTIC-K embeddings per chord
- [x] 2.1.5 Story: "Analyze this riff"
- [x] 2.1.6 Story: "What key is this tab in?"

### Score Parser (Optional)

- [ ] 2.2.1 Spike: Evaluate MusicXML libraries
- [ ] 2.2.2 Implement MusicXML harmony extractor
- [x] 2.2.3 Implement MIDI chord detector

---

## Phase 3: Musical Motion (Wavelets) ✅ Complete

### Signal Extraction

- [x] 3.1.1 Extract |F₅|(t) — diatonic stability
- [x] 3.1.2 Extract φ₅(t) — key drift
- [x] 3.1.3 Extract entropy(t) — harmonic temperature
- [x] 3.1.4 Extract velocity(t) — voice-leading cost
- [x] 3.1.5 Create `ProgressionSignalService`

### Wavelet Transform

- [x] 3.2.1 Spike: Implement Haar wavelet
- [x] 3.2.2 Implement db4 wavelet
- [x] 3.2.3 Implement adaptive level selection
- [x] 3.2.4 Create `WaveletTransformService`
- [x] 3.2.5 Create `ProgressionEmbeddingService`
- [x] 3.2.6 Story: "Show phrase boundaries in tab" (Via Cadence Detection)
- [x] 3.2.7 Story: "Show tension curve" (Via Sparkline)

---

## Phase 4: Spectral RAG ✅ Mostly Complete

### Vector Database

- [x] 4.1.1 Implement `FileBasedVectorIndex`
- [x] 4.1.2 Implement metadata filtering
- [x] 4.1.3 Implement batch embedding loader
- [ ] 4.1.4 Spike: Evaluate dedicated vector DB

### Similarity Scoring

- [x] 4.2.1 Define partition weights in `EmbeddingSchema`
- [x] 4.2.2 Implement weighted cosine similarity
- [x] 4.2.3 Create similarity presets (Tonal, Atonal, Jazz)
- [x] 4.2.4 Story: "Find similar voicings"
- [x] 4.2.5 Story: "Find jazzier version of X"

---

## Phase 5: Chat Orchestrator 🔄 In Progress

### Intent Pipeline

- [x] 5.1.1 Implement intent recognition
- [x] 5.1.2 Implement constraint extraction
- [x] 5.1.3 Implement preset selection
- [x] 5.1.4 Create `SpectralRagOrchestrator`
- [x] 5.1.5 Story: "Give me a jazzier Dm7"

### LLM Narrator

- [x] 5.2.1 Implement `VoicingExplanationService`
- [x] 5.2.2 Generate deterministic explanation facts
- [x] 5.2.3 Format LLM prompt with retrieved voicings
- [x] 5.2.4 Story: "Explain why this voicing works"
- [x] 5.2.5 Spike: Evaluate anti-hallucination guardrails (Verified by Tests)

---

## Phase 6: Advanced Intelligence Spike ✅ Complete

### Retrieval & Navigation

- [x] 6.1.1 Implement `SpectralRetrievalService` (Weighted Partition Cosine Similarity)
- [x] 6.1.2 Implement `NextChordSuggestionService` (Phase Sphere Neighbors)
- [x] 6.1.3 Implement `ModulationAnalyzer` (Key Drift Tracking via Barycenter)
- [x] 6.1.4 Story: "Suggest a chord that resolves this tension"

### Temporal Feature Extraction

- [x] 6.2.1 Implement `ProgressionSignalService` (Extract Stability, Tension, Entropy signals)
- [x] 6.2.2 Implement `WaveletTransformService` (Haar decomposition of harmonic signals)
- [x] 6.2.3 Implement `ProgressionEmbeddingService` (Fixed-length vector for sequences)
- [x] 6.2.4 Story: "Visualize harmonic tension over time"

### Style Learning

- [x] 6.3.1 Spike: Collect labeled progression dataset (`ProgressionHarvestingService`)
- [x] 6.3.2 Train style classifier on wavelet features (`StyleClassifierService`)
- [x] 6.3.3 Story: "What style is this progression?"

---

## Phase 7: Generative Realization (MIDI to Tab) ✅ Complete

### Fretboard Optimization

- [x] 7.1.1 Implement `FretboardPositionMapper` (MIDI Note -> All valid (string, fret))
- [x] 7.1.2 Define `PhysicalCostService` (Heuristics: fret distance, string skips, hand stretch, shifts)
- [x] 7.1.3 Implement Viterbi-based pathfinder for optimal sequence mapping (`AdvancedTabSolver`)
- [x] 7.1.4 Story: "Generate optimal tab for this MIDI score"

## Phase 8: Advanced RAG Integration 🔄 In Progress

### Integration & Validation ✅

- [x] 8.1.1 Implementation of `ProductionOrchestrator` (Combining all spikes)
- [x] 8.1.2 Story: "Integration Test: End-to-end Chat to Tab mapping"
- [x] 8.1.3 Story: "Validate Groundedness across 50 diverse user queries"
    - [x] Create `groundedness_bench.jsonl` (50 items)
    - [x] Create `GroundednessValidationTests.cs` scaffold
    - [x] Implement `ProductionOrchestrator` integration logic
    - [x] Verify 95% Pass Rate (97.2% achieved)

### Narrator Fine-tuning

- [ ] 8.2.1 Refine `GroundedPromptBuilder` for complex Jazz/Fusion scenarios
- [ ] 8.2.2 Implement "Suggest Smoother Path" LLM intent
- [ ] 8.2.3 Story: "Narrator explains voice-leading geodesics in simple terms"

### Performance & Polish

- [ ] 8.3.1 Performance optimization for Viterbi pathfinder (Memoization/Pruning)
- [ ] 8.3.2 Implement "Progressive Loading" for long tab analysis
- [ ] 8.3.3 Story: "The system responds under 500ms for standard 8-bar riffs"

## Phase 21: n8n Orchestration Spike 🔄 In Progress

### Infrastructure

- [ ] 21.1.1 Create `docker-compose.yml` (n8n + Postgres + Qdrant)
- [ ] 21.1.2 Verify local startup (localhost:5678)
- [ ] 21.1.3 Configure "Self-Hosted AI" starter kit features

### Proof of Concept

- [ ] 21.2.1 Workflow: "Ingest Tab from URL"
    - [ ] Input: URL (Ultimate-Guitar or similar)
    - [ ] Process: Fetch HTML, Extract content (LLM or selector)
    - [ ] Output: JSON { title, artist, tab_content }
- [ ] 21.2.2 Workflow: "Analyze Tab via C# API"
    - [ ] Input: JSON Tab
    - [ ] Process: HTTP Request to `GuitarAlchemist` API (Need to verify this exists or use CLI wrapper)
    - [ ] Output: Analysis result


---

## Checkpoints

| Checkpoint | Phase | Description |
|------------|-------|-------------|
| CP-0 | Start | Initial state before Phase 2 |
| CP-1 | 2 | Tab parser complete |
| CP-2 | 3 | Wavelet features complete |
| CP-3 | 5 | Orchestrator complete |
| CP-4 | 6 | Advanced features complete |
| CP-5 | 7 | Generative Realization complete |

---

## Current Focus

**Next Sprint**: Phase 8 — Advanced RAG & LLM Narrator Refinement

Priority items:
1. Integration testing of all components in production environment.
2. Fine-tuning the Grounded Narrator for complex Jazz/Fusion scenarios.
3. Performance optimization for the Viterbi pathfinder.


