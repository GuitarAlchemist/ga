---
status: pending
priority: p1
issue_id: "006"
tags: [code-review, architecture, chatbot, regression, session]
dependencies: []
---

# P1: ChatbotSessionOrchestrator session context silently dropped — unacknowledged feature regression

## Problem Statement

`ChatbotSessionOrchestrator` carries behaviour that `ProductionOrchestrator` does not replicate: `ISessionContextProvider` (user tuning/key/skill level/genre/fret range), `ChatbotOptions.HistoryLimit`, conversation history normalization, and semantic knowledge search. The plan switches `ChatbotController` and `ChatbotHub` to use `ProductionOrchestrator` without acknowledging what is lost. This is an unacknowledged feature regression that will surface as a user-facing behaviour change.

## Findings

- `ChatbotSessionOrchestrator` injects `ISessionContextProvider` and uses session context to build system prompts (confirmed by simplicity reviewer reading the source)
- `ChatbotOptions.HistoryLimit` controls conversation truncation — `ProductionOrchestrator.AnswerAsync` takes a raw `ChatRequest` with `IReadOnlyList<ChatMessage>? ConversationHistory = null` but does nothing special with history limits
- `NormalizeHistory` method in `ChatbotSessionOrchestrator` (12 lines of LINQ) truncates history before sending to Ollama — not ported to `ProductionOrchestrator`
- Plan says "conversation history moves to the caller (controller/hub)" but the hub's `ConcurrentDictionary<connectionId, List<ChatMessage>>` accumulates history without ever trimming it
- The plan keeps `ChatbotSessionOrchestrator` registered "for backward compat" but has no documented consumer after Phase 4 and does not mention what to do with its unique capabilities

## Proposed Solutions

### Option A: Port session context injection into ProductionOrchestrator (Recommended)
Add `ISessionContextProvider` injection to `ProductionOrchestrator`. Before calling `SemanticRouter.RouteAsync`, enrich the `ChatRequest` system prompt with session context (key, skill level, genre). Port `NormalizeHistory` as a static helper. Register `ISessionContextProvider` in `AddChatbotOrchestration()`.
- **Pros**: No regression; full feature parity
- **Cons**: Adds scope to the milestone
- **Effort**: Medium (3-4h)
- **Risk**: Low — context enrichment is additive

### Option B: Explicitly accept regression, document in PR
Make a conscious decision to drop session context for this milestone. Document in the PR description: "Session-aware tuning (key, genre, skill level) is not applied to GaApi responses in this milestone. Follow-up: [link issue]."
- **Pros**: Keeps milestone scope bounded
- **Cons**: Users on the React frontend will no longer receive session-personalized responses
- **Effort**: None (documentation only)
- **Risk**: Medium — visible UX regression

### Option C: Keep ChatbotSessionOrchestrator as a middleware wrapper
Use `ChatbotSessionOrchestrator` to enrich the `ChatRequest` with session context, then delegate to `ProductionOrchestrator.AnswerAsync` for routing and RAG.
- **Pros**: No code regression; clear responsibility split
- **Cons**: Maintains two orchestrators as part of the call chain
- **Effort**: Small (2h)
- **Risk**: Low

## Recommended Action
**Option B — Accept regression for this milestone.** History normalization (`NormalizeHistory`) is preserved in `ChatbotHub` via `sessionOrchestrator`. Session personalization (key, genre, skill level) is dropped. This is a known UX regression to be addressed in a follow-up. Document in PR description.

## Technical Details
- **Affected files**: `Apps/GaChatbot/Services/ChatbotSessionOrchestrator.cs` (understand what to port), `Apps/GaChatbot/Services/ProductionOrchestrator.cs` (target for porting)
- **Phase in plan**: Must be decided before Phase 4 ships

## Acceptance Criteria
- [ ] Explicit decision documented: session context is either ported or explicitly dropped with a linked follow-up issue
- [ ] If ported: `ProductionOrchestrator` applies `ISessionContextProvider` data to system prompt
- [ ] If dropped: PR description lists the regression and links a follow-up task
- [ ] `NormalizeHistory` logic (history truncation) is preserved in the new orchestrator path

## Work Log
- 2026-03-03: Identified by code-simplicity-reviewer and architecture-strategist

## Resources
- Plan: `docs/plans/2026-03-02-refactor-chatbot-orchestration-extraction-plan.md` Phase 4
- Source: `Apps/GaChatbot/Services/ChatbotSessionOrchestrator.cs`
