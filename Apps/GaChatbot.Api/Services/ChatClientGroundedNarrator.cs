namespace GaChatbot.Api.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Notation;
using Microsoft.Extensions.AI;

public sealed class ChatClientGroundedNarrator(
    IChatClient chatClient,
    GroundedPromptBuilder promptBuilder,
    ResponseValidator validator,
    ILogger<ChatClientGroundedNarrator> logger) : IGroundedNarrator
{
    public async Task<string> NarrateAsync(string query, IReadOnlyList<CandidateVoicing> candidates)
    {
        var systemPrompt = GroundedPromptBuilder.Build(query, candidates);
        List<ChatMessage> messages =
        [
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, query)
        ];

        try
        {
            var response = await chatClient.GetResponseAsync(messages, new ChatOptions
            {
                Temperature = 0.7f,
                MaxOutputTokens = 512
            });

            var content = response.Text ?? "No response generated.";
            var validation = validator.Validate(content, candidates);
            if (!validation.IsValid)
            {
                logger.LogWarning(
                    "Grounded narrator rejected hallucinated chord symbols: {Hallucinated}",
                    string.Join(", ", validation.HallucinatedChords));
                return FormatFallback(query, candidates);
            }

            return validation.CleanedMessage;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Narrator failed, using deterministic fallback");
            return FormatFallback(query, candidates);
        }
    }

    private static string FormatFallback(string query, IReadOnlyList<CandidateVoicing> candidates)
    {
        if (candidates.Count == 0)
        {
            return $"I could not find a grounded voicing match for '{query}'.";
        }

        List<string> lines = [$"Found {candidates.Count} voicing(s) for '{query}':"];
        foreach (var candidate in candidates.Take(5))
        {
            lines.Add($"  • {candidate.DisplayName} ({candidate.Shape}) - Score: {candidate.Score:F2}");
            if (PlayableNotationFormatter.TryFormatChordDiagramAsMarkdownFence(candidate.Shape) is { } notation)
            {
                lines.Add(notation);
            }

            if (!string.IsNullOrWhiteSpace(candidate.ExplanationText))
            {
                lines.Add($"    {candidate.ExplanationText}");
            }
        }

        return string.Join("\n", lines);
    }
}
