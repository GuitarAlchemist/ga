# Implementation Plan - Chatbot Integration

## Phase 1: Service Registration
**Goal:** Make GA.Business.ML services available to the GaChatbot dependency injection container.
- [x] Add project reference to `GA.Business.ML` in `GaChatbot.csproj` (if missing).
- [x] Register `FileBasedVectorIndex` as Singleton.
- [x] Register `SpectralRetrievalService` as Scoped.
- [x] Register `ModalFlavorService` as Singleton.
- [x] Ensure `VoicingExplanationService` is registered.

## Phase 2: Spectral RAG Integration
**Goal:** Upgrade the RAG pipeline to use the spectral retrieval logic.
- [x] Refactor `SpectralRagOrchestrator` to inject `ISpectralRetrievalService`.
- [x] Replace direct vector index usage with `_retrievalService.Search()`.
- [x] Implement preset selection logic (Tonal vs Atonal) based on basic intent (or default to Tonal for now).

## Phase 3: Modal Explanation Integration
**Goal:** Enhance voicing explanations with music theory context.
- [x] Refactor `VoicingExplanationService` to inject `IModalFlavorService`.
- [x] In the explanation generation flow, call `_modalFlavorService.Enrich(voicing)`.
- [x] Append flavor descriptions to the output explanation.

## Phase 4: Verification
- [ ] Manual test: Ask chatbot about a Lydian chord and verify "Lydian" context appears.
- [ ] Manual test: Verify search results return logically related chords (Spectral search).
