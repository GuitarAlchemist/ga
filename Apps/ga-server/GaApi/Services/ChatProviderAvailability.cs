namespace GaApi.Services;

using Configuration;
using Microsoft.Extensions.Options;

/// <summary>
///     Availability checks for the providers selectable through <c>AI:ChatProvider</c>:
///     <list type="bullet">
///         <item><c>ollama</c> (default): <c>GET /api/tags</c> lists a model whose name starts with the configured model family.</item>
///         <item><c>docker</c>: <c>GET /models</c> on Docker Model Runner mentions the configured model family.</item>
///         <item><c>claude</c>: always available; a missing API key already fails at startup.</item>
///     </list>
/// </summary>
public sealed class ChatProviderAvailability(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IOptionsMonitor<ChatbotOptions> chatbotOptions) : IChatProviderAvailability
{
    // Read once, like the singleton IChatClient: status must describe the provider and model
    // that requests actually use, even if configuration reloads later.
    private readonly string _provider = configuration["AI:ChatProvider"] ?? "ollama";
    private readonly string _ollamaModel = OllamaChatModel(chatbotOptions.CurrentValue, configuration);
    private readonly string _dockerModel = configuration["DockerModelRunner:ChatModel"] ?? "docker.io/ai/llama3.2:latest";
    private readonly string _dockerBaseUrl = (configuration["DockerModelRunner:BaseUrl"] ?? "http://localhost:12434/v1").TrimEnd('/');

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var provider = _provider;
        if (string.Equals(provider, "claude", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            return string.Equals(provider, "docker", StringComparison.OrdinalIgnoreCase)
                ? await IsDockerModelRunnerAvailableAsync(cancellationToken)
                : await IsOllamaAvailableAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Unreachable, misconfigured or cancelled: the provider is not available.
            return false;
        }
    }

    private async Task<bool> IsOllamaAvailableAsync(CancellationToken cancellationToken)
    {
        var model = _ollamaModel;
        using var response = await httpClientFactory.CreateClient("Ollama").GetAsync("/api/tags", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(cancellationToken);
        return result?.Models?.Any(m => m.Name.StartsWith(model.Split(':')[0])) == true;
    }

    private async Task<bool> IsDockerModelRunnerAvailableAsync(CancellationToken cancellationToken)
    {
        var model = _dockerModel;
        // Absolute URL: a leading-slash relative path would drop the /v1 segment of the base address.
        using var response = await httpClientFactory.CreateClient("DockerModelRunner").GetAsync($"{_dockerBaseUrl}/models", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return false;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return json.Contains(model.Split(':')[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ollama chat model: <c>Chatbot:Model</c>, then <c>Ollama:ChatModel</c>, then <c>llama3.2:3b</c>.</summary>
    internal static string OllamaChatModel(ChatbotOptions options, IConfiguration configuration) =>
        options.Model ?? configuration["Ollama:ChatModel"] ?? "llama3.2:3b";
}
