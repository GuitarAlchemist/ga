namespace GA.Business.Core.Orchestration.Clients;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Shared HTTP client for the Ollama <c>/api/generate</c> endpoint.
/// Consolidates the identical plumbing that was previously duplicated across
/// <see cref="Services.OllamaGroundedNarrator"/> and <see cref="Services.QueryUnderstandingService"/>.
/// </summary>
public sealed class OllamaGenerateClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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
    /// <param name="prompt">Prompt text.</param>
    /// <param name="format">Optional response format (e.g. <c>"json"</c>).</param>
    /// <param name="temperature">Sampling temperature (default 0.7).</param>
    /// <param name="numPredict">Optional max token limit.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string> GenerateAsync(
        string        prompt,
        string?       format      = null,
        float         temperature = 0.7f,
        int?          numPredict  = null,
        CancellationToken ct      = default)
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

    // ── helpers ───────────────────────────────────────────────────────────────

    private object BuildRequestBody(string prompt, string? format, float temperature, int? numPredict)
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
