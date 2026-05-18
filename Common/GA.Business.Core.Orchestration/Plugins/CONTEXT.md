# Orchestration Plugin Surface Context

> Fresh-session orientation for `Common/GA.Business.Core.Orchestration/Plugins/`. Read this BEFORE touching anything in this subsystem.

## What this subsystem is

The DI composition root for the chatbot's domain skills, hooks, MCP tools, and persistent stores. `GaPlugin` is a single `[ChatPlugin]`-attributed `IChatPlugin` discovered automatically by `ChatPluginHost` (no manual wiring needed in hosts). It runs in **layer 5** (Orchestration) and may depend on layer 4 (`GA.Business.ML`) — but lower layers MUST NOT depend on it. Every new chatbot skill, hook, or MCP tool that the chatbot is expected to dispatch lands here; missing the registration is the #1 reason a skill "exists in the codebase but the bot can't find it."

## Key invariants (DO NOT VIOLATE)

- **Layer rule:** AI code in layer 4 (`GA.Business.ML`). Orchestration in layer 5 (here). **Never in lower layers.** See `docs/architecture/layers.md`. A new skill's `.cs` lives in `GA.Business.ML/Agents/Skills/`; only its registration line lives here.
- **`AddOrchestratorSkillIntent<T>()` is the only sanctioned registration path.** It binds three services: the concrete skill, `IOrchestratorSkill`, and `IIntent` via `OrchestratorSkillIntent`. Manual `services.AddSingleton<MySkill>()` silently breaks `SemanticIntentRouter` dispatch.
- **Skill lifetime must match its dependencies.** Skills that depend on `IChatClient` MUST be `ServiceLifetime.Scoped` (because `IChatClient` is Scoped). All other skills are Singleton. The default in `AddOrchestratorSkillIntent` is Singleton — opt into Scoped explicitly. `KeyIdentificationSkill` and `ProgressionCompletionSkill` are the only current Scoped skills.
- **Order matters in `Register()`.** Hooks execute in registration order at each lifecycle point. `MemoryHook` MUST be registered BEFORE `MemoryWriteHook` so the transcript write fires before the durable-claim write (transcript = ground truth).
- **`McpToolTypes` is referenced by type, not project dependency.** Don't add a `<ProjectReference>` to `GaMcpServer` from this plugin. The list is consumed reflectively by `ChatPluginHost`.
- **`MemoryStore` factory must wire the logger AND the embedder.** Without `ILogger<MemoryStore>`, Load() IO errors are swallowed silently (PR #157 rel-001). Without `IEmbeddingGenerator`, `SearchHybridAsync` silently degrades to BM25-only.
- **`IOperatorTranscriptReader` is operator-only by name.** Don't wire it into runtime chat code (chat-runtime callers must use `ChatTranscriptStore` directly or via `MemoryHook`). The rename from `IChatTranscriptStore` on 2026-05-12 carries this contract explicitly.
- **`GaPlugin.cs` is the single source of truth for which skills the chatbot can dispatch.** If you add a skill class but don't add an `AddOrchestratorSkillIntent<>` line here, the chatbot will never call it — semantic routing only ranks registered intents.
- **Catalog-only graduated skills still need a C# wrapper here.** `CircleOfFifthsSkill`, `PracticeRoutineSkill`, etc. — the body lives in `skills/<name>/SKILL.md`, but the C# wrapper supplies routing metadata. No C# class = no routing.

## The 5-10 files that matter

- `GaPlugin.cs` (this folder) — the registration list. Read top-to-bottom; the comments document why each skill exists.
- `../Intents/IntentRegistrationExtensions.cs` — defines `AddOrchestratorSkillIntent<T>()`. Three-service binding (concrete + `IOrchestratorSkill` + `IIntent`).
- `../Extensions/ChatbotOrchestrationExtensions.cs` — `AddChatbotOrchestration()` — the outer entry that hosts call. Bootstraps the F# closure registry, wires `SemanticIntentRouter`, `ChatPluginHost`, Ollama HTTP client.
- `../../GA.Business.ML/Agents/IOrchestratorSkill.cs` — the contract every registration here implements.
- `../../GA.Business.ML/Agents/Intents/SemanticIntentRouter.cs` — what the `IIntent` registrations feed into.
- `../../GA.Business.ML/Agents/Hooks/MemoryHook.cs` + `MemoryWriteHook.cs` — the two hooks whose registration order is load-bearing.
- `../../GA.Business.ML/Agents/Mcp/DslEvalMcpTools.cs` — the MCP surface for Path B (DSL-eval) skills; referenced in `McpToolTypes`.
- `../../../docs/architecture/layers.md` — the layer rule that gates what's allowed to live here.
- `../../../docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md` — the migration plan all skill work descends from (still canonical).

## How to register a new chatbot skill

1. **Author the skill in `GA.Business.ML/Agents/Skills/<Name>Skill.cs`** (NOT here). See that folder's CONTEXT.md.
2. **Add ONE line to `GaPlugin.Register()`** with a 2-3 line comment naming the gap it closes and the date:
   ```csharp
   // <Date> — <gap closed in chatbot capability matrix>.
   services.AddOrchestratorSkillIntent<YourSkill>();
   ```
3. **If the skill needs `IChatClient`**, pass `ServiceLifetime.Scoped`. Otherwise omit (defaults to Singleton).
4. **Place the registration near peers.** Domain-retrieval skills group with `ChordVoicingsSkill` / `ImprovisationSkill`. Path-B (DSL-eval) skills group with `TransposeSkill` / `CommonTonesSkill` / `DiatonicChordsSkill`. Domain-backed (no LLM) group with `RelativeKeySkill` / `CapoSkill`. Order inside a group is chronological.
5. **No other registration sites.** Do not add a `services.AddSingleton<YourSkill>()` in `ChatbotOrchestrationExtensions.cs` or in the GaChatbot.Api host. `AddChatPluginHost` discovers this plugin reflectively.
6. **If the skill exposes an MCP tool**, add the tool type to `McpToolTypes` at the bottom of the file. Wire-name convention: `ga_<topic>_<verb>`.

## What NOT to do here

- Don't add a `<ProjectReference>` from this project to `GaMcpServer`. `McpToolTypes` is intentionally reflective.
- Don't register a skill from a host's `Program.cs` or a different plugin. Two registrations = double intent embeddings = misroutes.
- Don't reorder `MemoryHook` / `MemoryWriteHook`. Both are idempotent so it won't crash, but log ordering breaks (transcript-write must precede durable-write per the architecture note at `docs/solutions/architecture/2026-05-11-memoryhook-conflates-transcript-log-with-durable-memory.md`).
- Don't add a Scoped lifetime to a pure-domain skill. It works at first, but `SemanticIntentRouter` warms intents in a Singleton scope and will fail to resolve a Scoped skill at boot.
- Don't promote orchestration helpers into `GA.Business.ML`. The layer rule is one-way; layer 4 can't see layer 5.
- Don't refactor `AddOrchestratorSkillIntent` to drop the `IOrchestratorSkill` binding. The legacy `CanHandle` foreach fallback in `ProductionOrchestrator` still iterates it.
- Don't remove the `GaClosureBootstrap.init()` call from `ChatbotOrchestrationExtensions`. F# module init is lazy; ga_dsl_eval will silently return "closure not exposed" without it (diagnosed 2026-05-07 via codex CLI).

## Where to look for related context

- Parent: [/CLAUDE.md](../../../CLAUDE.md) — layer rule, build/test commands.
- Sibling: [`../../GA.Business.ML/Agents/CONTEXT.md`](../../GA.Business.ML/Agents/CONTEXT.md) — where the skill source lives.
- Sibling: [`../../../Apps/GaChatbot.Api/CONTEXT.md`](../../../Apps/GaChatbot.Api/CONTEXT.md) — the host that calls `AddChatbotOrchestration()`.
- Architecture: [`docs/architecture/layers.md`](../../../docs/architecture/layers.md) — the five-layer rule.
- Plan (canonical): `docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md`.
- Solutions: `docs/solutions/architecture/2026-05-07-di-composition-root-casing-drift.md`, `docs/solutions/runtime-errors/fsharp-module-init-closure-registry.md`, `docs/solutions/runtime-errors/2026-05-07-mcp-withtools-overload-resolution-trap.md`.
