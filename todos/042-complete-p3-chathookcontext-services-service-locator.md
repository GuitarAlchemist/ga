---
status: complete
priority: p3
issue_id: "042"
tags: [architecture, di, code-quality, code-review]
dependencies: []
---

# 042 — `ChatHookContext.Services` Exposes Service-Locator Anti-Pattern

## Problem Statement

`ChatHookContext.Services` is typed as `IServiceProvider` and passed into every hook invocation. Hooks can resolve any registered service at runtime, making their actual dependencies invisible to the DI container and to static analysis. The current hooks (`ObservabilityHook`, `PromptSanitizationHook`) do not use `Services` — the field was added speculatively.

## Findings

`Common/GA.Business.ML/Agents/Hooks/ChatHookContext.cs` line 34:
```csharp
public IServiceProvider Services { get; init; } = default!;
```

Grep across all hook implementations: zero calls to `ctx.Services.GetService(...)` or `ctx.Services.GetRequiredService(...)`.

## Proposed Solutions

### Option A — Remove `Services` from `ChatHookContext`
If a future hook genuinely needs a dependency, declare it in the hook's constructor so DI wires it properly.
- **Effort:** Trivial — remove one property.
- **Risk:** Low — zero consumers today.

### Option B — Keep but add `[Obsolete]` warning
```csharp
[Obsolete("Declare dependencies in hook constructor; do not use service locator.")]
public IServiceProvider? Services { get; init; }
```
- **Effort:** Trivial.

## Recommended Action
Option A.

## Acceptance Criteria

- [ ] `ChatHookContext` does not expose `IServiceProvider`
- [ ] All existing hook implementations compile without change

## Work Log

- 2026-03-10: Identified during architecture and simplicity review agents for PR #8
