namespace GA.Business.Core.Orchestration.Clients;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Shared single-prompt generation client for <see cref="Services.OllamaGroundedNarrator"/>,
/// <see cref="Services.QueryUnderstandingService"/> and the performance-intent services.
/// </summary>
/// <remarks>
/// By default it calls Ollama's <c>/api/generate</c>. With <c>AI:Generate:Provider=chat</c> it
/// sends the prompt through the application's <see cref="IChatClient"/> instead, so the chatbot
/// needs no local model server (CI, and production on Anthropic). In that mode no temperature is
/// sent (Haiku 5.5 rejects non-default sampling parameters), a JSON format or schema is written
/// into the prompt, and the JSON object is extracted from the reply.
/// </remarks>
public sealed class OllamaGenerateClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IChatClient? chatClient = null)
{
    private const string DefaultModel = "llama3.2";

    // Static to avoid expensive per-call reflection-cache allocation
    private static readonly JsonSerializerOptions _caseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    private bool UseChat =>
        chatClient is not null
        && string.Equals(configuration["AI:Generate:Provider"], "chat", StringComparison.OrdinalIgnoreCase);

    private string BaseUrl =>
        configuration["Ollama:Endpoint"] ?? "http://localhost:11434";

    private string Model =>
        configuration["Ollama:Model"] ?? DefaultModel;

    /// <summary>
    /// Sends a prompt to Ollama and returns the raw <c>response</c> field from the JSON body.
    /// </summary>
    /// <param name="prompt">Prompt text.</param>
    /// <param name="format">
    /// Optional response format. Either the literal string <c>"json"</c> (Ollama's
    /// free-form JSON mode) or a JSON Schema object (e.g. a <see cref="System.Text.Json.Nodes.JsonNode"/>)
    /// for Ollama's native structured outputs — the schema is passed straight through
    /// in the request's <c>format</c> field.
    /// </param>
    /// <param name="temperature">Sampling temperature (default 0.7).</param>
    /// <param name="numPredict">Optional max token limit.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string> GenerateAsync(
        string        prompt,
        object?       format      = null,
        float         temperature = 0.7f,
        int?          numPredict  = null,
        CancellationToken ct      = default)
    {
        if (UseChat)
        {
            return await GenerateWithChatAsync(prompt, format, numPredict, ct);
        }

        var requestBody = BuildRequestBody(prompt, format, temperature, numPredict);
        var json        = JsonSerializer.Serialize(requestBody);
        var content     = new StringContent(json, Encoding.UTF8, "application/json");

        var client   = httpClientFactory.CreateClient("ollama");
        var response = await client.PostAsync($"{BaseUrl}/api/generate", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }

    /// <summary>
    /// Like <see cref="GenerateAsync"/> but deserialises the LLM's JSON response string
    /// into <typeparamref name="T"/> (for structured-output prompts).
    /// Returns <see langword="null"/> when the LLM returns an empty or invalid response.
    /// </summary>
    public async Task<T?> GenerateStructuredAsync<T>(
        string        prompt,
        float         temperature = 0.1f,
        CancellationToken ct      = default) where T : class
    {
        var raw = await GenerateAsync(prompt, format: "json", temperature: temperature, ct: ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return JsonSerializer.Deserialize<T>(raw, _caseInsensitive);
    }

    /// <summary>
    /// Like <see cref="GenerateStructuredAsync{T}(string,float,CancellationToken)"/> but constrains
    /// generation with a JSON Schema via Ollama's native structured-output <c>format</c> field, so the
    /// model can only emit tokens that fit <paramref name="jsonSchema"/>. Returns <see langword="null"/>
    /// when the response is empty or fails to deserialise into <typeparamref name="T"/>.
    /// </summary>
    public async Task<T?> GenerateStructuredAsync<T>(
        string        prompt,
        object        jsonSchema,
        float         temperature = 0.1f,
        CancellationToken ct      = default) where T : class
    {
        var raw = await GenerateAsync(prompt, format: jsonSchema, temperature: temperature, ct: ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return JsonSerializer.Deserialize<T>(raw, _caseInsensitive);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GenerateWithChatAsync(string prompt, object? format, int? numPredict, CancellationToken ct)
    {
        var text = format switch
        {
            null => prompt,
            string => $"{prompt}\n\nRespond with a single JSON object and nothing else.",
            _ => $"{prompt}\n\nRespond with a single JSON object matching this JSON Schema, and nothing else:\n{JsonSerializer.Serialize(format)}",
        };
        var response = await chatClient!.GetResponseAsync(
            [new ChatMessage(ChatRole.User, text)],
            new ChatOptions { MaxOutputTokens = numPredict },
            ct);
        var reply = response.Text;
        return format is null ? reply : ExtractJsonObject(reply);
    }

    /// <summary>The outermost <c>{…}</c> of a reply (fences and prose around it dropped), or empty.</summary>
    private static string ExtractJsonObject(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        return start >= 0 && end > start ? reply[start..(end + 1)] : string.Empty;
    }

    private object BuildRequestBody(string prompt, object? format, float temperature, int? numPredict)
    {
        // Anonymous type with optional fields — use a dictionary so we can omit nulls cleanly.
        var body = new Dictionary<string, object>
        {
            ["model"]   = Model,
            ["prompt"]  = prompt,
            ["stream"]  = false,
            ["options"] = BuildOptions(temperature, numPredict),
        };

        if (format is not null)
            body["format"] = format;

        return body;
    }

    private static object BuildOptions(float temperature, int? numPredict)
    {
        if (numPredict.HasValue)
            return new { temperature, num_predict = numPredict.Value };

        return new { temperature };
    }
}
