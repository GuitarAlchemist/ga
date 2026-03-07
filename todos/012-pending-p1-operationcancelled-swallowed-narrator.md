---
status: pending
priority: p1
issue_id: "012"
tags: [quality, rop, code-review]
dependencies: []
---

# 012 — `OperationCanceledException` Swallowed in Narrator + Zero-Warnings Violations

## Problem Statement
`OllamaGroundedNarrator.cs:37` has a bare `catch (Exception ex)` block that converts `OperationCanceledException` (client disconnect, request timeout, cancellation token signaled) into a formatted fallback response. Cancellation must propagate so that callers can release resources correctly and upstream infrastructure (ASP.NET Core, SignalR, Aspire) can observe the cancellation. Additionally, several files in the same area violate the zero-warnings policy (collection expressions).

## Findings
- `Common/GA.Business.Core.Orchestration/Services/OllamaGroundedNarrator.cs:37`: `catch (Exception ex)` catches and swallows `OperationCanceledException`, returning a fallback string instead of propagating cancellation.
- Downstream effects: SignalR hub and streaming controller cannot detect client disconnects; Ollama HTTP request may continue running after the client has gone.
- Additional zero-warnings violations (bundled for efficiency):
  - `ClaudeChatService.cs:78` — `new List<MessageParam>()` should be `[]`
  - `ChatbotController.cs:126` — `new List<string> {` should be `[`
  - `OllamaGroundedNarrator.cs:75` — `new List<string>` should be `[]`
  - `ChatbotSessionOrchestrator.cs:21–25` — primary constructor parameters redundantly re-assigned to 5 explicit backing fields (should use primary constructor capture directly)

## Proposed Solutions
### Option A — Re-throw before general catch (recommended)
Add `catch (OperationCanceledException) { throw; }` immediately before the `catch (Exception ex)` block in `OllamaGroundedNarrator.cs`. In the same pass, apply the collection-expression and primary-constructor fixes listed above.

**Pros:** Minimal, surgical change; correct propagation of cancellation; clears all zero-warnings violations in affected files.
**Cons:** None.
**Effort:** Small
**Risk:** Low

### Option B — Convert to ROP pattern
Replace the try/catch in `OllamaGroundedNarrator` with a `Try<T>` or `Result<T, TError>` return type per the ROP Patterns skill, surfacing cancellation as a distinct error discriminant.

**Pros:** Consistent with the broader ROP architecture mandate; explicit cancellation state at all call sites.
**Cons:** Requires updating all callers of `IGroundedNarrator`; larger diff.
**Effort:** Medium
**Risk:** Low

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `Common/GA.Business.Core.Orchestration/Services/OllamaGroundedNarrator.cs:37, 75`
  - `Apps/ga-server/GaApi/Services/ClaudeChatService.cs:78`
  - `Apps/ga-server/GaApi/Controllers/ChatbotController.cs:126`
  - `Apps/GaChatbot/Services/ChatbotSessionOrchestrator.cs:21–25`
- **Components:** `OllamaGroundedNarrator`, `ClaudeChatService`, `ChatbotController`, `ChatbotSessionOrchestrator`

## Acceptance Criteria
- [ ] `OperationCanceledException` thrown during narrator execution propagates to the caller; no fallback string is returned for cancellations.
- [ ] All collection literal violations replaced with `[]` collection expressions.
- [ ] `ChatbotSessionOrchestrator` primary constructor no longer has redundant backing field assignments.
- [ ] Zero warnings in all touched files.
- [ ] Existing cancellation-related tests (if any) pass; add a test asserting cancellation propagation through `OllamaGroundedNarrator`.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
