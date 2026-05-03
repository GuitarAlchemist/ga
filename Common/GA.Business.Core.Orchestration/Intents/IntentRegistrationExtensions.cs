namespace GA.Business.Core.Orchestration.Intents;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI helpers for registering <see cref="IOrchestratorSkill"/> implementations
/// alongside their <see cref="OrchestratorSkillIntent"/> adapter so they
/// participate in <see cref="SemanticIntentRouter"/> dispatch.
/// </summary>
public static class IntentRegistrationExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TSkill"/> in the container three ways:
    /// (a) as itself (for direct resolution),
    /// (b) as <see cref="IOrchestratorSkill"/> (for the legacy <c>CanHandle</c>
    ///     foreach in <c>ProductionOrchestrator</c>),
    /// (c) as <see cref="IIntent"/> via <see cref="OrchestratorSkillIntent"/>
    ///     (for <see cref="SemanticIntentRouter"/> dispatch).
    /// </summary>
    /// <param name="lifetime">Match the skill's natural lifetime — Singleton
    /// for pure-domain skills, Scoped for skills that depend on
    /// <c>IChatClient</c> (which is Scoped).</param>
    public static IServiceCollection AddOrchestratorSkillIntent<TSkill>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TSkill : class, IOrchestratorSkill
    {
        services.Add(new ServiceDescriptor(typeof(TSkill), typeof(TSkill), lifetime));

        services.Add(new ServiceDescriptor(
            typeof(IOrchestratorSkill),
            sp => sp.GetRequiredService<TSkill>(),
            lifetime));

        services.Add(new ServiceDescriptor(
            typeof(IIntent),
            sp => new OrchestratorSkillIntent(sp.GetRequiredService<TSkill>()),
            lifetime));

        return services;
    }
}
