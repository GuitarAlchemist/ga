using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GaChatbot.Abstractions;
using GaChatbot.Models;

namespace GaChatbot.Services;

/// <summary>
/// Narrator that uses a local Ollama LLM for natural language responses.
/// Falls back to simple formatting if Ollama is unavailable.
/// </summary>
public class OllamaGroundedNarrator(
    GroundedPromptBuilder promptBuilder,
    ResponseValidator validator) : IGroundedNarrator
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string OllamaUrl = "http://localhost:11434/api/generate";
    private const string DefaultModel = "llama3.2"; // Use a local model

    public async Task<string> NarrateAsync(string query, List<CandidateVoicing> candidates, bool simulateHallucination = false)
    {
        // 1. Build the grounded prompt
        string prompt = promptBuilder.Build(query, candidates);

        // 2. Try calling Ollama
        try
        {
            var response = await CallOllamaAsync(prompt);
            
            // 3. Validate response (remove any hallucinated content)
            var validation = validator.Validate(response, candidates);
            return validation.CleanedMessage;
        }
        catch (Exception ex)
        {
            // Fallback to simple formatting if Ollama is unavailable
            Console.WriteLine($"[Ollama] Connection failed ({ex.Message}), using fallback formatting.");
            return FormatFallback(query, candidates);
        }
    }

    private async Task<string> CallOllamaAsync(string prompt)
    {
        var requestBody = new
        {
            model = DefaultModel,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                num_predict = 256
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(OllamaUrl, content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        
        return doc.RootElement.GetProperty("response").GetString() ?? "No response generated.";
    }

    private static string FormatFallback(string query, List<CandidateVoicing> candidates)
    {
        if (candidates.Count == 0)
        {
            return "No matching voicings found in the database.";
        }

        var lines = new List<string>
        {
            $"Found {candidates.Count} voicing(s) for '{query}':"
        };

        foreach (var c in candidates.Take(5))
        {
            lines.Add($"  • {c.DisplayName} ({c.Shape}) - Score: {c.Score:F2}");
            if (!string.IsNullOrWhiteSpace(c.ExplanationText))
            {
                lines.Add($"    {c.ExplanationText}");
            }
        }

        return string.Join("\n", lines);
    }
}
