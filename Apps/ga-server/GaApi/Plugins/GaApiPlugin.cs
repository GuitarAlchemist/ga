namespace GaApi.Plugins;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Plugins;
using GaApi.Services;
using GaApi.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// GaApi application plugin — registers GaApi-specific orchestrator skills
/// that depend on app-level services (e.g. <see cref="VoicingComfortSkill"/>).
/// Discovered automatically by <see cref="ChatPluginHost"/> via <see cref="ChatPluginAttribute"/>.
/// </summary>
[ChatPlugin]
public sealed class GaApiPlugin : IChatPlugin
{
    public string Name    => "GaApi";
    public string Version => "1.0";

    public void Register(IServiceCollection services)
    {
        // VoicingComfortService depends on VoicingFilterService (registered as Singleton in Program.cs).
        // TryAdd avoids double-registration if the app also registers it directly.
        services.TryAddScoped<VoicingComfortService>();

        // VoicingComfortSkill — Scoped to be safe with transitive Scoped dependencies.
        services.AddScoped<IOrchestratorSkill, VoicingComfortSkill>();
    }
}
