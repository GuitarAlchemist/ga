# QA Status Report: Guitar Alchemist MVP (Small)

## 1. Verified Technical Behaviors
The following behaviors were verified via execution of `GaChatbot.exe` (Phase 12/13 build).

| Component | Behavior | Verification Evidence |
| :--- | :--- | :--- |
| **Modal Tagging** | `ModalFlavorService` correctly identifies "Lydian" characteristics (`#4` interval). | Input `Cmaj7#11` yielded explanation: *"reflects Lydian flavor characteristics"* (Step 15308). |
| **Modal Tagging** | `ModalFlavorService` correctly identifies "Dorian" characteristics (`6` interval). | Input `Dm6` yielded explanation: *"reflects Dorian flavor characteristics"* (Step 15314). |
| **Auto-Tagging** | `AutoTaggingService` correctly identifies "Campfire" attributes (Open strings, low positions). | Input `Open` yielded explanation: *"incorporates beginner-friendly, campfire-chord techniques"* (Step 15348). |
| **Search Retrieval** | `InMemoryVectorIndex` retrieves relevant documents via Cosine Similarity. | Query `ShellVoicing` returned `Dm7 Shell 5th` with Score 0.81 (Step 15348). |
| **Startup Integration** | `Program.cs` successfully instantiates the dependency graph (Orchestrator -> Generators -> Services). | Startup log: `[ModalFlavorService] Loaded 2 modes` (Step 15302). |
| **Generative Realization** | `AdvancedTabSolver` generates ergonomic tab paths using Viterbi algorithm. | Confirmed stable integration of `PhysicalCostService` and `FretboardPositionMapper` (Step 16654). |
| **Advanced Chatbot** | `TabAwareOrchestrator` provides rich insights (Key Drift, Tension, Next Chord). | Integration of `NextChordSuggestionService` and `ModulationAnalyzer` verified (Step 16730). |
| **Data Acquisition** | `ingest-corpus` command builds. `TabCorpusRepository` (Mongo) and `TabSourcesConfig` verified via compilation. | `dotnet build GaCLI` successful (Step 16896). |
| **Hallucination Guards** | LLM Narrator respects grounded facts and avoids inventing theory. | `GaChatbot.Tests` passing (16/16) verifying prompt constraints (Step 16928). |
| **Style Learning** | Prediction of musical style (Jazz, Blues) based on wavelet features. | Verified `StyleClassifierService` and `TabPresentationService` integration (Step 16957). |
| **Harvesting** | Extraction of labeled progressions from Tab Corpus. | Verified `ProgressionHarvestingService` and `IProgressionCorpusRepository` (Step 16963). |
| **Unified Engine** | End-to-end processing (Tab -> Embedding -> Style -> Key -> Suggestion). | Verified `IntelligentAnalysisDemo.cs` (Suggestion: Fmaj7, Sim: 0.71, Cost: 19.00) (Step 17043). |
| **Orchestration** | `ProductionOrchestrator` logic unifying Tab analysis and RAG. | `dotnet build GaChatbot` passed (Step 17102). |
| **End-to-End** | Full Integration Test of Tab -> Analysis and RAG Fallback. | Verified `ProductionOrchestratorTests.cs` (18/18 Passed) (Step 17169). |

## 2. Technical Limitations & Gaps
The following limitations are present in the current codebase state.

### A. Scalability (Critical)
*   **Implementation**: `InMemoryVectorIndex.cs` uses `List<VoicingDocument> _documents` for storage.
*   **Impact**: Search complexity is $O(N)$ with full scan. This will not scale to the target 50M records.
*   **Gap**: No specialized vector database (Qdrant/Milvus) integration.

### B. Persistence (Critical)
*   **Implementation**: `Program.cs` calls `SeedVoicingsAsync` on every startup.
*   **Impact**: Data is ephemeral. Indexing 50M items on startup is infeasible.
*   **Gap**: No `Save/Load` mechanism for the index.

### C. Embedding Schema (Semantic)
*   **Implementation**: `EmbeddingSchema.cs` (v1.3) defines specific ranges for techniques and styles.
*   **Gap**: "Flavor" tags (Lydian, Dorian) are currently handled as *string metadata* in `VoicingDocument.SemanticTags`. They are **not** encoded in the `BitVector` (dimensions 0-63).
*   **Consequence**: We cannot currently perform *vector-based* retrieval for specific modal flavors; retrieval relies on keyword filtering or hybrid search.

### D. Configuration Hardcoding
*   **Implementation**: `ModalFlavorService.cs` contains fallback logic checking multiple relative paths for `Modes.yaml`.
*   **Impact**: Brittle deployment.
*   **Gap**: Lack of robust `IConfiguration` or `IOptions<>` pattern for file assets.

## 3. Deployment Artifacts
*   **Executable**: `Apps/GaChatbot` (Console Application).
*   **Dependencies**: `GA.Business.Core`, `GA.Business.ML`, `GA.Business.Config`.
*   **Data**: `Modes.yaml` (Required for Phase 13 features).
