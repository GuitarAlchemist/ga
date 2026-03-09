namespace GA.Business.Core.Orchestration.Plugins;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
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

        // ── Orchestrator skills (checked before LLM routing, first match wins) ─
        // Pure-domain skills are Singleton (stateless, no scoped dependencies).
        services.AddSingleton<IOrchestratorSkill, ScaleInfoSkill>();
        services.AddSingleton<IOrchestratorSkill, FretSpanSkill>();
        services.AddSingleton<IOrchestratorSkill, ChordSubstitutionSkill>();

        // Skills using IChatClient are Scoped (IChatClient lifetime is Scoped).
        services.AddScoped<IOrchestratorSkill, KeyIdentificationSkill>();
        services.AddScoped<IOrchestratorSkill, ProgressionCompletionSkill>();

        // ── Hooks (execute in registration order at each lifecycle point) ─────
        services.AddSingleton<IChatHook, PromptSanitizationHook>();
        services.AddSingleton<IChatHook, ObservabilityHook>();
    }

    /// <summary>
    /// MCP tool types from GaMcpServer that should be included in the in-process
    /// MCP server assembled by <see cref="ChatPluginHost"/> (wired in Phase 3).
    /// Referenced by type name to avoid a direct project dependency on GaMcpServer.
    /// </summary>
    public IReadOnlyList<Type> McpToolTypes => [];  // populated in Phase 3
}
