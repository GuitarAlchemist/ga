namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Narrator that uses a local Ollama LLM for natural language responses.
/// Falls back to simple formatting if Ollama is unavailable.
/// </summary>
public class OllamaGroundedNarrator(
    GroundedPromptBuilder promptBuilder,
    ResponseValidator validator,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IGroundedNarrator
{
    private const string DefaultModel = "llama3.2";

    private string OllamaBaseUrl =>
        configuration["Ollama:Endpoint"] ?? "http://localhost:11434";

    public async Task<string> NarrateAsync(string query, IReadOnlyList<CandidateVoicing> candidates)
    {
        string prompt = promptBuilder.Build(query, candidates);

        try
        {
            var response = await CallOllamaAsync(prompt);
            var validation = validator.Validate(response, candidates);
            return validation.CleanedMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ollama] Connection failed ({ex.Message}), using fallback formatting.");
            return FormatFallback(query, candidates);
        }
    }

    private async Task<string> CallOllamaAsync(string prompt)
    {
        var requestBody = new
        {
            model = configuration["Ollama:Model"] ?? DefaultModel,
            prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                num_predict = 512
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = httpClientFactory.CreateClient("ollama");
        var response = await client.PostAsync($"{OllamaBaseUrl}/api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        return doc.RootElement.GetProperty("response").GetString() ?? "No response generated.";
    }

    private static string FormatFallback(string query, IReadOnlyList<CandidateVoicing> candidates)
    {
        if (candidates.Count == 0)
            return "No matching voicings found in the database.";

        var lines = new List<string> { $"Found {candidates.Count} voicing(s) for '{query}':" };

        foreach (var c in candidates.Take(5))
        {
            lines.Add($"  • {c.DisplayName} ({c.Shape}) - Score: {c.Score:F2}");
            if (!string.IsNullOrWhiteSpace(c.ExplanationText))
                lines.Add($"    {c.ExplanationText}");
        }

        return string.Join("\n", lines);
    }
}
