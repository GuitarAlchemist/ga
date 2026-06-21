namespace GA.Business.ML.Providers.Mistral;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

/// <summary>
/// Hand-rolled <see cref="IChatClient"/> implementation against Mistral's
/// OpenAI-compatible <c>/v1/chat/completions</c> endpoint. Built for #193
/// (cascade fallback when the primary Ollama-backed client times out)
/// because <c>Microsoft.Extensions.AI.OpenAI</c> is deferred in this csproj
/// per the SDK-version-mismatch note on
/// <see cref="GA.Business.ML"/>'s package list.
/// </summary>
/// <remarks>
/// Streaming uses SSE per Mistral's spec. The implementation deliberately
/// stays minimal: no function calling, no tool use, no JSON mode — just the
/// text round-trip the cascade needs.
/// </remarks>
public sealed class MistralChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _model;
    private readonly Uri _endpoint;
    private readonly ChatClientMetadata _metadata;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public MistralChatClient(string apiKey, string model, Uri baseUrl, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(baseUrl);

        _model    = model;
        _endpoint = ResolveChatCompletionsEndpoint(baseUrl);
        _http     = httpClient ?? new HttpClient();
        _ownsHttp = httpClient is null;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (_http.DefaultRequestHeaders.Accept.Count == 0)
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _metadata = new ChatClientMetadata("mistral", baseUrl, model);
    }

    /// <summary>
    /// Composes the <c>chat/completions</c> endpoint from a configurable base
    /// URL. Operators sometimes set <c>Mistral:BaseUrl</c> to the bare host
    /// (<c>https://api.mistral.ai</c>) and sometimes to the already-versioned
    /// form (<c>https://api.mistral.ai/v1/</c>); naively concatenating
    /// <c>v1/chat/completions</c> against the latter produces
    /// <c>/v1/v1/chat/completions</c>. Codex P2 review on #225 caught this.
    /// </summary>
    private static Uri ResolveChatCompletionsEndpoint(Uri baseUrl)
    {
        var path = baseUrl.AbsolutePath.TrimEnd('/');
        var suffix = path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? "chat/completions"
            : "v1/chat/completions";

        // Append the suffix to the base — preserves scheme/host/port and any
        // existing path (e.g. an OpenAI-compatible proxy sitting on a subpath).
        var normalized = path.Length == 0
            ? new Uri(baseUrl, "/")
            : new UriBuilder(baseUrl) { Path = path + "/" }.Uri;

        return new Uri(normalized, suffix);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var payload = BuildRequest(messages, options, stream: false);
        using var response = await _http.PostAsJsonAsync(_endpoint, payload, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Mistral chat completion failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Truncate(body, 500)}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var completion = await response.Content
            .ReadFromJsonAsync<MistralChatCompletion>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (completion?.Choices is not { Count: > 0 } choices)
        {
            return new ChatResponse([new ChatMessage(ChatRole.Assistant, string.Empty)])
            {
                ModelId = _model,
            };
        }

        var first = choices[0];
        var content = first.Message?.Content ?? string.Empty;
        var role = string.IsNullOrEmpty(first.Message?.Role)
            ? ChatRole.Assistant
            : new ChatRole(first.Message!.Role!);

        var chatResponse = new ChatResponse([new ChatMessage(role, content)])
        {
            ModelId      = completion.Model ?? _model,
            ResponseId   = completion.Id,
            FinishReason = MapFinishReason(first.FinishReason),
        };

        if (completion.Usage is { } usage)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount  = usage.PromptTokens,
                OutputTokenCount = usage.CompletionTokens,
                TotalTokenCount  = usage.TotalTokens,
            };
        }

        return chatResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var payload = BuildRequest(messages, options, stream: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Mistral streaming chat completion failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Truncate(body, 500)}",
                inner: null,
                statusCode: response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payloadJson = line["data:".Length..].Trim();
            if (payloadJson.Length == 0 || payloadJson == "[DONE]") continue;

            MistralStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<MistralStreamChunk>(payloadJson, JsonOptions);
            }
            catch (JsonException)
            {
                // Skip malformed SSE frames rather than abort the stream — the
                // primary client may still be partway through useful content.
                continue;
            }

            if (chunk?.Choices is not { Count: > 0 } choices) continue;

            var delta = choices[0].Delta;
            if (delta is null) continue;

            var text = delta.Content;
            if (string.IsNullOrEmpty(text)) continue;

            yield return new ChatResponseUpdate(ChatRole.Assistant, text)
            {
                ModelId      = chunk.Model ?? _model,
                ResponseId   = chunk.Id,
                FinishReason = MapFinishReason(choices[0].FinishReason),
            };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null) return null;
        if (serviceType == typeof(ChatClientMetadata)) return _metadata;
        if (serviceType == typeof(MistralChatClient))  return this;
        return null;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private MistralChatRequest BuildRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        bool stream)
    {
        var wire = messages.Select(ToWireMessage).ToList();

        return new MistralChatRequest
        {
            Model       = options?.ModelId ?? _model,
            Messages    = wire,
            Stream      = stream,
            Temperature = options?.Temperature,
            TopP        = options?.TopP,
            MaxTokens   = options?.MaxOutputTokens,
        };
    }

    private static MistralChatMessage ToWireMessage(ChatMessage message)
    {
        var role = message.Role.Value switch
        {
            "system"    => "system",
            "user"      => "user",
            "assistant" => "assistant",
            "tool"      => "tool",
            _           => message.Role.Value,
        };

        // ChatMessage exposes .Text as the concatenated text of any TextContent parts.
        return new MistralChatMessage { Role = role, Content = message.Text ?? string.Empty };
    }

    private static ChatFinishReason? MapFinishReason(string? reason) => reason switch
    {
        null or ""        => null,
        "stop"            => ChatFinishReason.Stop,
        "length"          => ChatFinishReason.Length,
        "tool_calls"      => ChatFinishReason.ToolCalls,
        "content_filter"  => ChatFinishReason.ContentFilter,
        _                 => new ChatFinishReason(reason),
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    // ── Wire DTOs ────────────────────────────────────────────────────────────
    private sealed class MistralChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<MistralChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public float? TopP { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
    }

    private sealed class MistralChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class MistralChatCompletion
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<MistralChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public MistralUsage? Usage { get; set; }
    }

    private sealed class MistralChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public MistralChatMessage? Message { get; set; }

        [JsonPropertyName("delta")]
        public MistralChatMessage? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class MistralUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    private sealed class MistralStreamChunk
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<MistralChoice>? Choices { get; set; }
    }
}
