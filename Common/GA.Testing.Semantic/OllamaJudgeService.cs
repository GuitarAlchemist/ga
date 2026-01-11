namespace GA.Testing.Semantic;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implementation of IJudgeService using a local Ollama instance.
/// Uses JSON mode for structured evaluation.
/// </summary>
public class OllamaJudgeService(HttpClient client, string modelName = "mistral") : IJudgeService
{
    public async Task<JudgeResult> EvaluateAsync(string text, string prompt, string rubric, CancellationToken cancellationToken = default)
    {
        var systemPrompt = 
            $"{prompt}\n\n" +
            $"You are an AI Judge. Evaluate the following text against the provided rubric.\n" +
            $"TEXT TO EVALUATE:\n\"\"\"\n{text}\n\"\"\"\n\n" +
            $"RUBRIC:\n{rubric}\n\n" +
            $"Response Format: You MUST return a JSON object with exactly three fields:\n" +
            $"1. \"isPassing\": (boolean)\n" +
            $"2. \"rationale\": (string explanation)\n" +
            $"3. \"confidence\": (float between 0 and 1)\n";

        var requestBody = new
        {
            model = modelName,
            prompt = systemPrompt,
            stream = false,
            format = "json"
        };

        var response = await client.PostAsJsonAsync("/api/generate", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
        var content = responseJson?.Response ?? throw new InvalidOperationException("No response from Ollama");

        try 
        {
            var result = JsonSerializer.Deserialize<JudgeResultJson>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new JudgeResult(result?.IsPassing ?? false, result?.Rationale ?? "No rationale provided", result?.Confidence ?? 0);
        }
        catch (JsonException ex)
        {
            return new JudgeResult(false, $"Failed to parse Ollama JSON: {ex.Message}. Raw content: {content}", 0);
        }
    }

    private record OllamaGenerateResponse(string Response);
    private record JudgeResultJson(bool IsPassing, string Rationale, double Confidence);
}
