namespace GA.Business.Core.Orchestration.Plugins;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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

        // ── Progress tracking ──────────────────────────────────────────────────
        services.TryAddSingleton<ProgressTracker>();

        // ── Orchestrator skills (checked before LLM routing, first match wins) ─
        // ORDERING MATTERS: first match wins. Preference-setting and quiz answers
        // must be checked before general quiz/domain skills.

        // 1. Session context updates — "I'm a beginner" must not route to agent
        services.AddScoped<IOrchestratorSkill, SessionContextSkill>();

        // 2. Quiz answer validation — active quiz answers take priority
        services.AddScoped<IOrchestratorSkill, QuizAnswerSkill>();

        // 3-9. Pure-domain skills are Singleton (stateless, no scoped dependencies).
        services.AddSingleton<IOrchestratorSkill, ScaleInfoSkill>();
        services.AddSingleton<IOrchestratorSkill, ModeExplorationSkill>();
        services.AddSingleton<IOrchestratorSkill, IntervalInfoSkill>();
        services.AddSingleton<IOrchestratorSkill, ChordExplanationSkill>();
        services.AddSingleton<IOrchestratorSkill, FretSpanSkill>();
        services.AddSingleton<IOrchestratorSkill, ChordSubstitutionSkill>();

        // 10-11. Quiz generation skills (scoped — need MemoryStore + session context)
        services.AddScoped<IOrchestratorSkill, IntervalQuizSkill>();
        services.AddScoped<IOrchestratorSkill, ChordQuizSkill>();

        // 12-13. Practice skills (scoped — need session context)
        services.AddScoped<IOrchestratorSkill, PracticeRoutineSkill>();
        services.AddScoped<IOrchestratorSkill, ScalePracticeSkill>();

        // 14. Progress reporting
        services.AddScoped<IOrchestratorSkill, ProgressSkill>();

        // 15-18. Skills using IChatClient are Scoped (IChatClient lifetime is Scoped).
        services.AddScoped<IOrchestratorSkill, KeyIdentificationSkill>();
        services.AddScoped<IOrchestratorSkill, ProgressionCompletionSkill>();
        services.AddScoped<IOrchestratorSkill, ProgressionSuggestionSkill>();
        services.AddScoped<IOrchestratorSkill, HarmonicAnalysisSkill>();

        // 19. ML skill (routes to ix ML pipeline via federation)
        services.AddScoped<IOrchestratorSkill, MusicMlSkill>();

        // ── Persistent memory ────────────────────────────────────────────────
        services.TryAddSingleton<MemoryStore>();

        // ── Persona loader (Demerzel governance personas) ────────────────────
        services.TryAddSingleton<PersonaLoader>();

        // ── Subagent manager ─────────────────────────────────────────────────
        services.AddSingleton<SubagentManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SubagentManager>>();
            // Default runner routes through the semantic router when available
            return new SubagentManager(
                async (req, ct) =>
                {
                    // Placeholder: actual routing through SemanticRouter is scoped
                    // and will be wired when subagent spawning is invoked at request scope
                    return new SubagentResult(Guid.Empty, true, $"Completed: {req.Goal}", [], TimeSpan.Zero);
                },
                logger);
        });

        // ── Hooks (execute in registration order at each lifecycle point) ─────
        services.AddSingleton<IChatHook, PromptSanitizationHook>();
        services.AddScoped<IChatHook, SessionContextHook>();
        services.AddSingleton<IChatHook, MemoryHook>();
        services.AddSingleton<IChatHook, GovernanceHook>();
        services.AddSingleton<IChatHook, ObservabilityHook>();
        services.AddSingleton<IChatHook, TraceBridgeHook>();
    }

    /// <summary>
    /// MCP tool types from GaMcpServer that should be included in the in-process
    /// MCP server assembled by <see cref="ChatPluginHost"/> (wired in Phase 3).
    /// Referenced by type name to avoid a direct project dependency on GaMcpServer.
    /// </summary>
    public IReadOnlyList<Type> McpToolTypes => [typeof(MemoryMcpTools), typeof(SubagentMcpTools)];
}
