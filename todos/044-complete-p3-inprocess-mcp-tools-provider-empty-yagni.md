---
status: complete
priority: p3
issue_id: "044"
tags: [code-quality, yagni, mcp, code-review]
dependencies: ["030"]
---

# 044 — `InProcessMcpToolsProvider` Registered with Empty Type List (YAGNI)

## Problem Statement

`ChatPluginHost.AddChatPluginHost` registers `IMcpToolsProvider` / `InProcessMcpToolsProvider` as a singleton at startup. However, all current `IChatPlugin.McpToolTypes` implementations return `[]` (`GaPlugin` line 46, `SkillMdPlugin` line 59). The resulting provider is registered and allocates at startup but serves no tools — it is Phase 3 scaffolding shipped prematurely.

Every startup allocates the provider and its internal collections for zero benefit.

## Findings

`Common/GA.Business.ML/Agents/Plugins/ChatPluginHost.cs` lines 74–82:
```csharp
// capturedToolTypes is always empty because all plugins return []
services.AddSingleton<IMcpToolsProvider>(
    new InProcessMcpToolsProvider(capturedToolTypes));
```

`Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs` line 46:
```csharp
public IReadOnlyList<Type> McpToolTypes => [];  // populated in Phase 3
```

## Proposed Solutions

### Option A — Skip registration until Phase 3 arrives
```csharp
if (capturedToolTypes.Count > 0)
    services.AddSingleton<IMcpToolsProvider>(new InProcessMcpToolsProvider(capturedToolTypes));
```
- **Effort:** Trivial.
- **Risk:** Any code that resolves `IMcpToolsProvider` expecting non-null must handle null/missing registration — verify no callers exist yet.

### Option B — Remove registration entirely, re-add in Phase 3 PR
Delete the registration block now; add it back when `McpToolTypes` returns actual types.
- **Effort:** Trivial.

### Option C — Keep as-is (accept YAGNI)
Zero runtime impact; minor conceptual noise.

## Recommended Action
Option B — keep the codebase honest; Phase 3 can re-add cleanly.

## Acceptance Criteria

- [ ] No `IMcpToolsProvider` singleton registered until at least one plugin returns non-empty `McpToolTypes`
- [ ] `GaPlugin.McpToolTypes` override removed (defaults to interface `[]`)
- [ ] Build and tests pass

## Work Log

- 2026-03-10: Identified during code simplicity review agent for PR #8
