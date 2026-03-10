---
status: complete
priority: p3
issue_id: "030"
tags: [code-quality, dead-code, code-review]
dependencies: []
---

# 030 — `ChatPluginHost.CollectMcpToolTypes()` Is Dead Public Code

## Problem Statement

`ChatPluginHost.CollectMcpToolTypes()` is a public static method that re-scans all loaded assemblies to collect MCP tool types. This exact collection is already done inside `AddChatPluginHost()` — the results are captured in `capturedToolTypes` and used to register `IMcpToolsProvider`. The public method duplicates the scan for no consumer.

## Findings

`Common/GA.Business.ML/Agents/Plugins/ChatPluginHost.cs` lines 90–113:
```csharp
public static IReadOnlyList<Type> CollectMcpToolTypes()
    => AppDomain.CurrentDomain
        .GetAssemblies()
        // ... identical scan as AddChatPluginHost ...
        .SelectMany(pluginType => {
            var plugin = (IChatPlugin)Activator.CreateInstance(pluginType)!;
            return plugin.McpToolTypes;
        })
        // ...
```

Grep across the solution shows zero callers of `CollectMcpToolTypes()` outside of itself.

The method also instantiates every `IChatPlugin` a second time (after `AddChatPluginHost` already did so), doubling startup costs for plugin instantiation.

## Proposed Solutions

### Option A — Delete the method
If no callers exist, remove it entirely.
- **Pros:** Removes dead code, eliminates duplicate assembly scan.
- **Effort:** Trivial.
- **Risk:** Low — verify with `grep -rn "CollectMcpToolTypes"` first.

### Option B — Make it `internal`
If it's a test seam, mark `internal` and add `[PublicAPI]` if needed.
- **Effort:** Trivial.

## Recommended Action
Option A — delete after confirming no callers.

## Acceptance Criteria

- [ ] `grep -rn "CollectMcpToolTypes"` returns zero results (or only the definition)
- [ ] `ChatPluginHost` no longer has `CollectMcpToolTypes()`
- [ ] `AddChatPluginHost` behavior is unchanged

## Work Log

- 2026-03-10: Identified during `/ce:review` of PR #8
