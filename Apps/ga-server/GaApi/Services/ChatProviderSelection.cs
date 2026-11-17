namespace GaApi.Services;

using Configuration;

/// <summary>
///     The chat provider settings GaApi reads once, at registration. The singleton
///     <see cref="Microsoft.Extensions.AI.IChatClient" /> and <see cref="ChatProviderAvailability" /> both use
///     this snapshot, so status always describes the provider and model that requests use.
/// </summary>
/// <param name="Provider"><c>AI:ChatProvider</c>: <c>claude</c>, <c>docker</c>, anything else means Ollama.</param>
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
    public bool IsClaude => string.Equals(Provider, "claude", StringComparison.OrdinalIgnoreCase);

    public bool IsDocker => string.Equals(Provider, "docker", StringComparison.OrdinalIgnoreCase);

    public static ChatProviderSelection FromConfiguration(IConfiguration configuration)
    {
        var chatbot = configuration.GetSection(ChatbotOptions.SectionName).Get<ChatbotOptions>() ?? new ChatbotOptions();
        return new(
            configuration["AI:ChatProvider"] ?? "ollama",
            configuration["AI:CascadeProvider"],
            chatbot.Model ?? configuration["Ollama:ChatModel"] ?? "llama3.2:3b",
            TimeSpan.FromSeconds(Math.Max(5, chatbot.StreamTimeoutSeconds)),
            configuration["DockerModelRunner:ChatModel"] ?? "docker.io/ai/llama3.2:latest",
            (configuration["DockerModelRunner:BaseUrl"] ?? "http://localhost:12434/v1").TrimEnd('/'));
    }
}
