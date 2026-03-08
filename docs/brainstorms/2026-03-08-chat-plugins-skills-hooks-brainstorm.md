# Brainstorm: GA Chat Plugin/Skill/Hook Architecture

**Date:** 2026-03-08
**Status:** Ready for planning
**Author:** AI + Stephane Pareilleux

---

## What We're Building

A three-layer extension architecture for the GA chatbot orchestration stack that mirrors Claude Code's own mental model:

| Concept | Claude Code | GA C# |
|---------|------------|-------|
| **Skills** | SKILL.md — trigger-matched behaviors | `IOrchestratorSkill` (already exists) |
| **Hooks** | `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `Stop` | `IChatHook` — tight interceptors at 4 lifecycle points |
| **Plugins** | Bundle of skills + hooks + MCP tools | `IChatPlugin` — assembly-scanned, self-registering |

The hook model decouples security, observability, and memory from `ProductionOrchestrator`. The plugin model makes related skills, hooks, and tools a single deployable unit discoverable via `[ChatPlugin]` attribute.

---

## Why This Approach

Three options were evaluated:

| Option | Description | Decision |
|--------|-------------|----------|
| A — Hooks only | `IChatHook` at 4 lifecycle points, no plugin concept | Rejected: doesn't close the bundling gap |
| B — Hooks + explicit plugin registration | `IChatPlugin` with manual `AddChatPlugin<T>()` | Rejected: manual registration is the current anti-pattern |
| **C — Full plugin host with assembly scanning** | `[ChatPlugin]` attribute, host discovers + registers all skills/hooks/tools automatically | **Selected** |

**Option C fits GA because:**
- Three concrete hook implementations exist today as inline code (`MongoRoutingFeedback`, `ChatbotActivitySource`, prompt sanitization) — they need a home
- The SKILL.md bridge (`SkillMdDrivenSkill`) is a natural plugin: it bundles its own skills + MCP tools
- Assembly scanning is how ASP.NET Core controller discovery works — a familiar, tested pattern in .NET
- Future GA modules (BSP dungeon, SoundBank, etc.) may each contribute their own skills

---

## Key Decisions

### 1. Hook Lifecycle Points: All Four

All four Claude Code lifecycle points are needed:

```
OnRequestReceived   →  UserPromptSubmit  (sanitize, rate-limit, auth)
OnBeforeSkill       →  PreToolUse        (cancel skill, mutate message)
OnAfterSkill        →  PostToolUse       (observe result, mutate response)
OnResponseSent      →  Stop              (memory writing, analytics, cleanup)
```

### 2. Hook Coupling: Tight (can cancel or mutate)

Hooks return `HookResult` — they can cancel the pipeline or replace the message/response:

```csharp
interface IChatHook
{
    Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);

    Task<HookResult> OnBeforeSkill(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);

    Task<HookResult> OnAfterSkill(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);

    Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct)
        => Task.FromResult(HookResult.Continue);
}

record HookResult(bool Cancel = false, string? MutatedMessage = null, AgentResponse? BlockedResponse = null)
{
    public static HookResult Continue => new();
    public static HookResult Block(string reason) => new(Cancel: true, BlockedResponse: new AgentResponse(reason));
    public static HookResult Mutate(string message) => new(MutatedMessage: message);
}
```

Hook execution is **sequential in registration order**. If any hook returns `Cancel: true`, the pipeline stops and `BlockedResponse` is returned to the user. This mirrors Claude Code's PreToolUse veto.

### 3. Plugin Model: Assembly Scanning via `[ChatPlugin]` Attribute

```csharp
[ChatPlugin]
public class GaPlugin : IChatPlugin
{
    public string Name    => "GA";
    public string Version => "1.0";

    public IReadOnlyList<Type> SkillTypes => [
        typeof(ScaleInfoSkill),
        typeof(FretSpanSkill),
        typeof(ChordSubstitutionSkill),
        typeof(KeyIdentificationSkill),
    ];

    public IReadOnlyList<Type> HookTypes => [
        typeof(PromptSanitizationHook),   // was inline in ProductionOrchestrator
        typeof(ObservabilityHook),         // was ChatbotActivitySource
        typeof(MemoryWriterHook),          // was MongoRoutingFeedback
    ];

    public IReadOnlyList<Type> McpToolTypes => [
        typeof(GaDslTool),
        typeof(KeyTools),
        typeof(ChordAtonalTool),
        typeof(AtonalTool),
        typeof(InstrumentTool),
        typeof(ModeTool),
    ];
}
```

The plugin host (`AddChatPluginHost()`) scans `AppDomain.CurrentDomain.GetAssemblies()` for classes with `[ChatPlugin]` at startup, instantiates each, and registers their types in DI.

### 4. SKILL.md Bridge Becomes a Plugin

`SkillMdDrivenSkill` is not bolted onto `ChatbotOrchestrationExtensions` — it becomes `SkillMdPlugin`:

```csharp
[ChatPlugin]
public class SkillMdPlugin : IChatPlugin
{
    public string Name => "SkillMd";
    // Skills: dynamically discovered from .agent/skills/**/*.md (triggers only)
    // Tools: all MCP tools (shared with GaPlugin)
    // Hooks: none (skills handle their own lifecycle)
}
```

This makes SKILL.md skills truly plug-and-play: add a SKILL.md with `triggers`, the plugin discovers it at startup.

### 5. Context Object: ChatHookContext

```csharp
record ChatHookContext
{
    required string OriginalMessage { get; init; }
    string          CurrentMessage  { get; set; }  // mutable: hooks can rewrite
    string?         MatchedSkillName { get; init; } // null before OnBeforeSkill
    AgentResponse?  Response        { get; set; }  // set after skill executes
    string?         UserId          { get; init; }
    IServiceProvider Services       { get; init; } // DI access for stateful hooks
}
```

### 6. Hook Execution Order: Registration Order (DI)

Hooks fire in the order they are registered in DI (plugin registration order, then hook order within each plugin). `GaPlugin`'s `PromptSanitizationHook` registers first — it always runs before any skill routing.

### 7. Existing Code Migration

| Existing code | Migrates to |
|---------------|-------------|
| `MongoRoutingFeedback` (inside orchestrator) | `MemoryWriterHook : IChatHook` (OnResponseSent) |
| `ChatbotActivitySource` (hard-coded in orchestrator) | `ObservabilityHook : IChatHook` (all 4 points) |
| Prompt sanitization (inline in orchestrator) | `PromptSanitizationHook : IChatHook` (OnRequestReceived) |
| `services.AddSingleton<IOrchestratorSkill, ScaleInfoSkill>()` etc. in `ChatbotOrchestrationExtensions` | Moved into `GaPlugin.SkillTypes` |

---

## Architecture

```
GaChatbot startup
  → services.AddChatPluginHost()
  → Scans assemblies for [ChatPlugin]
  → Finds: GaPlugin, SkillMdPlugin
  → Registers all skill types, hook types, MCP tool types from each plugin

ChatRequest arrives
  → IEnumerable<IChatHook>.OnRequestReceived()   (PromptSanitizationHook → block/mutate)
  → ProductionOrchestrator.foreach IOrchestratorSkill
      → IEnumerable<IChatHook>.OnBeforeSkill()   (ObservabilityHook → start span)
      → skill.ExecuteAsync()
      → IEnumerable<IChatHook>.OnAfterSkill()    (ObservabilityHook → end span, MemoryWriterHook)
  → IEnumerable<IChatHook>.OnResponseSent()      (MemoryWriterHook → write to Mongo)
```

---

## Relationship to SKILL.md Bridge Plan

This brainstorm **extends** the existing plan at `docs/plans/2026-03-08-feat-skill-md-csharp-bridge-plan.md`:

- Phase 1 (`SkillMd`, `SkillMdParser`) — unchanged
- Phase 1.5 (NEW) — `IChatHook`, `HookResult`, `ChatHookContext`, `[ChatPlugin]`, `IChatPlugin`, `ChatPluginHost`
- Phase 2 (`SkillMdDrivenSkill`) — unchanged; moves inside `SkillMdPlugin`
- Phase 3 — replaces `AddSkillMdSkills()` with `AddChatPluginHost()` (assembly scanning)
- Phase 4 (`GaChatbotCli`) — uses plugin host, zero manual DI wiring

---

## Open Questions

*(Resolved during brainstorm — none remaining)*

### Resolved Questions

| Question | Decision |
|----------|----------|
| Hooks loose vs tight? | Tight — cancel + mutate supported via `HookResult` |
| Which lifecycle points? | All four: OnRequestReceived, OnBeforeSkill, OnAfterSkill, OnResponseSent |
| Plugins explicit vs scanned? | Assembly scanning via `[ChatPlugin]` attribute |
| Plugin model scope? | Full host, today (not YAGNI) |
| Where does SKILL.md bridge live? | `SkillMdPlugin` — first-class plugin, not a DI extension |
| Hook execution: sequential or parallel? | Sequential (cancellation semantics require ordering) |
| Hook DI access? | `ChatHookContext.Services` provides `IServiceProvider` for stateful hooks |

---

## What Success Looks Like

```bash
# 1. Add a new SKILL.md with triggers → auto-loaded, zero C# changes
cat .agent/skills/ga/chords/SKILL.md  # add triggers: [...]

# 2. Add a new hook → tag with [ChatPlugin] or add type to GaPlugin.HookTypes
# 3. ChatbotActivitySource is now ObservabilityHook — no code in ProductionOrchestrator
# 4. Prompt injection sanitization runs before every request — no try/catch in orchestrator
# 5. Future GA module ships as its own [ChatPlugin] class — skills + hooks + tools in one place
```
