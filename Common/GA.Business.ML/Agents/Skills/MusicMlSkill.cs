namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;
using Plugins;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

/// <summary>
/// Orchestrator skill that routes ML analysis queries (chord clustering,
/// scale recommendation, harmonic patterns) to the ix ML pipeline via MCP federation.
/// </summary>
public sealed partial class MusicMlSkill(
    FederationClient federation,
    ILogger<MusicMlSkill> logger) : IOrchestratorSkill
{
    public string Name        => "Music ML";
    public string Description => "ML analysis: chord clustering, scale recommendation, harmonic pattern analysis";

    public bool CanHandle(string message)
        => MlPattern().IsMatch(message);

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var tools = await federation.DiscoverToolsAsync("ix", cancellationToken);
            var mlTool = tools.FirstOrDefault(t => t.Name == "ix_ml_pipeline");

            if (mlTool is null)
            {
                logger.LogDebug("MusicMlSkill: ix_ml_pipeline tool not available");
                return AgentResponse.CannotHelp("music-ml", "ix ML server unavailable — cannot perform ML analysis right now.");
            }

            var taskType = InferTaskType(message);
            var args = new Dictionary<string, object?>
            {
                ["task_type"] = taskType,
                ["query"] = message,
            };

            var result = await federation.CallToolAsync("ix_ml_pipeline", args, cancellationToken);
            if (result is null)
                return AgentResponse.CannotHelp("music-ml", "ML pipeline call failed.");

            var output = string.Join("\n", result.Content
                .OfType<TextContentBlock>()
                .Select(c => c.Text));

            if (string.IsNullOrWhiteSpace(output))
                return AgentResponse.CannotHelp("music-ml", "ML pipeline returned empty result.");

            return new AgentResponse
            {
                AgentId = "music-ml",
                Result = output,
                Confidence = 0.85f,
                Evidence = [$"Task type: {taskType}", "Source: ix ML pipeline"],
                Assumptions = ["Data interpreted as chord/scale/harmonic content"]
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MusicMlSkill: execution failed");
            return AgentResponse.CannotHelp("music-ml", $"ML analysis error: {ex.Message}");
        }
    }

    private static string InferTaskType(string message)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("cluster")) return "clustering";
        if (lower.Contains("classify")) return "classification";
        if (lower.Contains("recommend")) return "recommendation";
        if (lower.Contains("pattern")) return "pattern_analysis";
        return "analysis";
    }

    [GeneratedRegex(@"\b(cluster|classify\s+chords?|recommend\s+scales?|harmonic\s+pattern|machine\s+learning|\bML\b)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MlPattern();
}
