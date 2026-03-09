namespace GaApi.Services;

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// <see cref="IChatService"/> backed by Docker Model Runner's OpenAI-compatible
/// <c>/v1/chat/completions</c> endpoint (port 12434).
/// </summary>
/// <remarks>
/// Use when Docker Desktop's built-in model runner is preferred over a separate Ollama install.
/// Activate via <c>AI:ChatProvider = "docker"</c> in appsettings.
/// </remarks>
public sealed class DockerModelRunnerChatService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DockerModelRunnerChatService> logger) : IChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = httpClientFactory.CreateClient("DockerModelRunner");
    private readonly string _model = configuration["DockerModelRunner:ChatModel"] ?? "docker.io/ai/llama3.2:latest";

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var messages = BuildMessages(userMessage, conversationHistory, systemPrompt);
        var body = new ChatCompletionRequest(_model, messages, Stream: true);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var payload = line.Substring("data: ".Length);
            if (payload == "[DONE]") break;

            StreamChunk? chunk;
            try { chunk = JsonSerializer.Deserialize<StreamChunk>(payload, JsonOptions); }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Unparseable Docker Model Runner chunk: {Payload}", payload);
                continue;
            }

            var text = chunk?.Choices?[0].Delta?.Content;
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    public async Task<string> ChatAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in ChatStreamAsync(userMessage, conversationHistory, systemPrompt, cancellationToken))
            sb.Append(chunk);
        return sb.ToString();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var resp = await _http.GetAsync("/models", cancellationToken);
            if (!resp.IsSuccessStatusCode) return false;
            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            return json.Contains(_model.Split(':')[0], StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<OpenAIMessage> BuildMessages(
        string userMessage,
        List<ChatMessage>? history,
        string? systemPrompt)
    {
        var messages = new List<OpenAIMessage>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new("system", systemPrompt));
        if (history is not null)
            messages.AddRange(history.Select(m => new OpenAIMessage(m.Role, m.Content)));
        messages.Add(new("user", userMessage));
        return messages;
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private record ChatCompletionRequest(
        [property: JsonPropertyName("model")]    string Model,
        [property: JsonPropertyName("messages")] List<OpenAIMessage> Messages,
        [property: JsonPropertyName("stream")]   bool Stream);

    private record OpenAIMessage(
        [property: JsonPropertyName("role")]    string Role,
        [property: JsonPropertyName("content")] string Content);

    private record StreamChunk(
        [property: JsonPropertyName("choices")] List<Choice>? Choices);

    private record Choice(
        [property: JsonPropertyName("delta")] Delta? Delta);

    private record Delta(
        [property: JsonPropertyName("content")] string? Content);
}
