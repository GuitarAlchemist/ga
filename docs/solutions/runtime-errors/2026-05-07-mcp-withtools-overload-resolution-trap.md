---
title: "MCP `WithTools(IEnumerable<Type>)` silently registers Type instances as tool objects"
date: 2026-05-07
problem_type: "runtime-errors"
component: "ModelContextProtocol .NET / GA.Business.ML.Agents.Plugins"
symptoms:
  - "MCP server boots cleanly, `initialize` round-trip succeeds"
  - "`tools/list` returns JSON-RPC `-32601 Method 'tools/list' is not available`"
  - "Server `capabilities` advertise `logging` only — no `tools` capability"
  - "InProcessMcpToolsProvider.GetToolsAsync throws `McpProtocolException: Request failed (remote): Method 'tools/list' is not available`"
  - "Every SkillMdDrivenSkill call 500s with the same exception cached forever (until restart)"
  - "Behaviour identical for `app.MapMcp('/mcp')` HTTP transport AND in-process Pipe transport"
tags:
  - "mcp"
  - "model-context-protocol"
  - "csharp-overload-resolution"
  - "WithTools"
  - "AIFunctionFactory"
  - "chatbot"
severity: "critical"
related_docs:
  - "docs/contracts/2026-05-06-ga-dsl-eval-contract.md"
related_prs:
  - "#150 (closed — IChatClientFactory fix that made this latent bug surface)"
  - "#151 (the comprehensive fix)"
---

# MCP `WithTools(IEnumerable<Type>)` overload resolution silently registers Type instances as tool objects

## Symptoms

The MCP server appears to boot fine. Initialize handshake succeeds. The first time anything calls `tools/list`, the server returns `-32601 Method 'tools/list' is not available`. The server's `capabilities` advertise `logging` only — no `tools` capability.

In our case, `InProcessMcpToolsProvider.StartInProcessServerAsync` called `_client.ListToolsAsync(ct)` after building the in-process server, threw `McpProtocolException`, and `Lazy<T,ExecutionAndPublication>` cached the exception. Every subsequent `SkillMdDrivenSkill.ExecuteAsync` re-fired the same exception, was caught by the skill's swallow handler, and returned the generic *"I encountered an error processing your request"* with `Confidence = 0` — chatbot looked broken to every user, original exception logged once per request and buried.

The exact same shape appeared on the MCP-over-HTTP path when we wrote:

```csharp
foreach (var toolType in toolTypes)
    mcpBuilder.WithTools(toolType);
```

…and again when we tried:

```csharp
mcpBuilder.WithTools(toolTypes);  // toolTypes is IReadOnlyList<Type>
```

Both compiled. Both ran. Neither worked.

## Root cause: C# overload resolution picks the wrong `WithTools<T>` overload

`Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions` exposes (relevant overloads, MCP 1.1.0):

```
WithTools<T>(IMcpServerBuilder, JsonSerializerOptions)                      // generic, by type
WithTools<T>(IMcpServerBuilder, T, JsonSerializerOptions)                   // generic, by INSTANCE
WithTools(IMcpServerBuilder, IEnumerable<McpServerTool>)                    // by tool instance enumerable
WithTools(IMcpServerBuilder, IEnumerable<Type>, JsonSerializerOptions)      // by Type enumerable
WithToolsFromAssembly(IMcpServerBuilder, Assembly, JsonSerializerOptions)   // assembly scan
```

When you write `mcpBuilder.WithTools(toolType)` with `toolType : Type`, the compiler resolves to `WithTools<T>(builder, T instance)` with `T = Type` — i.e. it registers `typeof(ChordMcpTools)`, `typeof(IntervalMcpTools)`, … as **tool *objects***. They are not McpServerTool instances, but the overload resolution doesn't enforce that — the runtime just records `Type`-as-tool with no `tools/list` handler.

When you fix that to `mcpBuilder.WithTools(toolTypes)` with `toolTypes : IReadOnlyList<Type>`, the **plural** overload IS what the doc comment promises ("walks each type's `[McpServerTool]` methods"). In MCP 1.1.0 it still didn't wire the `tools/list` handler in our in-process configuration — at minimum on Streamable HTTP it does work, but only when invoked from the AspNetCore `MapMcp` registration path.

## Fix — two paths

### In-process (chatbot's IMcpToolsProvider)

Skip the MCP round-trip entirely. The whole point is producing `IReadOnlyList<AIFunction>` for `ChatOptions.Tools`. Build them directly via reflection over `[McpServerTool]`-decorated methods:

```csharp
foreach (var toolType in toolTypes)
{
    object? lazyTarget = null;
    object GetTarget() => lazyTarget ??= ActivatorUtilities.CreateInstance(hostServices, toolType);

    foreach (var method in toolType.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static))
    {
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        if (toolAttr is null) continue;

        var descAttr    = method.GetCustomAttribute<DescriptionAttribute>();
        var name        = !string.IsNullOrEmpty(toolAttr.Name) ? toolAttr.Name : method.Name;
        var description = descAttr?.Description ?? string.Empty;
        var target      = method.IsStatic ? null : GetTarget();
        functions.Add(AIFunctionFactory.Create(method, target, name, description));
    }
}
```

Faster (no JSON-RPC, no pipe IO), simpler (no capability negotiation), bypasses the overload-resolution ambiguity entirely. Wire-name + description + parameter binding still come from `[McpServerToolAttribute]` + `[DescriptionAttribute]` so the contract LLMs see is unchanged.

See `Common/GA.Business.ML/Agents/Plugins/InProcessMcpToolsProvider.cs` (post-PR #151).

### MCP-over-HTTP (Claude Code parity surface)

Use `WithToolsFromAssembly`:

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(InProcessMcpToolsProvider).Assembly);
```

`WithToolsFromAssembly` walks every `[McpServerToolType]`-marked class in the assembly and registers tools correctly — including the `tools/list` handler. The visible surface is "every `[McpServerToolType]` in this assembly", which means a curated allowlist (like `GaPlugin.McpToolTypes`) does NOT drive this path; assembly scope does. If that's a problem (privileged tools added later that shouldn't be public), gate the assembly contents instead.

See `Apps/ga-server/GaApi/Program.cs` (post-PR #151) — the MCP HTTP block.

## Detection

Once you suspect MCP is mis-wired, the smoke test is one curl:

```bash
# initialize
curl -s -X POST http://localhost:5232/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -D /tmp/h.txt \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"curl","version":"1"}}}'
sid=$(grep -i mcp-session-id /tmp/h.txt | tr -d '\r' | awk '{print $2}')

# tools/list
curl -s -X POST http://localhost:5232/mcp \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -H "mcp-session-id: $sid" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
```

If the `initialize` response's `capabilities` lacks `"tools": { ... }` you have the bug. Don't bother retrying `tools/list` — fix the registration first.

## Prevention

1. **Eager validation at boot**. PR #151 introduced `McpToolsProviderStartupCheck` (an `IHostedService`) that resolves `IMcpToolsProvider.GetToolsAsync` on host startup. If `BuildTools` throws, the host fails to start with `LogCritical`. Mirror this for any new MCP-server registration: never let the failure surface only on first user request.

2. **Parity test between in-process and HTTP**. Both surfaces should advertise the same tool count + names. A test asserting that catches accidental drift.

3. **Don't trust `WithTools(...)` calls that compile silently**. If C# can pick a generic instance overload, the bug doesn't show up at compile time. Prefer explicit type arguments (`mcpBuilder.WithTools<MyTool>()`) or the assembly-scan path for clarity.

## Why this stayed latent

`SkillMdDrivenSkill` was registered as `IOrchestratorSkill` only — never as `IIntent` — so the chatbot's router never picked it. The MCP `tools/list` round-trip therefore never fired. As soon as we promoted SkillMdDrivenSkill behaviour to be reachable through the wrapper-skills' `IIntent` registration (PR #151 commit `3226f147`, "TransposeSkill delegates to SkillMdDrivenSkill"), the path became hot and the bug surfaced 100% of the time. Latent infrastructure failures bind to whichever new feature first exercises them — invest in eager startup probes proportional to the cost of *not* knowing about the failure.
