---
title: "feat: SKILL.md → C# Bridge (SkillMdDrivenSkill)"
type: feat
status: active
date: 2026-03-08
origin: docs/brainstorms/2026-03-08-skill-md-csharp-bridge-brainstorm.md
---

# feat: SKILL.md → C# Bridge (SkillMdDrivenSkill)

## Overview

A `SkillMdDrivenSkill` that loads any `.agent/skills/**/*.md` file at runtime and wraps it as a live `IOrchestratorSkill`, backed by the **Anthropic C# SDK** with **all GA domain tools** available in-process via the **MCP C# SDK**.

This closes the loop between Claude Code's `.agent/skills/` SKILL.md files and the production C# chatbot. A new chatbot skill can be prototyped in markdown, tested live in `GaChatbot`, then graduated to optimized C# when the pattern is proven.

## Problem Statement

Today the gap between Claude Code skills and the production chatbot is wide:
- Claude Code has 19 SKILL.md files with domain expertise — none are usable by the C# chatbot
- New chatbot skills require writing C# from scratch — no fast experimentation path
- Ollama (current LLM provider for `KeyIdentificationSkill`) is slow and blocks synchronous responses

This plan provides a fast, markdown-first skill development loop backed by the same Anthropic API and GA MCP tools that Claude Code itself uses.

## Proposed Solution

**Option B — Local SKILL.md loader** (selected in brainstorm; see brainstorm: docs/brainstorms/2026-03-08-skill-md-csharp-bridge-brainstorm.md):

1. Parse `.agent/skills/**/*.md` frontmatter → extract `name`, `description`, `triggers`
2. Skills with `triggers` are loaded as `SkillMdDrivenSkill` instances implementing `IOrchestratorSkill`
3. Each skill uses the body as a system prompt and calls Claude via the **Anthropic C# SDK**
4. GA domain tools are bound **in-process** as `McpClientTool[]` via `ModelContextProtocol`
5. Claude runs a multi-turn agentic tool-use loop → returns final answer

## Why NOT Microsoft Agent Framework

During research, `Microsoft.Agents.AI.Anthropic` (RC3 preview) was evaluated. **Decision: skip.**

| Criterion | Microsoft Agent Framework | Direct Stack |
|-----------|--------------------------|--------------|
| MCP Tools support | ❌ Anthropic provider only supports Function Tools | ✅ ModelContextProtocol works natively |
| Production readiness | Public preview, RC3 | Stable (Anthropic v12.8+, MCP **1.1.0** stable) |
| Dependency weight | Heavy (new framework) | Minimal (2 NuGet packages) |
| MCP integration path | Only via Azure Foundry provider | Native `McpClientTool : AIFunction` |

> The MCP tool constraint alone makes Microsoft Agent Framework a non-starter for this feature. (see brainstorm: docs/brainstorms/2026-03-08-skill-md-csharp-bridge-brainstorm.md)

## Architecture

```
ChatRequest.Message
      │
      ▼
ProductionOrchestrator
  foreach IOrchestratorSkill:
    ├── ScaleInfoSkill          (pure domain, 0 LLM)
    ├── FretSpanSkill           (pure domain, 0 LLM)
    ├── ChordSubstitutionSkill  (Grothendieck, 0 LLM)
    ├── KeyIdentificationSkill  (Ollama IChatClient)
    └── SkillMdDrivenSkill[]   ← NEW (one per SKILL.md with triggers)
              │
              ▼
        SKILL.md body → System prompt
        triggers      → CanHandle()
              │
              ▼
        Anthropic C# SDK (claude-sonnet-4-6)
              │  ←→ agentic tool-use loop
              ▼
        MCP C# SDK in-process (AIFunction[])
          GaDslTool.GaParseChord()
          GaDslTool.GaDiatonicChords()
          KeyTools.GetKeyNotes()
          ... all GA domain tools
```

### Dependency Direction Constraint

`GA.Business.ML` (Layer 4) cannot reference `GaMcpServer` (App layer). To keep the dependency graph clean, `SkillMdDrivenSkill` accepts `IReadOnlyList<AIFunction>` injected by the App layer (`GaChatbot`). The App layer builds the tool list from `GaMcpServer` tool classes via `McpClientTool` and passes them during DI composition.

```
GaChatbot (App)
  → references GA.Business.Core.Orchestration (Layer 5)
  → references GaMcpServer.Tools (App — tool declarations)
  → registers AIFunction[] in DI
  → SkillMdDrivenSkill receives IReadOnlyList<AIFunction> via constructor
```

## Technical Approach

### Phase 1: SKILL.md Parsing

**Files:**
- `Common/GA.Business.ML/Skills/SkillMd.cs` — domain record
- `Common/GA.Business.ML/Skills/SkillMdParser.cs` — YAML frontmatter + body

**`SkillMd` record:**
```csharp
// Common/GA.Business.ML/Skills/SkillMd.cs
namespace GA.Business.ML.Skills;

/// <summary>Parsed representation of a SKILL.md file.</summary>
public sealed record SkillMd
{
    public required string Name        { get; init; }
    public string          Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Triggers { get; init; } = [];
    public required string Body        { get; init; }  // markdown body = system prompt
    public required string FilePath    { get; init; }
}
```

**`SkillMdParser` — YAML frontmatter pattern** (existing `YamlDotNet 16.2.0`):
```csharp
// Common/GA.Business.ML/Skills/SkillMdParser.cs
// Pattern: split on "---" delimiter, deserialize YAML header, capture body
// Frontmatter YAML (PascalCase, matching project convention):
//   Name: "GA Chords"
//   Description: "..."
//   Triggers:
//     - "transpose"
//     - "parse chord"
```

**Acceptance criteria — Phase 1:**
- [x] `SkillMdParser.TryParse(filePath, out SkillMd?)` returns `null` for files without `triggers`
- [x] Frontmatter parsing uses `PascalCaseNamingConvention.Instance` (GA convention)
- [x] Body starts after the closing `---` delimiter
- [x] Missing `Name` frontmatter throws `InvalidOperationException` with file path in message
- [ ] Unit tests: `SkillMdParserTests` — valid file, missing name, no triggers, empty triggers list

### Phase 1.5: Chat Hook + Plugin Infrastructure

**Files:**
- `Common/GA.Business.ML/Agents/Hooks/IChatHook.cs` — hook interface + HookResult
- `Common/GA.Business.ML/Agents/Hooks/ChatHookContext.cs` — shared context object
- `Common/GA.Business.ML/Agents/Plugins/IChatPlugin.cs` — plugin interface
- `Common/GA.Business.ML/Agents/Plugins/ChatPluginAttribute.cs` — `[ChatPlugin]` marker
- `Common/GA.Business.ML/Agents/Plugins/ChatPluginHost.cs` — assembly scanner + DI registrar
- `Common/GA.Business.Core.Orchestration/Extensions/ChatbotOrchestrationExtensions.cs` — replace `AddChatbotOrchestration()` skills block with `AddChatPluginHost()`

**`IChatHook` — mirrors Claude Code's hook lifecycle:**
```csharp
// Common/GA.Business.ML/Agents/Hooks/IChatHook.cs
namespace GA.Business.ML.Agents.Hooks;

public interface IChatHook
{
    // Default implementations = opt-in per hook point (no abstract requirement)
    Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);
    Task<HookResult> OnBeforeSkill(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);
    Task<HookResult> OnAfterSkill(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);
    Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);
}

public sealed record HookResult(bool Cancel = false, string? MutatedMessage = null, AgentResponse? BlockedResponse = null)
{
    public static HookResult Continue => new();
    public static HookResult Block(string reason) => new(Cancel: true, BlockedResponse: new AgentResponse(reason));
    public static HookResult Mutate(string message) => new(MutatedMessage: message);
}
```

**`ChatHookContext`:**
```csharp
// Common/GA.Business.ML/Agents/Hooks/ChatHookContext.cs
public sealed class ChatHookContext
{
    public required string   OriginalMessage  { get; init; }
    public string            CurrentMessage   { get; set; } = string.Empty;  // mutable
    public string?           MatchedSkillName { get; init; }                 // null pre-routing
    public AgentResponse?    Response         { get; set; }                  // null pre-skill
    public string?           UserId           { get; init; }
    public IServiceProvider  Services         { get; init; } = null!;        // DI access
}
```

**`IChatPlugin` + `[ChatPlugin]`:**
```csharp
// Common/GA.Business.ML/Agents/Plugins/IChatPlugin.cs
[AttributeUsage(AttributeTargets.Class)]
public sealed class ChatPluginAttribute : Attribute;

public interface IChatPlugin
{
    string Name { get; }
    string Version => "1.0";
    IReadOnlyList<Type> SkillTypes { get; }
    IReadOnlyList<Type> HookTypes  { get; }
    IReadOnlyList<Type> McpToolTypes { get; }
}
```

**`ChatPluginHost` — assembly scanner:**
```csharp
// Common/GA.Business.ML/Agents/Plugins/ChatPluginHost.cs
public static class ChatPluginHost
{
    public static IServiceCollection AddChatPluginHost(this IServiceCollection services)
    {
        var pluginTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<ChatPluginAttribute>() is not null
                     && t.IsAssignableTo(typeof(IChatPlugin)));

        foreach (var pluginType in pluginTypes)
        {
            var plugin = (IChatPlugin)Activator.CreateInstance(pluginType)!;
            foreach (var skillType in plugin.SkillTypes)
                services.AddSingleton(typeof(IOrchestratorSkill), skillType);
            foreach (var hookType in plugin.HookTypes)
                services.AddSingleton(typeof(IChatHook), hookType);
            // McpToolTypes: stored for in-process MCP server wiring (Phase 3)
        }
        return services;
    }
}
```

**`GaPlugin` — migrates existing skill + hook registrations out of `ChatbotOrchestrationExtensions`:**
```csharp
// Common/GA.Business.Core.Orchestration/Plugins/GaPlugin.cs
[ChatPlugin]
public sealed class GaPlugin : IChatPlugin
{
    public string Name => "GA";

    public IReadOnlyList<Type> SkillTypes =>
    [
        typeof(ScaleInfoSkill),
        typeof(FretSpanSkill),
        typeof(ChordSubstitutionSkill),
        typeof(KeyIdentificationSkill),   // stays Scoped — ChatPluginHost handles lifetime
    ];

    public IReadOnlyList<Type> HookTypes =>
    [
        typeof(PromptSanitizationHook),   // replaces inline sanitization
        typeof(ObservabilityHook),         // replaces ChatbotActivitySource wiring
        typeof(MemoryWriterHook),          // replaces MongoRoutingFeedback wiring
    ];

    public IReadOnlyList<Type> McpToolTypes =>
    [
        typeof(GaDslTool), typeof(KeyTools), typeof(ChordAtonalTool),
        typeof(AtonalTool), typeof(InstrumentTool), typeof(ModeTool),
    ];
}
```

**Hook implementations extracted from `ProductionOrchestrator`:**
- `PromptSanitizationHook` — NFKD normalization + regex strip (`SYSTEM:|###|````) on `OnRequestReceived`
- `ObservabilityHook` — start/end `ActivitySource` spans at `OnBeforeSkill`/`OnAfterSkill`
- `MemoryWriterHook` — write `MongoRoutingFeedback` at `OnResponseSent`

**`ProductionOrchestrator` updates:** Iterate `IEnumerable<IChatHook>` at each lifecycle point. Sequential execution — if any hook returns `Cancel`, return `BlockedResponse` immediately.

**Acceptance criteria — Phase 1.5:**
- [x] `IChatHook` default implementations allow opt-in per lifecycle point
- [x] `HookResult.Block()` stops pipeline; orchestrator returns `BlockedResponse` to caller
- [x] `HookResult.Mutate()` replaces `ChatHookContext.CurrentMessage` before skill sees it
- [x] `[ChatPlugin]` assembly scan finds `GaPlugin` at startup
- [x] `PromptSanitizationHook` blocks messages containing `SYSTEM:`, `###`, triple-backtick injection patterns
- [x] `ObservabilityHook` produces spans tagged `skill.name` + `skill.result`
- [ ] `MemoryWriterHook` writes routing feedback (deferred — MongoRoutingFeedback extraction separate)
- [x] `KeyIdentificationSkill` still registered as Scoped (plugin host respects lifetime annotation)
- [x] `ChatbotOrchestrationExtensions` removes manual skill DI — replaced by `AddChatPluginHost()`
- [ ] Unit tests: hook cancel blocks pipeline, hook mutate changes message, hook exception is caught + logged

---

### Phase 2: SkillMdDrivenSkill

**File:** `Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs`

```csharp
// Common/GA.Business.ML/Agents/Skills/SkillMdDrivenSkill.cs
namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Skills;

/// <summary>
/// IOrchestratorSkill backed by a SKILL.md file.
/// Uses the Anthropic SDK for LLM and injected AIFunction[] for GA domain tools.
/// </summary>
public sealed class SkillMdDrivenSkill(
    SkillMd skillMd,
    IReadOnlyList<AIFunction> tools,
    IConfiguration configuration,
    ILogger<SkillMdDrivenSkill> logger) : IOrchestratorSkill
{
    public string Name        => skillMd.Name;
    public string Description => skillMd.Description;

    public bool CanHandle(string message)
    {
        if (skillMd.Triggers.Count == 0) return false;
        var lower = message.ToLowerInvariant();
        return skillMd.Triggers.Any(t => lower.Contains(t.ToLowerInvariant()));
    }

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken ct = default)
    {
        // 1. AnthropicClient (reads ANTHROPIC_API_KEY automatically if not in config)
        // 2. .AsIChatClient(model).AsBuilder().UseFunctionInvocation().Build()
        // 3. chatClient.GetResponseAsync(message, new ChatOptions { Tools = [..tools] })
        //    with system prompt = skillMd.Body in ChatMessage(ChatRole.System, ...)
        // 4. Map final response text to AgentResponse
    }
}
```

**Agentic loop** — handled automatically by `UseFunctionInvocation()` middleware:
```csharp
// No hand-coded loop required. UseFunctionInvocation() intercepts tool_use stops,
// dispatches each AIFunction, feeds results back, and loops until a text response.
IChatClient chatClient = new AnthropicClient()
    .AsIChatClient(model)
    .AsBuilder()
    .UseFunctionInvocation()   // ← handles the entire multi-turn agentic loop
    .Build();

var response = await chatClient.GetResponseAsync(
    [new ChatMessage(ChatRole.System, skillMd.Body),
     new ChatMessage(ChatRole.User, message)],
    new ChatOptions { Tools = [.. tools] },
    ct);
```

Reference implementation: `Apps/ga-server/GaApi/Services/ClaudeChatService.cs` — system prompt goes in `MessageCreateParams.System`, not the messages array (same pattern applies via `IChatClient`).

**Acceptance criteria — Phase 2:**
- [x] `CanHandle()` returns `true` only when ≥1 trigger keyword matches (case-insensitive substring)
- [x] `CanHandle()` returns `false` if `Triggers` is empty
- [x] `ExecuteAsync()` calls Anthropic API with `claude-sonnet-4-6` (configurable via `AnthropicSkills:Model`)
- [x] Tool calls dispatched via `UseFunctionInvocation()` middleware — no manual loop
- [x] `ANTHROPIC_API_KEY` missing → `InvalidOperationException` with actionable message at first use
- [ ] Unit test: mock `IChatClient` — verify system prompt injection and tool wiring

### Phase 3: SkillMdPlugin + In-Process MCP Wiring

**`SkillMdPlugin`** — replaces `AddSkillMdSkills()` extension; SKILL.md bridge is now a first-class plugin:

```csharp
// Common/GA.Business.ML/Agents/Plugins/SkillMdPlugin.cs
[ChatPlugin]
public sealed class SkillMdPlugin : IChatPlugin
{
    public string Name => "SkillMd";

    // Skills discovered dynamically from .agent/skills/**/*.md at plugin registration
    // (SkillMdLoader.LoadFromDirectory() called by ChatPluginHost when registering this plugin)
    public IReadOnlyList<Type> SkillTypes  => [];  // dynamic — see SkillMdPluginRegistrar
    public IReadOnlyList<Type> HookTypes   => [];
    public IReadOnlyList<Type> McpToolTypes => [];  // shares GaPlugin MCP tools
}
```

Because `SkillMdDrivenSkill` instances are built from runtime data (parsed SKILL.md files), `ChatPluginHost` needs a special registration path for `SkillMdPlugin`. The plugin host detects `IChatPlugin` implementations that also implement `IDynamicSkillPlugin` and calls `RegisterDynamicSkills(IServiceCollection, IReadOnlyList<AIFunction>)` instead.

**`SkillMdLoader`:**
```csharp
// Common/GA.Business.ML/Skills/SkillMdLoader.cs
public static class SkillMdLoader
{
    public static IReadOnlyList<SkillMd> LoadFromDirectory(string rootPath)
        => Directory
            .EnumerateFiles(rootPath, "SKILL.md", SearchOption.AllDirectories)
            .Select(SkillMdParser.TryParse)
            .OfType<SkillMd>()
            .Where(s => s.Triggers.Count > 0)
            .ToList();
}
```

**In-process MCP tool wiring** (assembled once by `ChatPluginHost`, shared by all plugins):

MCP 1.1.0 uses `System.IO.Pipelines.Pipe` — no subprocess, no HTTP:
```csharp
// ChatPluginHost assembles in-process MCP client from all plugin McpToolTypes
var clientToServer = new Pipe();
var serverToClient = new Pipe();

var serverServices = new ServiceCollection();
serverServices.AddLogging();
var mcpBuilder = serverServices.AddMcpServer()
    .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream());

foreach (var toolType in allPluginMcpToolTypes)
    mcpBuilder.WithTools(toolType);             // one call per tool class

serverServices.AddSingleton<IGrothendieckService, GrothendieckService>();
// ... other domain services the tools need

var serverProvider = serverServices.BuildServiceProvider();
_ = serverProvider.GetRequiredService<McpServer>().RunAsync(hostCts.Token);

var mcpClient = await McpClient.CreateAsync(
    new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));

IReadOnlyList<AIFunction> gaTools = (await mcpClient.ListToolsAsync()).ToList().AsReadOnly();
// gaTools injected into SkillMdDrivenSkill via DI
```

**`GaChatbot/Program.cs` startup — full plugin host:**
```csharp
// Before: manual skill + hook DI wiring
// After: one call
builder.Services.AddChatPluginHost();
// Discovers GaPlugin + SkillMdPlugin automatically
```

**Acceptance criteria — Phase 3:**
- [ ] `SkillMdLoader.LoadFromDirectory()` skips files without frontmatter or without triggers
- [ ] `[ChatPlugin]` assembly scan discovers `SkillMdPlugin` alongside `GaPlugin`
- [ ] `DI` resolves `IEnumerable<IOrchestratorSkill>` containing static skills (from `GaPlugin`) + dynamic SKILL.md skills
- [ ] In-process MCP pipe established once at startup; `McpClientTool[]` injected into `SkillMdDrivenSkill`
- [ ] Missing `.agent/skills/` directory → logs warning, 0 dynamic skills registered (no crash)
- [ ] `GaChatbot/Program.cs` replaces all manual skill/hook DI calls with `AddChatPluginHost()`

### Phase 4: GaChatbotCli (Standalone CLI)

**Purpose:** Claude Code skill files (`ga chat`) invoke this CLI to test the full orchestration stack in-process without starting Aspire.

**File:** `Apps/GaChatbotCli/Program.cs` (new console app)

```csharp
// Apps/GaChatbotCli/Program.cs
// Usage: dotnet run --project Apps/GaChatbotCli -- "Parse Am7 for me"
// 1. Build DI container (same as GaChatbot but without Blazor/HTTP)
// 2. AddChatbotOrchestration() + AddSkillMdSkills()
// 3. Run ProductionOrchestrator.Chat(args[0])
// 4. Print routing metadata + response to stdout (JSON or plain)
```

**Claude Code skill integration** (`.agent/skills/ga/chat/SKILL.md`):
```bash
# The skill calls this CLI to test in-process without Aspire
dotnet run --project Apps/GaChatbotCli -- "$MESSAGE"
```

**Acceptance criteria — Phase 4:**
- [ ] `dotnet run --project Apps/GaChatbotCli -- "parse Am7"` produces a response
- [ ] Output includes `routing.agentId` and `routing.routingMethod`
- [ ] Works without MongoDB/Redis (in-memory fallbacks)
- [ ] `ANTHROPIC_API_KEY` is required; clear error if missing

### Phase 5: Graduation Path (Optional CLI Scaffolding)

**`ga skill scaffold <SKILL.md path>`** — generates a C# `IOrchestratorSkill` skeleton:

```fsharp
// Apps/GaCli/Program.fs — add subcommand "skill scaffold"
// Reads frontmatter: Name, Description, Triggers
// Emits skeleton .cs file following FretSpanSkill.cs pattern
```

**Acceptance criteria — Phase 5:**
- [ ] `dotnet run --project Apps/GaCli -- skill scaffold .agent/skills/ga/chords/SKILL.md` emits `ChordsSkill.cs`
- [ ] Skeleton includes: class stub, `Name`/`Description`/`CanHandle()` from SKILL.md triggers, TODO body
- [ ] Output path: `Common/GA.Business.ML/Agents/Skills/<PascalName>Skill.cs`

## Dependencies to Add

| Package | Version | Project | Purpose |
|---------|---------|---------|---------|
| `Anthropic` | 12.8.0 | `GA.Business.ML` | Claude API + `AsIChatClient()` bridge |
| `ModelContextProtocol` | **1.1.0** (stable) | `GA.Business.ML` | `AIFunction`, `McpClientTool`, pipe transport |
| `ModelContextProtocol` | upgrade to 1.1.0 | `GaMcpServer` | Keep in sync — `WithStreamServerTransport` replaces removed `McpServerFactory` |

> `GaMcpServer` currently uses `0.1.0-preview.10`. Upgrade to `1.1.0` together with `GA.Business.ML` — the `[McpServerTool]`/`[McpServerToolType]` attributes and `WithTools<T>` API are stable across both versions.

`YamlDotNet 16.2.0` is already in `GA.Business.ML` — no new dependency.

## SKILL.md Frontmatter Convention

Add `triggers` field to any SKILL.md to auto-load it as a chatbot skill:

```yaml
---
name: "GA Chords"
description: "Parse chords, transpose progressions, find diatonic sets"
triggers:
  - "transpose"
  - "diatonic chords"
  - "parse chord"
  - "what chords are in"
  - "chord substitut"
---
```

Skills without `triggers` remain pure Claude Code guides and are ignored by the loader.

## System-Wide Impact

### Interaction Graph

```
GaChatbot startup
  → AddChatPluginHost()
  → Assembly scan → finds [ChatPlugin]: GaPlugin, SkillMdPlugin
  → GaPlugin: registers ScaleInfoSkill, FretSpanSkill, ChordSubstitutionSkill,
               KeyIdentificationSkill, PromptSanitizationHook, ObservabilityHook, MemoryWriterHook
  → SkillMdPlugin: scans .agent/skills/**/*.md → registers N SkillMdDrivenSkill instances
  → ChatPluginHost: builds in-process MCP pipe from all plugin McpToolTypes
  → McpClient.ListToolsAsync() → IReadOnlyList<AIFunction> (shared by all SkillMdDrivenSkill)

ChatRequest arrives
  → IEnumerable<IChatHook>.OnRequestReceived()         ← PromptSanitizationHook (may Block)
  → ProductionOrchestrator.foreach IOrchestratorSkill
      → IEnumerable<IChatHook>.OnBeforeSkill()         ← ObservabilityHook (start span)
      → skill.CanHandle() → match
      → skill.ExecuteAsync()                           ← SkillMdDrivenSkill or pure-domain skill
      → IEnumerable<IChatHook>.OnAfterSkill()          ← ObservabilityHook (end span)
  → IEnumerable<IChatHook>.OnResponseSent()            ← MemoryWriterHook (write to Mongo)
  → Returns AgentResponse to caller
```

### Error Propagation

- `ANTHROPIC_API_KEY` missing → `InvalidOperationException` at first use (not startup), logs error
- Anthropic API rate limit / transient error → propagate as `OrchestratorException`; fallback to next skill
- Tool invocation failure → logged, tool_result contains error message; Claude self-corrects or falls back
- SKILL.md parse failure → logged as warning, that file is skipped; remaining skills loaded normally

### State Lifecycle Risks

- `SkillMdDrivenSkill` is Singleton with no mutable state — safe ✅
- `AIFunction[]` (tool list) built once at startup — safe ✅
- Anthropic SDK creates `HttpClient` internally — should be monitored for socket exhaustion at high RPS
- Mitigation: reuse `AnthropicClient` instance (or inject via `IHttpClientFactory`)

### API Surface Parity

- All `IOrchestratorSkill` implementors share the same `CanHandle`/`ExecuteAsync` contract — SkillMdDrivenSkill is a drop-in sibling of `FretSpanSkill`/`ChordSubstitutionSkill`
- `ProductionOrchestrator` enumerates via `IEnumerable<IOrchestratorSkill>` — no changes required

### Integration Test Scenarios

1. **Trigger match → Anthropic called**: Load SKILL.md with `triggers: ["test-trigger"]`, send message containing `"test-trigger"`, verify `ExecuteAsync` is called (mock Anthropic client)
2. **No trigger → skip**: Message with no matching trigger → `CanHandle()` returns false → `ExecuteAsync` never called
3. **Tool use loop**: Anthropic returns `tool_use`, tool is invoked, loop continues until text response
4. **Multi-skill coexistence**: 2 SKILL.md files with different triggers → correct skill routes each message
5. **Graduation invariant**: After replacing with handcoded C# skill, same trigger message routes to new skill (not SKILL.md version) — remove `triggers` from SKILL.md to achieve this

## Acceptance Criteria

### Functional

- [ ] `dotnet run --project Apps/GaChatbotCli -- "transpose C major up a fifth"` returns correct answer using GA tools
- [ ] SKILL.md files without `triggers` are ignored by the loader
- [ ] `ProductionOrchestrator` routes "transpose" → `SkillMdDrivenSkill[GA Chords]` (not `KeyIdentificationSkill`)
- [ ] Multi-turn tool-use loop works: Claude calls `GaTransposeChord`, receives result, returns final answer
- [ ] `curl -X POST /api/chatbot/chat -d '{"message":"transpose C major"}' | jq '.routing.routingMethod'` → `"orchestrator-skill"`

### Non-Functional

- [ ] `SkillMdDrivenSkill` response time ≤ 5 seconds on first call (Anthropic API, no tool calls)
- [ ] Zero additional allocations in `CanHandle()` (string operations only, no Regex per-call)
- [ ] `ANTHROPIC_API_KEY` env var configured in `GaChatbot` and `GaChatbotCli`
- [ ] Build passes: `dotnet build AllProjects.slnx -c Debug`
- [ ] No new compilation warnings in touched files

### Quality Gates

- [ ] `SkillMdParserTests`: 5+ unit tests covering parse success, missing name, no triggers, bad YAML
- [ ] `SkillMdDrivenSkillTests`: mock Anthropic responses — no tool calls, single tool call, multi-turn
- [ ] Pre-commit hook passes (`dotnet format --verify-no-changes`)

## Implementation Phases

| Phase | Deliverable | Dependencies |
|-------|-------------|-------------|
| 1 | `SkillMd` record + `SkillMdParser` | None |
| **1.5** | **`IChatHook`, `ChatHookContext`, `IChatPlugin`, `[ChatPlugin]`, `ChatPluginHost`, `GaPlugin`** | **None** |
| 2 | `SkillMdDrivenSkill` (agentic loop via `UseFunctionInvocation()`) | Phase 1, Anthropic NuGet |
| 3 | `SkillMdPlugin`, `SkillMdLoader`, in-process MCP wiring via `ChatPluginHost` | Phase 1.5, Phase 2, MCP 1.1.0 |
| 4 | `GaChatbotCli` console app | Phase 3 |
| 5 | `ga skill scaffold` (GaCli subcommand) | Phase 1 (reads frontmatter only) |

## Graduation Path

```
1. Author SKILL.md with triggers → auto-loaded as SkillMdDrivenSkill
2. Test via GaChatbotCli or chatbot — refine markdown
3. Stable → scaffold: dotnet run --project Apps/GaCli -- skill scaffold .agent/skills/foo/SKILL.md
4. Implement domain logic in generated CSharpSkill.cs (pure domain, 0 LLM)
5. Remove triggers from SKILL.md (reverts to Claude Code guide only)
```

## Sources & References

### Origin

- **Brainstorm 1:** [docs/brainstorms/2026-03-08-skill-md-csharp-bridge-brainstorm.md](../brainstorms/2026-03-08-skill-md-csharp-bridge-brainstorm.md)
  - Option B (local SKILL.md loader) selected; Anthropic SDK + MCP in-process; explicit `triggers`
- **Brainstorm 2:** [docs/brainstorms/2026-03-08-chat-plugins-skills-hooks-brainstorm.md](../brainstorms/2026-03-08-chat-plugins-skills-hooks-brainstorm.md)
  - Full Claude Code Plugins/Skills/Hooks mental model adopted; Approach C (assembly scanning); `IChatHook` tight coupling with cancel/mutate; `SkillMdDrivenSkill` → `SkillMdPlugin`

### Internal References

- Existing skills pattern: `Common/GA.Business.ML/Agents/Skills/FretSpanSkill.cs`
- DI registration pattern: `Common/GA.Business.Core.Orchestration/Extensions/ChatbotOrchestrationExtensions.cs`
- MCP tool declarations: `GaMcpServer/Tools/GaDslTool.cs`, `GaMcpServer/Tools/KeyTools.cs`
- YAML parsing convention: `Common/GA.Business.Config/` (PascalCase, YamlDotNet)
- `IOrchestratorSkill` interface: `Common/GA.Business.ML/Agents/` (existing)

### External References

- Anthropic C# SDK: [github.com/anthropics/anthropic-sdk-dotnet](https://github.com/anthropics/anthropic-sdk-dotnet) — NuGet `Anthropic`
- MCP C# SDK: [github.com/modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — NuGet `ModelContextProtocol` 1.1.0
- `McpClientTool` API docs: `ModelContextProtocol.Client.McpClientTool` — `sealed class McpClientTool : AIFunction`
- In-process pipe pattern: `tests/ModelContextProtocol.Tests/ClientServerTestBase.cs` in SDK repo
- `UseFunctionInvocation()` middleware: `Microsoft.Extensions.AI` — handles full agentic tool loop
- Spring AI Anthropic Agentic Skills (inspiration): [spring.io/blog/2026/01/28/spring-ai-anthropic-agentic-skills](https://spring.io/blog/2026/01/28/apring-ai-anthropic-agentic-skills)
- Existing Anthropic reference: `Apps/ga-server/GaApi/Services/ClaudeChatService.cs` (in-project canonical usage)
- Security patterns applied: MCP allowlist (prefix-based), prompt injection sanitization (NFKD + regex) — from `docs/solutions/compound-reviews/2026-03-07-ce-review-security-arch-hygiene.md`
