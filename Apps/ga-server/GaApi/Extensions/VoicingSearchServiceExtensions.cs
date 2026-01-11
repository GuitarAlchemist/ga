namespace GaApi.Extensions;

using GaApi.Services;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.AI.Services.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension helpers for registering the voicing search stack.
/// </summary>
public static class VoicingSearchServiceExtensions
{
    /// <summary>
    /// Registers indexing, vector search strategies, and the hosted initialization workflow.
    /// Supports falling back to CPU search for faster integration tests or when GPU is unavailable.
    /// </summary>
    public static IServiceCollection AddVoicingSearchServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMemoryCache();
        services.AddSingleton<VoicingIndexingService>();
        
        // Strategy selection logic
        var preferredStrategy = configuration.GetValue<string>("VoicingSearch:SearchStrategy", "Auto");
        
        services.AddSingleton<IVoicingSearchStrategy>(sp => 
        {
            var logger = sp.GetRequiredService<ILogger<EnhancedVoicingSearchService>>();
            
            if (preferredStrategy.Equals("CPU", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Enabling CPU-based in-memory search (requested by config)");
                return new CpuVoicingSearchStrategy();
            }

            try 
            {
                var gpuStrategy = new GpuVoicingSearchStrategy();
                if (gpuStrategy.IsAvailable)
                {
                    logger.LogInformation("Enabling GPU-accelerated voicing search");
                    return gpuStrategy;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to initialize GPU strategy, falling back to CPU");
            }

            logger.LogInformation("Falling back to CPU-based parallel search strategy");
            return new CpuVoicingSearchStrategy();
        });

        services.AddSingleton<EnhancedVoicingSearchService>();
        services.AddSingleton<IVoicingEmbeddingCache, VoicingEmbeddingCache>();
        services.AddHostedService<VoicingIndexInitializationService>();

        return services;
    }
}
