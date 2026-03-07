namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using System.Text.Json;
using GA.Business.Core.Orchestration.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extracts structured search constraints from a natural language user query via LLM.
/// </summary>
public class QueryUnderstandingService(
    DomainMetadataPrompter prompter,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<QueryUnderstandingService> logger)
{
    private const string DefaultModel = "llama3.2";

    private string OllamaBaseUrl =>
        configuration["Ollama:Endpoint"] ?? "http://localhost:11434";

    public async Task<QueryFilters?> ExtractFiltersAsync(string userQuery, CancellationToken ct = default)
    {
        try
        {
            var systemPrompt = prompter.BuildSystemPrompt();

            var requestBody = new
            {
                model = configuration["Ollama:Model"] ?? DefaultModel,
                prompt = $"{systemPrompt}\n\nUSER QUERY: {userQuery}",
                stream = false,
                format = "json",
                options = new
                {
                    temperature = 0.1
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = httpClientFactory.CreateClient("ollama");
            var response = await client.PostAsync($"{OllamaBaseUrl}/api/generate", content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            var llmOutput = doc.RootElement.GetProperty("response").GetString();
            if (string.IsNullOrWhiteSpace(llmOutput)) return null;

            return JsonSerializer.Deserialize<QueryFilters>(llmOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[QueryUnderstanding] Failed to extract filters");
            return null;
        }
    }
}
