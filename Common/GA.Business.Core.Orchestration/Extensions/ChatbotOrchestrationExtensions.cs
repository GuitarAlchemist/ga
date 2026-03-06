namespace GA.Business.Core.Orchestration.Extensions;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration for the chatbot orchestration stack.
/// Call <see cref="AddChatbotOrchestration"/> in both GaChatbot and GaApi.
/// </summary>
public static class ChatbotOrchestrationExtensions
{
    /// <summary>
    /// Registers all chatbot orchestration services.
    /// Reads <c>Ollama:Endpoint</c> and <c>Ollama:Model</c> lazily from the resolved <see cref="IConfiguration"/>.
    /// </summary>
    public static IServiceCollection AddChatbotOrchestration(this IServiceCollection services)
    {
        // HTTP client for Ollama — config resolved lazily at first use
        services.AddHttpClient("ollama", (sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var endpointRaw = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            if (!Uri.TryCreate(endpointRaw, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Ollama:Endpoint '{endpointRaw}' is not a valid http/https URI.");
            }

            client.BaseAddress = uri;
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Vector store — TryAdd so GaApi's FileBasedVectorIndex takes precedence if already registered
        services.TryAddSingleton<GA.Business.ML.Embeddings.IVectorIndex, GA.Business.ML.Embeddings.InMemoryVectorIndex>();

        // Core stateless services (Singleton — no scoped dependencies)
        services.AddSingleton<DomainMetadataPrompter>();
        services.AddSingleton<GroundedPromptBuilder>();
        services.AddSingleton<ResponseValidator>();
        services.AddSingleton<QueryUnderstandingService>();
        services.AddSingleton<TabPresentationService>();
        // NOTE: IGroundedNarrator is intentionally NOT registered here.
        // Each consuming app must register its own narrator implementation:
        //   - GaApi: services.AddScoped<IGroundedNarrator, OllamaGroundedNarrator>();
        //   - GaChatbot: services.AddExtensionsAINarrator();

        // Orchestrators — Scoped because they transitively depend on
        // SemanticRouter (Scoped) and SpectralRetrievalService (Scoped).
        services.AddScoped<SpectralRagOrchestrator>();
        services.AddScoped<TabAwareOrchestrator>();
        services.AddScoped<ProductionOrchestrator>();

        // Register as the standard interface so callers can depend on IHarmonicChatOrchestrator
        services.AddScoped<IHarmonicChatOrchestrator>(sp =>
            sp.GetRequiredService<ProductionOrchestrator>());

        return services;
    }
}
