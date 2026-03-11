---
status: complete
priority: p3
issue_id: "024"
tags: [quality, code-review]
dependencies: []
---

# 024 — Minor SSE Helper Duplication and Correctness Issues in ChatbotController

## Problem Statement
Five small independent issues in `ChatbotController.cs`, `Program.cs`, and `GaDslTool.cs` that each have targeted one- or two-line fixes. None is a blocker on its own, but together they represent dead code, a false XML doc claim, and an inconsistent error contract.

## Findings

**Issue 1 — WriteSseErrorAsync duplication**
`WriteSseErrorAsync` at `ChatbotController.cs:148` reimplements the `WriteAsync` + `FlushAsync` pattern inline instead of delegating to `WriteSseLineAsync` after constructing the payload. This creates two divergent code paths for the same operation.

**Issue 2 — Redundant ThrowIfCancellationRequested**
`cancellationToken.ThrowIfCancellationRequested()` at `ChatbotController.cs:81` is dead: `WriteSseLineAsync` already passes `ct` to `WriteAsync`, which throws `OperationCanceledException` on cancellation. The explicit call adds noise.

**Issue 3 — Duplicate /stats route**
`Program.cs:173` registers `/api/stats` and `Program.cs:174` registers `/stats` with no comment explaining the backwards-compatibility reason. Without a comment, the duplication looks like a mistake and will confuse future maintainers.

**Issue 4 — False XML doc on SplitIntoChunks**
`SplitIntoChunks` XML doc at `ChatbotController.cs:157` states "Keeps chunks ≤ 200 characters", but the implementation has no character limit — long sentences emit as single unbounded chunks. The doc is misleading.

**Issue 5 — Missing try/catch in GaDslTool.InvokeJsonAsync**
`GaDslTool.InvokeJsonAsync` lacks the `try/catch` that `GaDslTool.InvokeAsync` has, resulting in different error contracts on two parallel code paths. Callers of `InvokeJsonAsync` receive unhandled exceptions where `InvokeAsync` callers receive structured error results.

## Proposed Solutions
1. Refactor `WriteSseErrorAsync` to build the payload string and delegate to `WriteSseLineAsync`.
2. Remove the redundant `ThrowIfCancellationRequested()` call at line 81.
3. Add an inline comment on the `/stats` route explaining the backwards-compat rationale (or remove the duplicate if it is no longer needed).
4. Either enforce the 200-character limit in `SplitIntoChunks` or correct the XML doc to accurately describe the current behavior.
5. Wrap `InvokeJsonAsync` in the same `try/catch` pattern used by `InvokeAsync`.

## Recommended Action

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` (lines 81, 148, 157)
  - `Apps/ga-server/GaApi/Program.cs` (lines 173–174)
  - `GaMcpServer/Tools/GaDslTool.cs` (`InvokeJsonAsync`)

## Acceptance Criteria
- [ ] `WriteSseErrorAsync` delegates to `WriteSseLineAsync` with no duplicated flush logic.
- [ ] Redundant `ThrowIfCancellationRequested()` call removed.
- [ ] `/stats` duplicate route has an explanatory comment or is removed.
- [ ] `SplitIntoChunks` XML doc matches actual behavior (enforce limit or correct the claim).
- [ ] `GaDslTool.InvokeJsonAsync` has equivalent error handling to `InvokeAsync`.
- [ ] Build and all tests pass.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
