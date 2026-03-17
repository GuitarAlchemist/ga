namespace GA.Business.Core.Orchestration.Extensions;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Clients;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// DI registration for the chatbot orchestration stack.
/// Call <see cref="AddChatbotOrchestration"/> in both GaChatbot and GaApi.
/// </summary>
public static class ChatbotOrchestrationExtensions
{
    /// <summary>
    /// Registers all chatbot orchestration services.
    /// Skills, hooks, and domain services are discovered automatically via
    /// <see cref="ChatPluginHost.AddChatPluginHost"/> — no manual skill registration needed.
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

        // Shared Ollama HTTP client wrapper — single plumbing point for /api/generate calls
        services.AddSingleton<OllamaGenerateClient>();

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

        // Plugin host — discovers [ChatPlugin] implementations in loaded assemblies
        // and registers their skills, hooks, and domain services automatically.
        // GaPlugin (in GA.Business.Core.Orchestration) registers:
        //   Skills: ScaleInfoSkill, FretSpanSkill, ChordSubstitutionSkill, KeyIdentificationSkill
        //   Hooks:  PromptSanitizationHook, ObservabilityHook, GovernanceHook
        services.AddChatPluginHost();

        // MCP federation client — connects to TARS/ix MCP servers via stdio
        services.AddSingleton<FederationClient>();

        // PersonaLoader wired to base class (same pattern as MdRegistry in ChatPluginHost)
        services.AddSingleton<PersonaLoader>();

        // Conversation history — singleton in-memory store shared across scopes
        services.AddSingleton<ConversationHistoryStore>();

        // Cross-agent delegation coordinator
        services.AddScoped<IAgentCoordinator, AgentCoordinator>();

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
