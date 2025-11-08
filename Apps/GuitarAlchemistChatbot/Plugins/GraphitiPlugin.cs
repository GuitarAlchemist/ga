namespace GuitarAlchemistChatbot.Plugins;

using Services;

/// <summary>
///     Semantic Kernel plugin for Graphiti temporal knowledge graph
/// </summary>
public class GraphitiPlugin(GraphitiClient graphitiClient, ILogger<GraphitiPlugin> logger)
{
    private const string DefaultUserId = "chatbot-user"; // TODO: Get from authentication

    /// <summary>
    ///     Search the knowledge graph for relevant musical information
    /// </summary>
    /// <param name="query">Search query (e.g., "C major scale", "jazz voicings", "practice history")</param>
    /// <param name="limit">Maximum number of results (default: 5)</param>
    /// <returns>Search results from the knowledge graph</returns>
    [Description(
        "Search the temporal knowledge graph for relevant musical information, learning history, and personalized insights")]
    public async Task<string> SearchKnowledgeGraphAsync(
        [Description("Search query (e.g., 'C major scale', 'jazz voicings', 'practice history')")]
        string query,
        [Description("Maximum number of results (default: 5)")]
        int limit = 5)
    {
        logger.LogInformation("Searching knowledge graph: {Query}", query);

        var result = await graphitiClient.SearchAsync(query, DefaultUserId, limit);

        if (result == null || result.Results.Count == 0)
        {
            return $"No results found for '{query}' in the knowledge graph.";
        }

        var response = $@"**Knowledge Graph Search Results**

**Query**: {query}
**Found**: {result.Count} results

**Results**:
{FormatSearchResults(result.Results)}";

        return response;
    }

    /// <summary>
    ///     Get personalized learning recommendations
    /// </summary>
    /// <param name="recommendationType">
    ///     Type of recommendation: next_chord, practice_path, technique, or concept (default:
    ///     next_chord)
    /// </param>
    /// <returns>Personalized recommendations based on learning history</returns>
    [Description("Get personalized learning recommendations based on the user's learning history and progress")]
    public async Task<string> GetRecommendationsAsync(
        [Description("Type of recommendation: next_chord, practice_path, technique, or concept (default: next_chord)")]
        string recommendationType = "next_chord")
    {
        logger.LogInformation("Getting recommendations: {Type}", recommendationType);

        var result = await graphitiClient.GetRecommendationsAsync(
            DefaultUserId,
            recommendationType);

        if (result == null || result.Recommendations.Count == 0)
        {
            return $"No recommendations available for '{recommendationType}' at this time.";
        }

        var response = $@"**Personalized Recommendations**

**Type**: {recommendationType}
**User**: {result.UserId}

**Recommendations**:
{FormatRecommendations(result.Recommendations)}";

        return response;
    }

    /// <summary>
    ///     Get the user's learning progress and statistics
    /// </summary>
    /// <returns>User's learning progress, skill level, and improvement trends</returns>
    [Description("Get the user's learning progress, skill level, sessions completed, and improvement trends")]
    public async Task<string> GetLearningProgressAsync()
    {
        logger.LogInformation("Getting learning progress for user {UserId}", DefaultUserId);

        var result = await graphitiClient.GetUserProgressAsync(DefaultUserId);

        if (result == null || result.Progress == null)
        {
            return "No learning progress data available yet. Start practicing to build your learning history!";
        }

        var progress = result.Progress;

        var response = $@"**Your Learning Progress**

**Skill Level**: {progress.SkillLevel:F2} / 10.0
**Sessions Completed**: {progress.SessionsCompleted}
**Recent Activity**: {progress.RecentActivity ?? "No recent activity"}
**Improvement Trend**: {progress.ImprovementTrend ?? "Not enough data"}
**Next Milestone**: {progress.NextMilestone ?? "Keep practicing!"}

{GetProgressInterpretation(progress)}";

        return response;
    }

    /// <summary>
    ///     Record a learning episode (practice session, chord learned, etc.)
    /// </summary>
    /// <param name="episodeType">
    ///     Type of episode: practice_session, chord_learned, scale_learned, technique_mastered, or
    ///     concept_understood
    /// </param>
    /// <param name="description">Description of what was learned or practiced</param>
    /// <returns>Confirmation of the recorded episode</returns>
    [Description(
        "Record a learning episode (practice session, chord learned, scale learned, etc.) to build your learning history")]
    public async Task<string> RecordLearningEpisodeAsync(
        [Description(
            "Type of episode: practice_session, chord_learned, scale_learned, technique_mastered, or concept_understood")]
        string episodeType,
        [Description("Description of what was learned or practiced")]
        string description)
    {
        logger.LogInformation("Recording learning episode: {Type} - {Description}", episodeType, description);

        var content = new Dictionary<string, object>
        {
            ["description"] = description,
            ["timestamp"] = DateTime.UtcNow.ToString("O")
        };

        var result = await graphitiClient.AddEpisodeAsync(
            DefaultUserId,
            episodeType,
            content);

        if (result == null || result.Status != "success")
        {
            return $"Failed to record learning episode. {result?.Message ?? "Unknown error"}";
        }

        return $@"**Learning Episode Recorded** ✅

**Type**: {episodeType}
**Description**: {description}
**Status**: Successfully added to your learning history

Your progress has been updated in the knowledge graph!";
    }

    private static string FormatSearchResults(List<GraphitiSearchResult> results)
    {
        if (results.Count == 0)
        {
            return "No results found.";
        }

        var formatted = results.Take(5).Select((r, i) =>
            $"{i + 1}. **{r.Type ?? "Unknown"}** (score: {r.Score:F2})\n   {r.Content}");

        return string.Join("\n\n", formatted);
    }

    private static string FormatRecommendations(List<GraphitiRecommendation> recommendations)
    {
        if (recommendations.Count == 0)
        {
            return "No recommendations available.";
        }

        var formatted = recommendations.Take(5).Select((r, i) =>
        {
            var reasoning = !string.IsNullOrEmpty(r.Reasoning)
                ? $"\n   Reasoning: {r.Reasoning}"
                : "";

            return $"{i + 1}. **{r.Type}**: {r.Content} (confidence: {r.Confidence:F2}){reasoning}";
        });

        return string.Join("\n\n", formatted);
    }

    private static string GetProgressInterpretation(GraphitiUserProgress progress)
    {
        var parts = new List<string>();

        if (progress.SkillLevel < 3.0)
        {
            parts.Add("- **Beginner level**: Keep practicing fundamentals");
        }
        else if (progress.SkillLevel < 6.0)
        {
            parts.Add("- **Intermediate level**: Good progress, explore more advanced concepts");
        }
        else if (progress.SkillLevel < 8.0)
        {
            parts.Add("- **Advanced level**: Excellent skills, focus on mastery");
        }
        else
        {
            parts.Add("- **Expert level**: Outstanding! Consider teaching others");
        }

        if (progress.SessionsCompleted < 10)
        {
            parts.Add("- **Early stages**: Build consistency with regular practice");
        }
        else if (progress.SessionsCompleted < 50)
        {
            parts.Add("- **Building momentum**: Great consistency, keep it up!");
        }
        else
        {
            parts.Add("- **Dedicated learner**: Impressive commitment to practice");
        }

        return string.Join("\n", parts);
    }
}
