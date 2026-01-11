# Implementation Plan - GaChatbot Rewrite

## Objective
Replace the existing `GuitarAlchemistChatbot` with a new, authoritative `GaChatbot` that relies on the core domain logic (`GA.Business.Core`) and the indexed database (`MongoDB`) instead of hallucinated or hardcoded responses.

## Phase 1: Cleanup & Setup
- [ ] **Delete** the old `Apps/GuitarAlchemistChatbot` project (source of hallucinations).
- [ ] **Create** a new Console Application `Apps/GaChatbot`.
- [ ] **Configure** dependencies:
    - [ ] `GA.Business.Core` (Domain Logic)
    - [ ] `GA.Data.MongoDB` (Data Access)
    - [ ] `Spectre.Console` (CLI UI)
    - [ ] `Microsoft.Extensions.AI` / `OpenAI` (Local LLM Client)
    - [ ] `Microsoft.Extensions.Hosting` (DI Container)

## Phase 2: Core Architecture
- [ ] **Data Service**: Implement `VoicingDataService` to query the `voicings` collection in MongoDB.
    - [ ] Support searching by `SemanticTags`, `Difficulty`, `ChordName`.
    - [ ] Support retrieving "Canonical" voicings (seeded oracle data).
- [ ] **Theory Engine**: Implement `TheoryComputer` service using `GA.Business.Core`.
    - [ ] `AnalyzeChord(symbol)` -> Returns accurate notes/intervals using `ChordSymbolParser`.
    - [ ] `ExpandScale(root, scaleName)` -> Returns scale notes.
- [ ] **Tool Definition**: Define `AITool` wrappers for the above services.
    - [ ] `search_voicings`: Search DB for playable options.
    - [ ] `analyze_chord`: Get theoretical breakdown of a symbol.

## Phase 3: The Chat Loop
- [ ] **Agent Service**: Implement the main chat loop.
    - [ ] **System Prompt**: Strictly instruct the LLM to use tools for *any* factual query.
    - [ ] **Tool Execution**: creating a robust tool dispatcher.
    - [ ] **Context Management**: Keep track of the last discussed chord/voicing for follow-up questions.

## Phase 4: Integration & Verification
- [ ] **Configuration**: Ensure `appsettings.json` points to the correct local MongoDB and Ollama instance.
- [ ] **Smoke Test**: 
    - [ ] "What are the notes in Cmaj7?" -> MUST call `analyze_chord` -> Return `C, E, G, B`.
    - [ ] "Show me a sad jazz chord" -> MUST call `search_voicings(tags=['sad', 'jazz'])` -> Return DB results.

