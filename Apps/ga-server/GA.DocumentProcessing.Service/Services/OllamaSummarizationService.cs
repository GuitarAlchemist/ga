namespace GA.DocumentProcessing.Service.Services;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Service for generating summaries using Ollama (Stage 1 of NotebookLM pattern)
/// </summary>
public class OllamaSummarizationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaSummarizationService> _logger;
    private readonly IConfiguration _configuration;

    public OllamaSummarizationService(
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaSummarizationService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generate text using Ollama with custom prompt
    /// </summary>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var model = _configuration["Ollama:SummarizationModel"] ?? "llama3.2:latest";
        return await CallOllamaAsync(model, prompt, cancellationToken);
    }

    /// <summary>
    /// Generate a summary of the document using Ollama
    /// </summary>
    public async Task<string> GenerateSummaryAsync(string text, CancellationToken cancellationToken = default)
    {
        var model = _configuration["Ollama:SummarizationModel"] ?? "llama3.2:latest";

        var prompt = $@"You are a music theory expert analyzing educational content.

Please provide a comprehensive summary of the following music theory document. Focus on:
1. Main topics and concepts covered
2. Chord progressions and harmonic concepts
3. Scales, modes, and melodic concepts
4. Guitar techniques and fingering patterns
5. Musical examples and exercises
6. Key insights and practical applications

Document:
{text}

Summary:";

        return await CallOllamaAsync(model, prompt, cancellationToken);
    }

    /// <summary>
    /// Extract structured knowledge from text using Ollama (Stage 2 preparation)
    /// </summary>
    public async Task<string> ExtractConceptsAsync(string text, CancellationToken cancellationToken = default)
    {
        var model = _configuration["Ollama:SummarizationModel"] ?? "llama3.2:latest";

        var prompt = $@"You are a music theory expert extracting structured knowledge.

Analyze the following text and extract:
1. Chord progressions (e.g., ""ii-V-I in C major"")
2. Scales and modes (e.g., ""Dorian mode"", ""Harmonic minor scale"")
3. Guitar techniques (e.g., ""alternate picking"", ""hammer-ons"")
4. Key concepts with definitions
5. Musical styles or artists mentioned

Format your response as JSON with these keys:
- chordProgressions: array of strings
- scales: array of strings
- techniques: array of strings
- concepts: object with concept names as keys and definitions as values
- styles: array of strings

Text:
{text}

JSON:";

        return await CallOllamaAsync(model, prompt, cancellationToken);
    }

    private async Task<string> CallOllamaAsync(string model, string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Ollama");

            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.7,
                    NumPredict = 4096
                }
            };

            var response = await client.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);

            if (result?.Response == null)
            {
                throw new InvalidOperationException("Ollama returned null response");
            }

            _logger.LogInformation("Generated {CharCount} characters using model {Model}",
                result.Response.Length, model);

            return result.Response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Ollama API");
            throw new InvalidOperationException("Failed to generate summary with Ollama", ex);
        }
    }
}

// Ollama API models
internal class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

internal class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; }
}

internal class OllamaGenerateResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

