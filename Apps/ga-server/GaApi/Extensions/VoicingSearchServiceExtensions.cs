namespace GaApi.Extensions;

using GaApi.Services;
using GA.Business.Core.Fretboard.Voicings.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension helpers for registering the voicing search stack.
/// </summary>
public static class VoicingSearchServiceExtensions
{
    /// <summary>
    /// Registers indexing, vector search strategies, and the hosted initialization workflow.
    /// </summary>
    public static IServiceCollection AddVoicingSearchServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<VoicingIndexingService>();
        services.AddSingleton<IVoicingSearchStrategy, IlgpuVoicingSearchStrategy>();
        services.AddSingleton<EnhancedVoicingSearchService>();
        services.AddHostedService<VoicingIndexInitializationService>();

        return services;
    }
}
