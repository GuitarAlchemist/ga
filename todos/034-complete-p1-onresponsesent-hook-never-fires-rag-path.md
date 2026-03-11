---
status: complete
priority: p1
issue_id: "034"
tags: [hooks, correctness, observability, code-review]
dependencies: []
---

# 034 — `OnResponseSent` Hook Never Fires for RAG / Tab Path

## Problem Statement

`ProductionOrchestrator.AnswerAsync` calls `OnResponseSent` correctly when a skill short-circuits (fast path), but the RAG and tab branches return without ever invoking `OnResponseSent`. Any hook that writes session memory, emits analytics events, or updates context windows will silently fail for the majority of production traffic — the skill fast-path handles only deterministic queries; RAG/tab is the normal case.

This is a correctness bug masquerading as a design omission.

## Findings

`Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs` lines 129–172:
```csharp
// Skill short-circuit path — OnResponseSent fires ✓
if (skill is not null)
{
    ...
    await RunHooksAfterAsync(hooks, afterCtx, ct);
    await RunHooksSentAsync(hooks, sentCtx, ct);   // ← fires
    return response;
}

// RAG path — OnResponseSent NEVER fires ✗
var ragResponse = await _narrator.NarrateAsync(...);
return ragResponse;  // ← returns directly, skips sent hook

// Tab path — OnResponseSent NEVER fires ✗
var tabResponse = await _tabAgent.ProcessAsync(...);
return tabResponse;  // ← returns directly, skips sent hook
```

## Proposed Solutions

### Option A — Extract to single exit point (Recommended)
Refactor `AnswerAsync` to have one exit point that always dispatches `OnResponseSent`:
```csharp
AgentResponse result;
if (skill is not null)
    result = await RunSkillAsync(skill, hooks, request, ct);
else if (IsTabQuery(request))
    result = await _tabAgent.ProcessAsync(request, ct);
else
    result = await _narrator.NarrateAsync(request, ct);

await RunHooksSentAsync(hooks, new ChatHookContext { Response = result, ... }, ct);
return result;
```
- **Effort:** Small — refactor existing method body.
- **Risk:** Low — hooks are currently no-ops on RAG path so behaviour is additive.

### Option B — Add `OnResponseSent` call before each `return` statement
Inline the hook call at each return site.
- **Effort:** Trivial — 2 additional `await` calls.
- **Risk:** Low; slightly more fragile if additional paths are added later.

## Recommended Action
Option A — prevents future paths from missing the hook by accident.

## Acceptance Criteria

- [ ] `OnResponseSent` fires for every response path: skill, RAG, and tab
- [ ] Integration test: assert that `ObservabilityHook.OnAfterSent` is invoked for a request that hits the RAG path
- [ ] No existing test regressions

## Work Log

- 2026-03-10: Identified during architecture review agent for PR #8
