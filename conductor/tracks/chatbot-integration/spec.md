# Chatbot Integration Specification

## Architecture
The `GaChatbot` application acts as the orchestration layer for user interactions. It delegates harmonic intelligence tasks to `GA.Business.ML`.

### 1. Spectral RAG Pipeline
**Current:** `SpectralRagOrchestrator` uses `FileBasedVectorIndex` directly with simple cosine similarity.
**Target:** `SpectralRagOrchestrator` should use `SpectralRetrievalService`.
- **Logic:**
    1. Parse user query (Intent detection - existing/mock).
    2. Select Search Preset (Tonal/Atonal) based on intent.
    3. Call `SpectralRetrievalService.Search(queryVec, preset)`.
    4. Pass results to LLM/Explainer.

### 2. Modal Explanations
**Current:** `VoicingExplanationService` generates static text.
**Target:** `VoicingExplanationService` should enrich voicings with `ModalFlavorService`.
- **Logic:**
    1. During explanation generation, call `ModalFlavorService.Enrich()`.
    2. If flavor tags found (e.g., "Flavor:Lydian"), add specific narrative text ("This voicing has a Lydian character due to the #4...").

## Service Registration
- `Program.cs` must register:
    - `FileBasedVectorIndex` (Singleton)
    - `SpectralRetrievalService` (Transient/Scoped)
    - `ModalFlavorService` (Singleton - loads config once)
    - `VoicingExplanationService` (Transient)
