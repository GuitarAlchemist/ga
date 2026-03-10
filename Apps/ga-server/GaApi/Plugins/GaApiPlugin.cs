namespace GaApi.Plugins;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Plugins;
using GaApi.Skills;
using Microsoft.Extensions.DependencyInjection;

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

    public void Register(IServiceCollection services) =>
        // VoicingComfortService is Transient (depends on VoicingFilterService which may be Scoped);
        // use Scoped to be safe with any transitive Scoped dependencies.
        services.AddScoped<IOrchestratorSkill, VoicingComfortSkill>();
}
