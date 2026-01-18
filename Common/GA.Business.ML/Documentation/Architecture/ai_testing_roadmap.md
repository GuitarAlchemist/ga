# AI Testing Roadmap: The Semantic Basin Paradigm

This roadmap outlines the transition from brittle keyword-based testing to robust, AI-driven semantic assertions in the Guitar Alchemist project.

## Vision
To treat the AI-generated outputs (stories, chatbot responses, theory explanations) as **cognitive surfaces** that must land within specified **semantic basins**, rather than exact string matches.

## Phase 0: Foundations (Level 0) - [COMPLETED]
**Goal:** Deterministic, millisecond-latency semantic checks using local embeddings.

- [x] **`GA.Testing.Semantic` library**: Generic core for vector assertions.
- [x] **SIMD Optimization**: Using TensorPrimitives for near-zero latency similarity.
- [x] **Embedding Cache**: SHA256-based persistence to make subsequent test runs instantaneous.
- [x] **Concept Probes**: Mapping nomenclature to vector basins (Mood, Genre, Structure).

## Phase 1: Robustness & Directional Analysis (Level 2) - [COMPLETED]
**Goal:** Validating the "behavior" of semantic shifts across chords.

- [x] **Transposition Invariance**: Verified that Hendrix chords maintain semantic identity across keys.
- [x] **Complexity Monotonicity**: Verified that adding extensions (7, 9, 11) linearly increases sophistication similarity.

### Phase 5: Advanced Semantic Validation (Level 1 & 2) [COMPLETED]
*   **Goal**: Ensure semantic search quality and persona adherence.
*   **Tasks**:
    *   [x] Register Stability Test (Level 2): Emotional tone vs physical register drift.
    *   [x] Semantic CLI Benchmarks: Add "Register Drift" and "Mood Precision" to `GaCLI`.
    *   [x] Chatbot Persona Validation (Level 1): Expert Reasoning Judge for "Guitar Alchemist" persona.

## Phase 2: Higher-Order Semantic Validation (Level 1) - [COMPLETED]
**Goal:** Validating open-ended reasoning using "LLM-as-Judge" (Mocked).

- [x] **Interval Vector Emotional Analysis**: Mapping raw ICV strings to emotional basins via nomenclature.
- [x] **Contextual Harmonic Accuracy**: Rubric-based evaluation of narrative function using `IJudgeService`.
- [x] **Textural Spacing Differential**: Measuring drift between open and closed voicings.

## Phase 3: Real AI Integration - [COMPLETED]
**Goal:** Replacing mocks with real local LLM integration for live reasoning validation.

- [x] **`OllamaJudgeService`**: Implementation of `IJudgeService` using local Ollama API.
- [x] **Gated Execution**: Integration of `Level1` tests into CI with optional skipping for local dev speed.
- [x] **Storytelling Quality Rubrics**: Expanding the rubric library for deep musical insight validation.

## CI/CD Integration
- **Fast Path**: Level 0 & Level 2 tests run on every commit (instantaneous via cache).
- **Deep Path**: Level 1 real AI tests run on PR merges or Nightly builds.
