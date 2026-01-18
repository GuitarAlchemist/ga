namespace GA.AI.Service.Services;

using System.Net.Http;
using System.Text;
using System.Text.Json;

public class OllamaService(HttpClient httpClient, ILogger<OllamaService> logger, IConfiguration configuration) : IOllamaService
{
    private readonly string _model = configuration["Ollama:Model"] ?? "glm4";
    // Default to localhost for local dev. Docker overrides via env var.
    private readonly string _baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

    public async Task<string> GenerateAsync(string prompt, string? model = null)
    {
        var targetModel = model ?? _model;
        logger.LogInformation("Generating AI response with model {Model}", targetModel);

        var requestBody = new
        {
            model = targetModel,
            prompt = prompt,
            stream = false
        };

        try
        {
            var response = await httpClient.PostAsync($"{_baseUrl}/api/generate", 
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("response").GetString() ?? "No response from AI.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ollama request failed");
            return $"Error: Unable to reach AI service ({ex.Message})";
        }
    }

    public async Task<string> AnalyzeBenchmarkAsync(string benchmarkName, object benchmarkData)
    {
        var prompt = $"""
            You are the 'Guitar Alchemist AI Analyst'. 
            Analyze the following benchmark results for '{benchmarkName}'.
            Explain why certain tests might have failed and suggest harmonic or technical improvements.
            Be concise and musically accurate.
            
            Results:
            {JsonSerializer.Serialize(benchmarkData, new JsonSerializerOptions { WriteIndented = true })}
            """;
        
        return await GenerateAsync(prompt);
    }

    public async Task<string> ExplainVoicingAsync(string voicingName, object voicingData)
    {
        var prompt = $"""
            You are the 'Guitar Alchemist AI Analyst'. 
            Explain the harmonic significance and physical properties of the following voicing: '{voicingName}'.
            Discuss its quality, extension, and its likely position on the Phase Sphere (harmonic pole).
            
            Voicing Data:
            {JsonSerializer.Serialize(voicingData, new JsonSerializerOptions { WriteIndented = true })}
            """;
        
        return await GenerateAsync(prompt);
    }
}
