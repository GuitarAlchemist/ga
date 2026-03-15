---
status: pending
priority: p2
issue_id: "010"
tags: [code-review, architecture, di, chatbot, simplicity]
dependencies: []
---

# P2: ChatbotSessionOrchestrator kept registered after all call sites removed — architectural dead weight

## Problem Statement

Phase 4 removes `ChatbotSessionOrchestrator` from `ChatbotController` and `ChatbotHub` — its only two call sites. The plan keeps it registered in the DI container "for backward compat / potential future use." With no consumer, it is a dead registration that causes developer confusion and silently provides an alternative, worse pipeline path that future developers might accidentally inject.

## Findings

- `ChatbotController` and `ChatbotHub` are the only consumers (confirmed by architecture review)
- Both are updated to inject `ProductionOrchestrator` in Phase 4
- Plan says "Keep `ChatbotSessionOrchestrator` registered (for backward compat / potential future use)" — no specific consumer named
- `ChatbotSessionOrchestrator` is `Scoped` while `ProductionOrchestrator` is `Singleton` — two different orchestrator lifetimes in the same container for the same role
- Note: `ChatbotSessionOrchestrator` carries session context behaviour — see todo 006 for the regression risk

## Proposed Solutions

### Option A: Remove registration entirely in Phase 4 (Recommended)
After updating both call sites, remove the DI registration. If a non-RAG fast path is needed in future, create it as an explicitly named, documented type.
- **Pros**: Clean DI container; no confusion
- **Effort**: Trivial (delete 1 line)
- **Risk**: Low — no consumers after Phase 4

### Option B: Rename to DirectOllamaChatOrchestrator and keep with a comment
Rename to signal it is intentionally different from the agentic path, add XML doc comment explaining when to use it.
- **Pros**: Preserves the non-RAG path explicitly
- **Effort**: Small
- **Risk**: Low

## Recommended Action
*(leave blank for triage)*

## Technical Details
- **Files to update**: `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs` (remove registration)
- **Phase in plan**: Phase 4

## Acceptance Criteria
- [ ] `ChatbotSessionOrchestrator` is not registered in `GaApi`'s DI container after Phase 4
- [ ] OR: it is renamed with explicit documentation of its distinct purpose
- [ ] No code calls `ChatbotSessionOrchestrator` via DI after Phase 4 (confirm with grep)

## Work Log
- 2026-03-03: Flagged by architecture-strategist (P2-B) and code-simplicity-reviewer

## Resources
- Plan: Phase 4, Scope Boundaries
- Source: `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs`
