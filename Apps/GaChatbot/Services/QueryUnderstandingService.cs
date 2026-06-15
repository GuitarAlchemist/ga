namespace GaChatbot.Services;

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class QueryUnderstandingService(
    DomainMetadataPrompter prompter,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private const string DefaultModel = "llama3.2";

    private string OllamaBaseUrl =>
        configuration["Ollama:Endpoint"] ?? "http://localhost:11434";

    public async Task<HybridSearchFilters?> ExtractFiltersAsync(string userQuery)
    {
        try
        {
            var systemPrompt = prompter.BuildSystemPrompt();

            var requestBody = new
            {
                model = configuration["Ollama:Model"] ?? DefaultModel,
                prompt = $"{systemPrompt}\n\nUSER QUERY: {userQuery}",
                stream = false,
                format = "json", // Force JSON mode
                options = new
                {
                    temperature = 0.1 // Low temperature for deterministic extraction
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = httpClientFactory.CreateClient("ollama");
            // When the Ollama endpoint is fronted by Cloudflare Access (remote/CI),
            // attach the service-token headers; no-op for a header-less localhost
            // Ollama. See docs/runbooks/chatbot-qa-ollama-ci-endpoint.md.
            var accessClientId = configuration["Ollama:AccessClientId"];
            var accessClientSecret = configuration["Ollama:AccessClientSecret"];
            if (!string.IsNullOrWhiteSpace(accessClientId) && !string.IsNullOrWhiteSpace(accessClientSecret))
            {
                client.DefaultRequestHeaders.Remove("CF-Access-Client-Id");
                client.DefaultRequestHeaders.Add("CF-Access-Client-Id", accessClientId);
                client.DefaultRequestHeaders.Remove("CF-Access-Client-Secret");
                client.DefaultRequestHeaders.Add("CF-Access-Client-Secret", accessClientSecret);
            }

            var response = await client.PostAsync($"{OllamaBaseUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var llmOutput = doc.RootElement.GetProperty("response").GetString();
            if (string.IsNullOrWhiteSpace(llmOutput)) return null;

            return JsonSerializer.Deserialize<HybridSearchFilters>(llmOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QueryUnderstanding] Failed to extract filters: {ex.Message}");
            return null;
        }
    }
}

public class HybridSearchFilters
{
    public string? Intent { get; set; }
    public string? Quality { get; set; }
    public string? Extension { get; set; }
    public string? StackingType { get; set; }
    public int? NoteCount { get; set; }
}
