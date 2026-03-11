namespace GA.Business.Core.Orchestration.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Clients;
using GA.Business.Core.Orchestration.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Narrator that uses a local Ollama LLM for natural language responses.
/// Falls back to simple formatting if Ollama is unavailable.
/// </summary>
public class OllamaGroundedNarrator(
    GroundedPromptBuilder promptBuilder,
    ResponseValidator validator,
    OllamaGenerateClient ollamaClient,
    ILogger<OllamaGroundedNarrator> logger) : IGroundedNarrator
{
    public async Task<string> NarrateAsync(string query, IReadOnlyList<CandidateVoicing> candidates)
    {
        string prompt = promptBuilder.Build(query, candidates);

        try
        {
            var response   = await ollamaClient.GenerateAsync(prompt, temperature: 0.7f, numPredict: 512);
            var validation = validator.Validate(response, candidates);
            return validation.CleanedMessage;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Ollama] Connection failed, using fallback formatting");
            return FormatFallback(query, candidates);
        }
    }

    private static string FormatFallback(string query, IReadOnlyList<CandidateVoicing> candidates)
    {
        if (candidates.Count == 0)
            return "No matching voicings found in the database.";

        List<string> lines = [$"Found {candidates.Count} voicing(s) for '{query}':"];

        foreach (var c in candidates.Take(5))
        {
            lines.Add($"  • {c.DisplayName} ({c.Shape}) - Score: {c.Score:F2}");
            if (!string.IsNullOrWhiteSpace(c.ExplanationText))
                lines.Add($"    {c.ExplanationText}");
        }

        return string.Join("\n", lines);
    }
}
