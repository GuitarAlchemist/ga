---
status: complete
priority: p1
issue_id: "032"
tags: [concurrency, observability, hooks, code-review]
dependencies: []
---

# 032 — `ObservabilityHook` Singleton Race Condition — Concurrent Same-Skill Requests Corrupt Traces

## Problem Statement

`ObservabilityHook` is registered as a singleton by `GaPlugin`, but stores in-flight `Activity` objects in a `ConcurrentDictionary` keyed by `MatchedSkillName` (a plain string). Two concurrent requests that match the same skill (e.g., two simultaneous `KeyIdentification` queries) will collide: the second `OnBeforeSkill` overwrites the first entry, the first `OnAfterSkill` disposes the wrong activity, and the original request's span is orphaned and never closed. Under any load where the same skill fires concurrently this produces corrupted distributed traces that are difficult to diagnose retroactively.

## Findings

`Common/GA.Business.ML/Agents/Hooks/ObservabilityHook.cs` lines 13, 19–38:
```csharp
private readonly ConcurrentDictionary<string, Activity?> _activities = new();

public Task<HookResult> OnBeforeSkillAsync(ChatHookContext ctx, ...)
{
    var activity = _source.StartActivity(ctx.MatchedSkillName!);
    _activities[ctx.MatchedSkillName!] = activity;  // ← overwrites on collision
    ...
}

public Task<HookResult> OnAfterSkillAsync(ChatHookContext ctx, ...)
{
    if (_activities.TryRemove(ctx.MatchedSkillName!, out var activity))
        activity?.Stop();  // ← may stop wrong request's span
    ...
}
```

`GaPlugin` registers as singleton (line 37 of `GaPlugin.cs`). `ProductionOrchestrator` injects `IEnumerable<IChatHook>` — each hook runs on every request using the same instance.

## Proposed Solutions

### Option A — Key by per-request correlation ID (Recommended)
Add a `CorrelationId` (Guid) to `ChatHookContext` set by `ProductionOrchestrator` at the start of each `AnswerAsync` call. Key `_activities` by that ID:
```csharp
_activities[ctx.CorrelationId] = activity;
```
- **Effort:** Small — add one property to `ChatHookContext`, set it once in orchestrator.
- **Risk:** Low — purely additive.

### Option B — Register `ObservabilityHook` as Scoped
Change registration to `Scoped` so each request gets its own hook instance with its own state. Requires resolving hooks from `IServiceScope` inside `ProductionOrchestrator`.
- **Effort:** Medium — changes hook resolution pattern.

### Option C — Inline activity into `ProductionOrchestrator`
Remove `ObservabilityHook` and inline the OpenTelemetry activity tagging directly in the orchestrator, where per-request state is naturally scoped.
- **Effort:** Small — deletes a class, adds ~10 lines to orchestrator.

## Recommended Action
Option A — minimal change, preserves the hook abstraction.

## Acceptance Criteria

- [ ] Two concurrent requests hitting the same skill produce two separate, complete, non-overlapping trace spans
- [ ] No `Activity` is ever orphaned (left open without a corresponding `Stop()`)
- [ ] Unit test: invoke `OnBeforeSkill` twice with the same skill name, verify both activities are independently stopped

## Work Log

- 2026-03-10: Identified during parallel performance and architecture review agents for PR #8
