### Mock and Fake Components Investigation Report

This report identifies all mocked, faked, or stubbed components within the application that are candidates for replacement with real implementations.

#### 1. Mocking Frameworks (Test Environments)
These are standard industry tools used for unit testing and are generally acceptable in test projects, but listed here for completeness.
*   **Moq (C#)**: Extensively used across all C# test projects in the `Tests/` directory.
*   **Vitest (TypeScript)**: Used for testing in `Apps/ga-dashboard`.

#### 2. Manual Fakes & Stubs in Test Projects
Hardcoded implementations within test projects that bypass real logic.
*   **`OnnxEmbeddingServiceTests.cs`**: Contains `FakeOnnxSession`, `FakeOnnxSessionFactory`, and `FakeResultCollection` to avoid loading real ONNX models during tests.

#### 3. In-place Mocks & Stubs in Production Code (Backend)
These are critical "to-be-banished" items as they reside in production assemblies and return synthetic data.

**Controllers (ga-server)**
*   **`ChordProgressionsController.cs`**: 
    *   Generates mock shape IDs (`shape-{i}-{Guid}`) when none are provided.
    *   Creates mock `DynamicalSystemInfo` for progression analysis.
*   **`EnhancedPersonalizationController.cs`**: 
    *   Uses mock performance data for demonstration.
    *   Uses mock learning path IDs.
    *   Mock difficulty calibration based on historical performance.
*   **`GuitarPlayingController.cs`**: 
    *   Creates mock positions for guitar mapping.
*   **`AdvancedAnalyticsController.cs`**: 
    *   Creates mock user profiles for analytics.
*   **`GrothendieckController.cs`**: 
    *   Mock conversion for responses.

**Services & Business Logic**
*   **`MockHuggingFaceClient.cs`**: A complete mock implementation of `HuggingFaceClient` that generates synthetic WAV audio files locally instead of calling the Hugging Face API.
*   **`AIServices.cs`**: 
    *   `PredictNextShapes`: Returns hardcoded predicted shapes with random probabilities.
    *   `GetTransitionMatrix`: Returns a mock 4x4 transition matrix with random values.
*   **`ActorSystemManager.cs`**: Returns "Mock response" for actor system queries.
*   **`VectorSearchServices.cs`**: Returns mock success and mock embedding vectors.
*   **`StyleClassifierService.cs`**: Uses "PerformHeuristicInference" as a mock for a real ML/ONNX multiclass classifier.
*   **`ContentDiscovery.fs`**: Contains hardcoded `mockResults` for external repositories:
    *   Ultimate Guitar
    *   Songsterr
    *   MuseScore
    *   IMSLP
*   **`BiomechanicalAnalyzer.cs`**: Contains hardcoded stubs for physical constraints and complexity calculations.
*   **`FretboardChordAnalyzerExtensions.cs`**: Contains stubs for advanced chord analysis logic.

#### 4. Mock Data (Frontend)
Static data files used to simulate backend responses in UI development.
*   **`ga-fretboard-app/src/data/mockData.ts`**: Contains over 100 lines of hardcoded fretboard positions for:
    *   Chords (C Major, G Major)
    *   Scales (C Major, A Minor Pentatonic)
    *   Modes (D Dorian)
    *   Arpeggios (C Major, E Minor)
*   **`ga-dashboard/src/app/embedding-viewer/embedding-viewer.component.ts`**: Contains placeholders like `MOCK_VOICINGS` (some already deleted per policy).
*   **`ga-dashboard/src/app/benchmark-detail/benchmark-detail.component.ts`**: Contains placeholders for deleted mock data.

#### 5. Recommendations for "Banishment"
1.  **Prioritize Controllers**: Replace random/GUID-based mocks in `ga-server` controllers with real database or service calls.
2.  **ML/AI Integration**: Replace `MockHuggingFaceClient` and `StyleClassifierService` heuristic logic with real ONNX model inference or API calls.
3.  **Content Discovery**: Implement real scrapers or API integrations for the repositories listed in `ContentDiscovery.fs`.
4.  **Frontend Data**: Switch `ga-fretboard-app` to fetch data from the `ga-server` API instead of using `mockData.ts`.
