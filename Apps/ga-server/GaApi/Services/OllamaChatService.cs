namespace GaApi.Services;

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Configuration;
using Microsoft.Extensions.Options;

public interface IOllamaChatService
{
    IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<string> ChatAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Ollama-based chat service for conversational AI
///     Uses local Ollama instance with streaming support
/// </summary>
public class OllamaChatService : IOllamaChatService
{
    private readonly ChatbotOptions _chatbotOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaChatService> _logger;
    private readonly string _model;

    public OllamaChatService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptionsMonitor<ChatbotOptions> chatOptions,
        ILogger<OllamaChatService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _chatbotOptions = chatOptions.CurrentValue;
        _model = _chatbotOptions.Model ?? configuration["Ollama:ChatModel"] ?? "llama3.2:3b";
        var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _httpClient.BaseAddress = new Uri(baseUrl);
        var timeoutSeconds = Math.Max(5, _chatbotOptions.StreamTimeoutSeconds);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _logger = logger;
    }

    /// <summary>
    ///     Send a chat message and get streaming response
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var messages = new List<ChatMessage>();

        // Add system prompt if provided
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
        }

        // Add conversation history
        if (conversationHistory != null)
        {
            messages.AddRange(conversationHistory);
        }

        // Add current user message
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        var request = new OllamaChatRequest
        {
            Model = _model,
            Messages = messages,
            Stream = true
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            OllamaChatResponse? chatResponse;
            try
            {
                chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(line);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Ollama response: {Line}", line);
                continue;
            }

            if (chatResponse?.Message?.Content != null)
            {
                yield return chatResponse.Message.Content;
            }

            if (chatResponse?.Done == true)
            {
                break;
            }
        }
    }

    /// <summary>
    ///     Send a chat message and get complete response (non-streaming)
    /// </summary>
    public async Task<string> ChatAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var responseBuilder = new StringBuilder();

        await foreach (var chunk in ChatStreamAsync(userMessage, conversationHistory, systemPrompt, cancellationToken))
        {
            responseBuilder.Append(chunk);
        }

        return responseBuilder.ToString();
    }

    /// <summary>
    ///     Check if Ollama is available and the model is loaded
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(cancellationToken);
            return result?.Models?.Any(m => m.Name.StartsWith(_model.Split(':')[0])) == true;
        }
        catch
        {
            return false;
        }
    }
}

public class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}

public class OllamaChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")] public bool Stream { get; set; } = true;

    [JsonPropertyName("options")] public OllamaOptions? Options { get; set; }
}

public class OllamaOptions
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("top_p")] public double TopP { get; set; } = 0.9;

    [JsonPropertyName("num_predict")] public int? NumPredict { get; set; }
}

public class OllamaChatResponse
{
    [JsonPropertyName("model")] public string? Model { get; set; }

    [JsonPropertyName("message")] public ChatMessage? Message { get; set; }

    [JsonPropertyName("done")] public bool Done { get; set; }

    [JsonPropertyName("total_duration")] public long? TotalDuration { get; set; }

    [JsonPropertyName("load_duration")] public long? LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")] public int? EvalCount { get; set; }
}

public class OllamaModelsResponse
{
    [JsonPropertyName("models")] public List<OllamaModel>? Models { get; set; }
}

public class OllamaModel
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("modified_at")] public DateTime ModifiedAt { get; set; }
}
