namespace GA.Business.Core.Orchestration.Plugins;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Mcp;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Memory.Curator;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The core GA chatbot plugin — bundles all built-in orchestrator skills and hooks.
/// Discovered automatically by <see cref="ChatPluginHost"/> via <see cref="ChatPluginAttribute"/>.
/// </summary>
[ChatPlugin]
public sealed class GaPlugin : IChatPlugin
{
    public string Name    => "GA";
    public string Version => "1.0";

    public void Register(IServiceCollection services)
    {
        // ── Domain services required by skills ────────────────────────────────
        services.TryAddSingleton<IGrothendieckService, GrothendieckService>();

        // ── Orchestrator skills (semantic dispatch via SemanticIntentRouter; ──
        //     legacy CanHandle foreach kept as fallback). Each call registers
        //     three things: the concrete skill, its IOrchestratorSkill binding,
        //     and an OrchestratorSkillIntent adapter for the IIntent registry.
        services.AddOrchestratorSkillIntent<ChordInfoSkill>();
        services.AddOrchestratorSkillIntent<ScaleInfoSkill>();
        services.AddOrchestratorSkillIntent<ModesSkill>();
        services.AddOrchestratorSkillIntent<IntervalSkill>();
        services.AddOrchestratorSkillIntent<FretSpanSkill>();
        services.AddOrchestratorSkillIntent<ChordSubstitutionSkill>();
        services.AddOrchestratorSkillIntent<BeginnerChordsSkill>();
        services.AddOrchestratorSkillIntent<ProgressionMoodSkill>();

        // Catalog skills graduated 2026-05-05 — body lives in SKILL.md,
        // C# class supplies routing metadata + emits the body verbatim.
        services.AddOrchestratorSkillIntent<CircleOfFifthsSkill>();
        services.AddOrchestratorSkillIntent<PracticeRoutineSkill>();
        services.AddOrchestratorSkillIntent<GenreEssentialsSkill>();
        services.AddOrchestratorSkillIntent<WhatCanYouDoSkill>();

        // Tool-driven skills graduated 2026-05-06 — Path B (LLM-in-the-loop)
        // canaries for the DSL-eval pattern. Each wrapper owns routing
        // metadata; ExecuteAsync delegates to a SkillMdDrivenSkill that
        // dispatches ga_dsl_eval against the named closure. Adding new
        // skills follows the TransposeSkill/DiatonicChordsSkill template:
        // 1) skills/<name>/SKILL.md, 2) <Name>Skill.cs, 3) register here.
        services.AddOrchestratorSkillIntent<TransposeSkill>();
        services.AddOrchestratorSkillIntent<CommonTonesSkill>();
        services.AddOrchestratorSkillIntent<DiatonicChordsSkill>();

        // Domain-backed key-arithmetic skill (Tier 1 of 2026-05-13 plan).
        // Built 2026-05-13 to close "parallel minor of C major" corpus failure.
        services.AddOrchestratorSkillIntent<RelativeKeySkill>();

        // Deterministic theory-comparison skill. Built 2026-05-16 to close
        // "What is the difference between major and minor" — that prompt
        // was being semantically misrouted to RelativeKeySkill, returning
        // 0.1 confidence, then timing out at the Ollama fallback. This
        // skill's description scores higher for "difference / compare /
        // vs" queries so the semantic router picks it first.
        services.AddOrchestratorSkillIntent<TheoryComparisonSkill>();

        // Domain-backed set-theory equivalence skill — deterministic Y/N for
        // "are two PC-sets equivalent under T / I / TI" via SetClass prime
        // form. Built 2026-05-13 to close the pitch-class equivalence corpus
        // failure where LLM wording was too variable for substring asserts.
        services.AddOrchestratorSkillIntent<SetTheoryEquivalenceSkill>();

        // Domain-backed capo skill — pure semitone arithmetic for
        // sounding↔shape conversion at a given capo fret. Built 2026-05-14 to
        // close BACKLOG dealbreaker #3.
        services.AddOrchestratorSkillIntent<CapoSkill>();

        // Domain-backed voice-leading skill — optimal pitch-class assignment
        // between two chords via exhaustive permutation. Built 2026-05-14 to
        // close BACKLOG dealbreaker #4.
        services.AddOrchestratorSkillIntent<VoiceLeadingSkill>();

        // Domain-backed alternate-tunings skill — tuning-table lookup with
        // string-by-string interval analysis. Built 2026-05-14 to close
        // BACKLOG dealbreaker #2.
        services.AddOrchestratorSkillIntent<AlternateTuningsSkill>();

        // Grothendieck stolen-from-demo bundle (2026-05-14):
        // Five domain-backed skills that surface the same in-process
        // GrothendieckService used by /test/grothendieck-dsl. ICV math,
        // delta, neighbors, shortest path, plus the F# DSL parser.
        services.AddOrchestratorSkillIntent<IntervalClassVectorSkill>();
        services.AddOrchestratorSkillIntent<GrothendieckDeltaSkill>();
        services.AddOrchestratorSkillIntent<IcvNeighborsSkill>();
        services.AddOrchestratorSkillIntent<IcvShortestPathSkill>();
        services.AddOrchestratorSkillIntent<GrothendieckParseSkill>();

        // ChordVoicingsSkill (2026-05-16) — chat-layer wrapper over the
        // OPTIC-K voicing-search pipeline that already powers VoicingAgent.
        // Closes the "show me voicings for Cmaj7" gap in chatbot routing.
        services.AddOrchestratorSkillIntent<ChordVoicingsSkill>();

        // ImprovisationSkill (2026-05-16, #219) — "what scale can I use to
        // solo over X" — single-chord chord-scale recommendations from a
        // canonical quality→scales mapping. Pure domain compute, no LLM.
        services.AddOrchestratorSkillIntent<ImprovisationSkill>();

        // Skills using IChatClient are Scoped (IChatClient lifetime is Scoped).
        services.AddOrchestratorSkillIntent<KeyIdentificationSkill>(ServiceLifetime.Scoped);
        services.AddOrchestratorSkillIntent<ProgressionCompletionSkill>(ServiceLifetime.Scoped);

        // Durable-memory writer (2026-05-11) — first explicit "remember this"
        // skill. Emits a MemoryWriteRequest on AgentResponse.Data; the new
        // MemoryWriteHook below picks it up on OnResponseSent and writes via
        // the caller's SessionId. Closes the loop on EnrichOnRetrieve — until
        // this skill, MemoryStore had no fact/preference/focus writer at all
        // (transcript turns post-PR #174 go to ChatTranscriptStore).
        services.AddOrchestratorSkillIntent<RememberThisSkill>();

        // ── Persistent memory ────────────────────────────────────────────────
        // Construct via factory so we can wire ILogger<MemoryStore> for the
        // Load() IO-error surfacing introduced in PR #157 review rel-001.
        // Without the logger the legacy parameterless ctor swallows
        // permission-denied / disk-full silently — which historically looked
        // identical to "memory forgot everything between restarts."
        //
        // Embedder (PR after #195): if the host registered an
        // IEmbeddingGenerator<string, Embedding<float>> (e.g. via
        // AddTextEmbeddings / OllamaEmbeddingGenerator), pass it through so
        // SearchHybridAsync's cosine layer actually runs. When the host
        // didn't register one, MemoryStore silently degrades to BM25-only
        // and emits a one-shot warning the first time SearchHybridAsync
        // is called — that's the operator's signal that the embedder
        // binding is missing.
        services.TryAddSingleton<MemoryStore>(sp =>
            new MemoryStore(
                sp.GetService<ILogger<MemoryStore>>() ?? NullLogger<MemoryStore>.Instance,
                sp.GetService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>()));

        // ── Transcript log (PR #173 Phase 1 + PR #174 Phase 2) ───────────────
        // Sibling to MemoryStore: holds per-turn chat content, not durable
        // memory. MemoryHook.OnResponseSent (post-#174) writes user +
        // assistant turns here so MemoryStore stays clean for durable
        // knowledge. Also exposes IOperatorTranscriptReader (renamed from
        // IChatTranscriptStore on 2026-05-12) so the memory curator and
        // similar offline tooling have a cross-session view — that
        // interface name explicitly carries the operator-only contract,
        // discouraging any chat-runtime caller from accidentally wiring it.
        services.TryAddSingleton<ChatTranscriptStore>(sp =>
            new ChatTranscriptStore(sp.GetService<ILogger<ChatTranscriptStore>>() ?? NullLogger<ChatTranscriptStore>.Instance));
        services.TryAddSingleton<IOperatorTranscriptReader>(sp =>
            sp.GetRequiredService<ChatTranscriptStore>());

        // ── Hooks (execute in registration order at each lifecycle point) ─────
        services.AddSingleton<IChatHook, PromptSanitizationHook>();
        services.AddSingleton<IChatHook, MemoryHook>();
        // MemoryWriteHook (2026-05-11) — picks up MemoryWriteRequest emitted
        // by RememberThisSkill on AgentResponse.Data and persists with the
        // caller's SessionId. Registered AFTER MemoryHook so MemoryHook's
        // transcript-write fires first (transcript = ground truth of what
        // was said), then MemoryWriteHook persists the structured durable
        // claim. Both are idempotent; order is for log ordering, not safety.
        services.AddSingleton<IChatHook, MemoryWriteHook>();
        services.AddSingleton<IChatHook, ObservabilityHook>();
    }

    /// <summary>
    /// MCP tool types from GaMcpServer that should be included in the in-process
    /// MCP server assembled by <see cref="ChatPluginHost"/> (wired in Phase 3).
    /// Referenced by type name to avoid a direct project dependency on GaMcpServer.
    /// </summary>
    public IReadOnlyList<Type> McpToolTypes =>
    [
        typeof(MemoryMcpTools),
        // Domain-compute tools exposed via MCP for the SkillMdDrivenSkill path.
        // Each class wraps one logical topic (interval, scale, etc.); methods
        // within the class are individual operations exposed as MCP tools with
        // the `ga_<topic>_<verb>` wire-name convention. See
        // docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
        // §"Porting policy: catalog vs. computation skills".
        typeof(IntervalMcpTools),
        typeof(ScaleMcpTools),
        typeof(ChordMcpTools),
        typeof(FretSpanMcpTools),
        typeof(ChordSubstitutionMcpTools),
        typeof(KeyIdentificationMcpTools),
        // ga_dsl_eval — bridges chatbot to F# closure registry. Exposes
        // Domain-category closures only; Pipeline / Io / Agent excluded
        // for safety. Contract: docs/contracts/2026-05-06-ga-dsl-eval-contract.md
        // (v0.1, 2026-05-06).
        typeof(DslEvalMcpTools),

        // ga_sampling_demo — spike (#79) proving the MCP sampling
        // pattern works from a GA tool: the tool asks the connected
        // client (Claude Code, chatbot orchestrator, …) to do the LLM
        // call instead of opening its own IChatClient. First step
        // toward replacing IChatClientFactory in SkillMdDrivenSkill.
        typeof(SamplingDemoMcpTool),
    ];
}
