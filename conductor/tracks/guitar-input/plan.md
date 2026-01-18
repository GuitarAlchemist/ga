# Implementation Plan - Guitar Input

## Phase 1: Research & Scaffolding
- [x] 1.1 Spike: Survey common ASCII tab formats and edge cases.
- [x] 1.2 Create `GA.Business.ML.Tabs` project or namespace.
- [x] 1.3 Define data models: `TabBlock`, `TabSlice`, `TabNote`.

## Phase 2: Core Parser Implementation
- [x] 2.1 Implement `TabTokenizer` (regex or state-machine based).
- [x] 2.2 Implement `TabToPitchConverter` (with tuning support).
- [x] 2.3 Unit tests for basic chord shapes (G Major, Power chords).

## Phase 3: Integration with Embedding System
- [x] 3.1 Implement `TabAnalysisService` to wrap the parser and embedding generator.
- [x] 3.2 Add logic to aggregate time slices into logical "chord events".
- [x] 3.3 Verify OPTIC-K generation for parsed tabs.

## Phase 4: Chatbot Integration
- [x] 4.1 Update `GaChatbot` to detect tab-like input (intent detection).
- [x] 4.2 Implement "Analyze this riff" command/handler.
- [x] 4.3 Format analysis results for the user (Narrator).

## Phase 5: Verification
- [ ] Manual test: Paste a "Smoke on the Water" riff and verify correct chord detection.
- [ ] Manual test: Verify key detection for a simple progression.
