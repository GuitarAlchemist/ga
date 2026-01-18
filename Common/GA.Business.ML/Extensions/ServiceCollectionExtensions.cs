namespace GA.Business.ML.Extensions;

using Abstractions;
using Configuration;
using Embeddings;
using Embeddings.Services;
using Musical.Explanation;
using Text.HuggingFace;
using Text.Ollama;
using Text.Onnx;
using Text.Internal;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AI services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds AI services to the service collection
        /// </summary>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddGuitarAlchemistAI()
        {
            // Register embedding services
            services.AddTransient<EmbeddingServiceFactory>();
            services.AddTransient<OnnxEmbeddingService>();
            services.AddTransient<OllamaEmbeddingService>();
            services.AddTransient<HuggingFaceEmbeddingService>();
            services.AddTransient<BatchOllamaEmbeddingService>();

            // Register Musical embedding services
            services.AddTransient<IdentityVectorService>();
            services.AddTransient<TheoryVectorService>();
            services.AddTransient<MorphologyVectorService>();
            services.AddTransient<ContextVectorService>();
            services.AddTransient<SymbolicVectorService>();
            services.AddSingleton<PhaseSphereService>();
            services.AddTransient<MusicalEmbeddingGenerator>();
            services.AddTransient<VoicingExplanationService>();

            // Register Tab Analysis Services
            services.AddTransient<Tabs.TabTokenizer>();
            services.AddTransient<Tabs.TabToPitchConverter>();
            services.AddTransient<Tabs.TabAnalysisService>();

            // Register Wavelet & Motion Services
            services.AddTransient<Wavelets.WaveletTransformService>();
            services.AddTransient<Wavelets.ProgressionSignalService>();
            services.AddTransient<Wavelets.ProgressionEmbeddingService>();
            services.AddTransient<Wavelets.StyleClassifierService>();

            // Register Musical Analysis Services
            services.AddTransient<Musical.Analysis.CadenceDetector>();

            // Register Retrieval & Navigation Services
            services.AddTransient<Retrieval.StyleProfileService>();
            services.AddTransient<Retrieval.NextChordSuggestionService>();
            services.AddTransient<Retrieval.ModulationAnalyzer>();

            // Register Tab Realization & Optimization Services
            services.AddTransient<Core.Fretboard.Analysis.IMlNaturalnessRanker, Naturalness.MlNaturalnessRanker>();
            services.AddTransient<Tabs.AdvancedTabSolver>();
            services.AddTransient<Tabs.VoiceLeadingOptimizer>();
            services.AddTransient<Tabs.AlternativeFingeringService>();

            // Register Harvesting & Corpus Services
            services.AddTransient<Tabs.ProgressionHarvestingService>();

            // Register Benchmarking Services
            services.AddTransient<AI.Benchmarks.BenchmarkRunner>();
            services.AddTransient<GA.Business.Core.AI.Benchmarks.IBenchmark, AI.Benchmarks.VoicingQualityBenchmark>();

            return services;
        }

        /// <summary>
        /// Adds embedding services with configuration
        /// </summary>
        /// <param name="configureOptions">Configuration action</param>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddEmbeddingServices(Action<EmbeddingServiceSettings>? configureOptions = null)
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
}
