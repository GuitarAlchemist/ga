---
status: complete
priority: p2
issue_id: "039"
tags: [architecture, hooks, correctness, code-review]
dependencies: []
---

# 039 — `PromptSanitizationHook` Mutates `ChatHookContext` Directly, Bypassing `HookResult.Mutate`

## Problem Statement

The `IChatHook` contract specifies that message mutations should be signalled by returning `HookResult.Mutate(newMessage)`, which `ProductionOrchestrator` then applies to `hookCtx.CurrentMessage`. Instead, `PromptSanitizationHook` writes directly to `ctx.CurrentMessage` and returns `HookResult.Continue` with a null `MutatedMessage`.

This works today only because `ChatHookContext` is a shared reference type. Any future hook ordering change or context-clone optimisation will silently lose the sanitization. It also makes the hook impossible to test in isolation (the side effect lives on a shared object, not the return value).

## Findings

`Common/GA.Business.ML/Agents/Hooks/PromptSanitizationHook.cs` line 43:
```csharp
ctx.CurrentMessage = normalized;   // ← mutates shared context directly
return HookResult.Continue;        // ← MutatedMessage is null
```

`ProductionOrchestrator` propagation loop (lines 52–53):
```csharp
if (hookResult.MutatedMessage is not null)
    hookCtx.CurrentMessage = hookResult.MutatedMessage;  // ← never reached
```

Intended usage per `HookResult`:
```csharp
return HookResult.Mutate(normalized);
```

## Proposed Solutions

### Option A — Return `HookResult.Mutate(normalized)` (Recommended)
```csharp
var normalized = SanitizeAndNormalize(ctx.CurrentMessage);
if (normalized == ctx.CurrentMessage)
    return HookResult.Continue;
return HookResult.Mutate(normalized);
```
- **Effort:** Trivial — 2-line change.
- **Risk:** Zero if `ProductionOrchestrator`'s propagation loop is correct.

### Option B — Document the direct mutation as intentional
Add a comment explaining the side-effect pattern.
- **Cons:** Leaves a correctness landmine for future refactors.

## Recommended Action
Option A.

## Acceptance Criteria

- [ ] `PromptSanitizationHook.OnBeforeSkillAsync` does not write to `ctx.CurrentMessage` directly
- [ ] Returns `HookResult.Mutate(sanitized)` when the message was changed
- [ ] Returns `HookResult.Continue` when no change was needed
- [ ] Unit test: verifies the mutated message propagates correctly through the orchestrator

## Work Log

- 2026-03-10: Identified during architecture review agent for PR #8
