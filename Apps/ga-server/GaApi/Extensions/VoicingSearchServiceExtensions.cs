namespace GaApi.Extensions;

using GA.Business.ML.Search;
using Services;
using GA.Business.ML.Text.Internal; // Assuming this is needed, keeping original usings except the bad one

/// <summary>
///     Extension helpers for registering the voicing search stack.
/// </summary>
public static class VoicingSearchServiceExtensions
{
    /// <summary>
    ///     Registers indexing, vector search strategies, and the hosted initialization workflow.
    ///     Supports falling back to CPU search for faster integration tests or when GPU is unavailable.
    /// </summary>
    public static IServiceCollection AddVoicingSearchServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMemoryCache();
        services.AddSingleton<VoicingIndexingService>();

        // Strategy selection logic
        var preferredStrategy = configuration.GetValue<string>("VoicingSearch:SearchStrategy", "Auto");

        // Register OptickSearchStrategy as a concrete singleton so the DI container takes
        // disposal ownership of its mmap + view accessor. The IVoicingSearchStrategy binding
        // below resolves to this same instance via GetRequiredService.
        var opticRegistrationPath = configuration["VoicingSearch:OpticIndexPath"]
            ?? System.IO.Path.Combine(AppContext.BaseDirectory, "state", "voicings", "optick.index");
        if (File.Exists(opticRegistrationPath))
        {
            services.AddSingleton(sp => new OptickSearchStrategy(opticRegistrationPath));
        }

        services.AddSingleton<IVoicingSearchStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EnhancedVoicingSearchService>>();

            // OPTK is the default when an index file is present. It reads pre-computed
            // OPTIC-K musical embeddings directly — query vectors must come from
            // MusicalQueryEncoder rather than Ollama text embeddings.
            if (!preferredStrategy.Equals("CPU", StringComparison.OrdinalIgnoreCase) &&
                !preferredStrategy.Equals("GPU", StringComparison.OrdinalIgnoreCase))
            {
                var indexPath = configuration["VoicingSearch:OpticIndexPath"]
                                ?? System.IO.Path.Combine(AppContext.BaseDirectory, "state", "voicings", "optick.index");
                if (File.Exists(indexPath))
                {
                    try
                    {
                        // Resolve the concrete singleton so the DI container owns disposal —
                        // the Optick reader holds an mmap + view accessor that must be released
                        // at host shutdown. See OptickStrategyRegistration below.
                        var strategy = sp.GetRequiredService<OptickSearchStrategy>();
                        logger.LogInformation("Enabling OPTK voicing search from {Path}", indexPath);
                        return strategy;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "OPTK index at {Path} failed to open; falling back to GPU/CPU strategies", indexPath);
                    }
                }
                else
                {
                    logger.LogInformation(
                        "OPTK index not found at {Path}; falling back to GPU/CPU strategies", indexPath);
                }
            }

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
                    logger.LogInformation(
                        "Enabling GPU-accelerated voicing search accelerator={Accelerator} realGpu={RealGpu}",
                        gpuStrategy.AcceleratorTypeName, gpuStrategy.IsRealGpu);
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

        // Contextual Chord & Voicing Services
        services.AddSingleton<ContextualChordService>();
        services.AddSingleton<VoicingFilterService>();

        return services;
    }
}
