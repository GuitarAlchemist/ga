namespace GaApi.Services;

/// <summary>
///     Availability checks for the providers selectable through <c>AI:ChatProvider</c>:
///     <list type="bullet">
///         <item><c>ollama</c> (default): <c>GET /api/tags</c> lists a model whose name starts with the configured model family.</item>
///         <item><c>docker</c>: <c>GET /models</c> on Docker Model Runner mentions the configured model family.</item>
///         <item><c>claude</c>: always available; a missing API key already fails at startup.</item>
///     </list>
///     With <c>AI:CascadeProvider</c> set, this reports the primary provider only.
/// </summary>
public sealed class ChatProviderAvailability(
    IHttpClientFactory httpClientFactory,
    ChatProviderSelection selection) : IChatProviderAvailability
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (selection.IsClaude)
            return true;

        try
        {
            return selection.IsDocker
                ? await IsDockerModelRunnerAvailableAsync(cancellationToken)
                : await IsOllamaAvailableAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Unreachable, misconfigured, timed out or cancelled: the provider is not available.
            return false;
        }
    }

    private async Task<bool> IsOllamaAvailableAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("Ollama");
        // Same bound as the former OllamaChatService client (Chatbot:StreamTimeoutSeconds).
        client.Timeout = selection.OllamaTimeout;
        using var response = await client.GetAsync("/api/tags", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(cancellationToken);
        var family = selection.OllamaModel.Split(':')[0];
        return result?.Models?.Any(m => m.Name.StartsWith(family)) == true;
    }

    private async Task<bool> IsDockerModelRunnerAvailableAsync(CancellationToken cancellationToken)
    {
        // The named client keeps its 60 s timeout. Absolute URL: a leading-slash relative
        // path would drop the /v1 segment of the base address.
        using var client = httpClientFactory.CreateClient("DockerModelRunner");
        using var response = await client.GetAsync($"{selection.DockerBaseUrl}/models", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return false;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return json.Contains(selection.DockerModel.Split(':')[0], StringComparison.OrdinalIgnoreCase);
    }
}
