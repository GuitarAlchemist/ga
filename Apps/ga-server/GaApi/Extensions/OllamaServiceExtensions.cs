namespace GaApi.Extensions;

using System.Net;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Polly;
using Polly.Extensions.Http;
using Services;

/// <summary>
///     Extension methods for registering Ollama AI services
/// </summary>
public static class OllamaServiceExtensions
{
    /// <summary>
    ///     Registers all Ollama-related services including:
    ///     - HTTP client with retry and circuit breaker policies
    ///     - Embedding service for semantic search
    ///     - Chat service for conversational AI
    ///     - Semantic search service
    ///     - Semantic knowledge source
    ///     - Chatbot session orchestrator
    ///     - Guitar agent orchestrator
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOllamaServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Ollama HTTP client with custom timeout, retry, and circuit breaker
        services.AddHttpClient("Ollama", client =>
            {
                var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
                client.BaseAddress = new Uri(ollamaBaseUrl);
            })
            .AddPolicyHandler(CreateRetryPolicy())
            .AddPolicyHandler(CreateCircuitBreakerPolicy());

        // Register Ollama embedding service for semantic search
        services.AddSingleton<OllamaEmbeddingService>();
        services.AddSingleton<SemanticSearchService.IEmbeddingService>(sp =>
            sp.GetRequiredService<OllamaEmbeddingService>());

        // Register Ollama chat service for conversational AI
        services.AddSingleton<IOllamaChatService, OllamaChatService>();
        services.AddSingleton<OllamaChatClientAdapter>();
        services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredService<OllamaChatClientAdapter>());

        // Register semantic search service
        services.AddSingleton<SemanticSearchService>();

        // Register semantic knowledge source for RAG
        services.AddSingleton<SemanticKnowledgeSource>();
        services.AddSingleton<ISemanticKnowledgeSource>(sp =>
            sp.GetRequiredService<SemanticKnowledgeSource>());

        // Register chatbot session orchestrator
        services.AddScoped<ChatbotSessionOrchestrator>();

        // Register guitar agent orchestrator
        services.AddSingleton<GuitarAgentOrchestrator>();

        // Register OllamaSharp client for direct API access
        services.AddSingleton<IOllamaApiClient>(sp =>
        {
            var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            return new OllamaApiClient(ollamaBaseUrl);
        });

        return services;
    }

    /// <summary>
    ///     Creates a retry policy for transient HTTP failures
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts if needed
                });
    }

    /// <summary>
    ///     Creates a circuit breaker policy to prevent cascading failures
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromSeconds(30),
                (outcome, duration) =>
                {
                    // Log circuit breaker activation if needed
                },
                () =>
                {
                    // Log circuit breaker reset if needed
                });
    }
}
