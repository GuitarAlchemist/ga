---
status: complete
priority: p2
issue_id: "038"
tags: [performance, json, allocation, code-review]
dependencies: []
---

# 038 — `JsonSerializerOptions` Allocated Per LLM Response Parse Call

## Problem Statement

`GuitarAlchemistAgentBase.ParseStructuredResponse` and `SemanticRouter.LlmRouteAsync` each construct `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` inside the method body. `JsonSerializerOptions` is expensive to construct: it builds an internal reflection cache. Creating one per LLM response call means the cache is thrown away each time, causing repeated heap allocations and prolonged GC pressure.

This is a well-known .NET performance anti-pattern with a trivial fix.

## Findings

`Common/GA.Business.ML/Agents/GuitarAlchemistAgentBase.cs` line ~162:
```csharp
var structured = JsonSerializer.Deserialize<StructuredAgentResponse>(json,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
```

`Common/GA.Business.ML/Agents/SemanticRouter.cs` line ~136:
```csharp
var route = JsonSerializer.Deserialize<RoutingResponse>(json,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
```

Both called on every LLM response that goes through structured parsing or semantic routing.

## Proposed Solutions

### Option A — `static readonly` field (Recommended)
```csharp
private static readonly JsonSerializerOptions _jsonOptions =
    new() { PropertyNameCaseInsensitive = true };
```
Reuse across all calls. The options object is thread-safe once constructed.
- **Effort:** Trivial — 2 lines changed per location.
- **Risk:** Zero.

### Option B — Source-generated JSON context
Use `[JsonSerializable]` + `JsonSerializerContext` for zero-allocation deserialization.
- **Effort:** Small — more invasive.
- **Risk:** Low.

## Recommended Action
Option A — immediate fix, zero risk.

## Acceptance Criteria

- [ ] No `new JsonSerializerOptions(...)` inside any method body in `GuitarAlchemistAgentBase` or `SemanticRouter`
- [ ] `static readonly` `JsonSerializerOptions` fields exist at class level
- [ ] All existing tests pass

## Work Log

- 2026-03-10: Identified during performance review agent for PR #8
