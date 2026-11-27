namespace GA.Business.Core.Orchestration.Clients;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Ollama adapter for <see cref="ITextGenerator"/>: POSTs to <c>/api/generate</c> on the named
/// <c>"ollama"</c> HTTP client.
/// </summary>
/// <remarks>
/// Ollama is being phased out. This adapter is selected only with <c>AI:Generate:Provider=ollama</c>,
/// or when no chat client is registered; <see cref="ChatClientTextGenerator"/> is the default.
/// </remarks>
public sealed class OllamaGenerateClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ITextGenerator
{
    private const string DefaultModel = "llama3.2";

    // Static to avoid expensive per-call reflection-cache allocation
    private static readonly JsonSerializerOptions _caseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    private string BaseUrl =>
        configuration["Ollama:Endpoint"] ?? "http://localhost:11434";

    private string Model =>
        configuration["Ollama:Model"] ?? DefaultModel;

    /// <summary>
    /// Sends a prompt to Ollama and returns the raw <c>response</c> field from the JSON body.
    /// </summary>
    public Task<string> GenerateAsync(
        string        prompt,
        float         temperature = 0.7f,
        int?          numPredict  = null,
        CancellationToken ct      = default) =>
        PostGenerateAsync(prompt, format: null, temperature, numPredict, ct);

    /// <summary>
    /// Constrains generation with a JSON Schema via Ollama's native structured-output <c>format</c>
    /// field, so the model can only emit tokens that fit <paramref name="jsonSchema"/>. Returns
    /// <see langword="null"/> when the response is empty.
    /// </summary>
    public async Task<T?> GenerateStructuredAsync<T>(
        string        prompt,
        object        jsonSchema,
        float         temperature = 0.1f,
        CancellationToken ct      = default) where T : class
    {
        var raw = await PostGenerateAsync(prompt, jsonSchema, temperature, numPredict: null, ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return JsonSerializer.Deserialize<T>(raw, _caseInsensitive);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> PostGenerateAsync(
        string prompt, object? format, float temperature, int? numPredict, CancellationToken ct)
    {
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
