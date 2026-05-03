namespace GA.Business.Core.Orchestration.Plugins;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Mcp;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Skills using IChatClient are Scoped (IChatClient lifetime is Scoped).
        services.AddOrchestratorSkillIntent<KeyIdentificationSkill>(ServiceLifetime.Scoped);
        services.AddOrchestratorSkillIntent<ProgressionCompletionSkill>(ServiceLifetime.Scoped);

        // ── Persistent memory ────────────────────────────────────────────────
        services.TryAddSingleton<MemoryStore>();

        // ── Hooks (execute in registration order at each lifecycle point) ─────
        services.AddSingleton<IChatHook, PromptSanitizationHook>();
        services.AddSingleton<IChatHook, MemoryHook>();
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
    ];
}
