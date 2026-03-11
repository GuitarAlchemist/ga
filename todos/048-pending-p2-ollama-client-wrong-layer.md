---
status: pending
priority: p2
issue_id: "048"
tags: [code-review, architecture, layering, ollama]
---

# OllamaGenerateClient in Wrong Architectural Layer

## Problem Statement
`OllamaGenerateClient` lives in `GA.Business.Core.Orchestration` (Layer 5 — Orchestration) but it is pure LLM infrastructure with no orchestration logic. Per the five-layer dependency model, LLM client infrastructure belongs in `GA.Business.ML` (Layer 4 — AI/ML). Having it in Layer 5 prevents Layer 4 agents from depending on it without creating a circular reference.

## Proposed Solution
- Move `OllamaGenerateClient` to `Common/GA.Business.ML/Clients/OllamaGenerateClient.cs`
- Update the DI registration in `ChatbotOrchestrationExtensions` to reference the new location
- Verify no Layer 5 types leak into the moved file

**Files:**
- `Common/GA.Business.Core.Orchestration/Clients/OllamaGenerateClient.cs` (source — to be removed)
- `Common/GA.Business.ML/Clients/OllamaGenerateClient.cs` (destination — to be created)
- `Common/GA.Business.Core.Orchestration/Extensions/ChatbotOrchestrationExtensions.cs` (registration update)

## Acceptance Criteria
- [ ] `OllamaGenerateClient` resides in `GA.Business.ML`, not `GA.Business.Core.Orchestration`
- [ ] No file in Layer 4 or below references a Layer 5 type
- [ ] DI registration updated and verified by integration test or build
- [ ] Solution builds with zero warnings after the move
- [ ] Existing unit/integration tests for `OllamaGenerateClient` still pass
