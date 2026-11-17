namespace GaApi.Services;

using Configuration;

/// <summary>
///     The chat provider settings GaApi reads once, at registration. The singleton
///     <see cref="Microsoft.Extensions.AI.IChatClient" /> and <see cref="ChatProviderAvailability" /> both use
///     this snapshot, so status always describes the provider and model that requests use.
/// </summary>
/// <param name="Provider">
///     <c>AI:ChatProvider</c>: <c>claude</c> or <c>anthropic</c>, <c>docker</c>, or <c>ollama</c> (the default).
///     Any other value fails at startup instead of silently running on Ollama.
/// </param>
/// <param name="CascadeProvider"><c>AI:CascadeProvider</c>: optional secondary used when the primary fails.</param>
/// <param name="OllamaModel"><c>Chatbot:Model</c>, then <c>Ollama:ChatModel</c>, then <c>llama3.2:3b</c>.</param>
/// <param name="OllamaTimeout"><c>Chatbot:StreamTimeoutSeconds</c>, at least 5 s.</param>
/// <param name="DockerModel"><c>DockerModelRunner:ChatModel</c>.</param>
/// <param name="DockerBaseUrl"><c>DockerModelRunner:BaseUrl</c> without a trailing slash.</param>
public sealed record ChatProviderSelection(
    string Provider,
    string? CascadeProvider,
    string OllamaModel,
    TimeSpan OllamaTimeout,
    string DockerModel,
    string DockerBaseUrl)
{
    // "anthropic" is the name the shared GA.Business.ML registration and GaChatbot.Api use.
    private static readonly string[] SupportedProviders = ["ollama", "docker", "claude", "anthropic"];

    public bool IsClaude =>
        string.Equals(Provider, "claude", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Provider, "anthropic", StringComparison.OrdinalIgnoreCase);

    public bool IsDocker => string.Equals(Provider, "docker", StringComparison.OrdinalIgnoreCase);

    public static ChatProviderSelection FromConfiguration(IConfiguration configuration)
    {
        var provider = configuration["AI:ChatProvider"] ?? "ollama";
        if (!SupportedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            // Configuration boundary: fail at startup rather than run on a provider nobody chose.
            throw new InvalidOperationException(
                $"AI:ChatProvider '{provider}' is not supported by GaApi. " +
                $"Use one of: {string.Join(", ", SupportedProviders)}.");
        }

        var chatbot = configuration.GetSection(ChatbotOptions.SectionName).Get<ChatbotOptions>() ?? new ChatbotOptions();
        return new(
            provider,
            configuration["AI:CascadeProvider"],
            chatbot.Model ?? configuration["Ollama:ChatModel"] ?? "llama3.2:3b",
            TimeSpan.FromSeconds(Math.Max(5, chatbot.StreamTimeoutSeconds)),
            configuration["DockerModelRunner:ChatModel"] ?? "docker.io/ai/llama3.2:latest",
            (configuration["DockerModelRunner:BaseUrl"] ?? "http://localhost:12434/v1").TrimEnd('/'));
    }
}
