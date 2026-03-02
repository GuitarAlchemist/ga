namespace GA.Business.ML.Extensions;

using Agents;
using AI.Benchmarks;
using Configuration;
using Embeddings;
using Embeddings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Musical.Analysis;
using Naturalness;
using Retrieval;
using Tabs;
using Text.HuggingFace;
using Text.Ollama;
using Text.Onnx;
using Wavelets;
using GA.Domain.Services.Fretboard.Analysis;
using GA.Business.ML.Naturalness;

/// <summary>
///     Extension methods for configuring AI services in the dependency injection container
/// </summary>
public static class MlServiceCollectionExtensions
{
    /// <summary>
    ///     Add all Guitar Alchemist AI/ML services.
    /// </summary>
    public static IServiceCollection AddGuitarAlchemistAI(this IServiceCollection services)
    {
        // Register musical domain knowledge
        services.AddSingleton<GA.Domain.Core.Instruments.Tuning>(GA.Domain.Core.Instruments.Tuning.Default);
        services.AddSingleton<GA.Domain.Services.Fretboard.Analysis.FretboardPositionMapper>();
        services.AddSingleton<GA.Domain.Services.Fretboard.Analysis.PhysicalCostService>();
        services.AddSingleton<Musical.Enrichment.ModalFlavorService>();
        services.AddSingleton<Musical.Enrichment.ModalCharacteristicIntervalService>(_ => Musical.Enrichment.ModalCharacteristicIntervalService.Instance);

        // Register embedding services
        services.AddTransient<EmbeddingServiceFactory>();
        services.AddSingleton<IMlNaturalnessRanker, MlNaturalnessRanker>();
        services.AddTransient<OnnxEmbeddingService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<EmbeddingServiceSettings>>().Value;
            var options = new OnnxEmbeddingOptions
            {
                ModelPath = settings.ModelPath ?? throw new InvalidOperationException("ModelPath not configured")
            };
            return new OnnxEmbeddingService(
                options,
                sp.GetService<ILogger<OnnxEmbeddingService>>());
        });
        services.AddTransient<OllamaEmbeddingService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<EmbeddingServiceSettings>>().Value;
            return new OllamaEmbeddingService(
                sp.GetRequiredService<HttpClient>(),
                settings.OllamaHost ?? "http://localhost:11434",
                settings.ModelName ?? "nomic-embed-text");
        });
        services.AddTransient<HuggingFaceEmbeddingService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<EmbeddingServiceSettings>>().Value;
            return new HuggingFaceEmbeddingService(
                sp.GetRequiredService<HttpClient>(),
                settings.ApiKey ?? string.Empty,
                settings.ModelName ?? "sentence-transformers/all-MiniLM-L6-v2");
        });
        services.AddTransient<BatchOllamaEmbeddingService>();

        // Register Musical embedding services
        services.AddTransient<IdentityVectorService>();
        services.AddTransient<TheoryVectorService>();
        services.AddTransient<MorphologyVectorService>();
        services.AddTransient<ContextVectorService>();
        services.AddTransient<SymbolicVectorService>();
        services.AddTransient<ModalVectorService>();
        services.AddSingleton<PhaseSphereService>();
        services.AddTransient<MusicalEmbeddingGenerator>();
        services.AddTransient<Abstractions.IEmbeddingGenerator, MusicalEmbeddingGenerator>();
        services.AddTransient<VoicingExplanationService>();

        // Register Agents
        services.AddTransient<TabAgent>();
        services.AddTransient<TheoryAgent>();
        services.AddTransient<TechniqueAgent>();
        services.AddTransient<ComposerAgent>();
        services.AddTransient<CriticAgent>();

        // Register agents as the base type for SemanticRouter injection
        services.AddTransient<GuitarAlchemistAgentBase>(sp => sp.GetRequiredService<TabAgent>());
        services.AddTransient<GuitarAlchemistAgentBase>(sp => sp.GetRequiredService<TheoryAgent>());
        services.AddTransient<GuitarAlchemistAgentBase>(sp => sp.GetRequiredService<TechniqueAgent>());
        services.AddTransient<GuitarAlchemistAgentBase>(sp => sp.GetRequiredService<ComposerAgent>());
        services.AddTransient<GuitarAlchemistAgentBase>(sp => sp.GetRequiredService<CriticAgent>());

        // Register Semantic Router
        services.AddScoped<SemanticRouter>();

        // Register Tab Analysis Services
        services.AddTransient<TabTokenizer>();
        services.AddTransient<TabToPitchConverter>();
        services.AddTransient<TabAnalysisService>();

        // Register Wavelet & Motion Services
        services.AddTransient<WaveletTransformService>();
        services.AddTransient<ProgressionSignalService>();
        services.AddTransient<ProgressionEmbeddingService>();
        services.AddTransient<StyleClassifierService>();

        // Register Musical Analysis Services
        services.AddTransient<CadenceDetector>();

        // Register Retrieval & Navigation Services
        services.TryAddSingleton<GA.Business.ML.Embeddings.IVectorIndex>(new GA.Business.ML.Embeddings.FileBasedVectorIndex("voicing_index.json"));
        services.AddScoped<SpectralRetrievalService>();
        services.AddTransient<StyleProfileService>();
        services.AddTransient<NextChordSuggestionService>();
        services.AddTransient<ModulationAnalyzer>();

        // Register Tab Realization & Optimization Services
        services.AddTransient<MlNaturalnessRanker>();
        services.AddTransient<AdvancedTabSolver>();
        services.AddTransient<VoiceLeadingOptimizer>();
        services.AddTransient<AlternativeFingeringService>();

        // Register Harvesting & Corpus Services
        services.AddTransient<ProgressionHarvestingService>();

        // Register Benchmarking Services
        services.AddTransient<BenchmarkRunner>();
        // services.AddTransient<GA.Domain.Services.AI.Benchmarks.IBenchmark, AI.Benchmarks.VoicingQualityBenchmark>();

        return services;
    }

    /// <summary>
    ///     Configures embedding services settings.
    /// </summary>
    public static IServiceCollection AddEmbeddingServices(this IServiceCollection services, Action<EmbeddingServiceSettings>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddTransient<EmbeddingServiceFactory>();
        services.AddTransient<ITextEmbeddingService>(provider =>
            provider.GetRequiredService<EmbeddingServiceFactory>().CreateService());

        return services;
    }
}
