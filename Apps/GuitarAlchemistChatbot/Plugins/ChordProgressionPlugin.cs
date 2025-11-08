namespace GuitarAlchemistChatbot.Plugins;

using Services;

/// <summary>
///     Semantic Kernel plugin for chord progression analysis
/// </summary>
public class ChordProgressionPlugin(GaApiClient gaApiClient, ILogger<ChordProgressionPlugin> logger)
{
    /// <summary>
    ///     Analyze a chord progression using information theory
    /// </summary>
    /// <param name="chordNames">Chord names separated by spaces or commas (e.g., "Cmaj7 Dm7 G7 Cmaj7")</param>
    /// <returns>Analysis results including entropy, complexity, and suggestions</returns>
    [Description(
        "Analyze a chord progression using information theory to measure entropy, complexity, and predictability")]
    public async Task<string> AnalyzeProgressionAsync(
        [Description("Chord names separated by spaces or commas (e.g., 'Cmaj7 Dm7 G7 Cmaj7')")]
        string chordNames)
    {
        logger.LogInformation("Analyzing progression: {ChordNames}", chordNames);

        // Parse chord names
        var chords = chordNames
            .Split(new[] { ' ', ',', '-', '>' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray();

        if (chords.Length == 0)
        {
            return
                "Error: No valid chord names provided. Please provide chord names separated by spaces (e.g., 'Cmaj7 Dm7 G7 Cmaj7').";
        }

        var result = await gaApiClient.AnalyzeProgressionAsync(chords);

        if (result == null)
        {
            return "Error: Failed to analyze progression. The GaApi service may be unavailable.";
        }

        // Format the response
        var response = $@"**Chord Progression Analysis**

**Progression**: {string.Join(" → ", chords)}

**Information Theory Metrics**:
- **Entropy**: {result.Entropy:F2} bits (measure of unpredictability)
- **Complexity**: {result.Complexity:F2} (0.0 = simple, 1.0 = complex)
- **Predictability**: {result.Predictability:F2} (0.0 = unpredictable, 1.0 = predictable)
- **Unique Shapes**: {result.UniqueShapes} out of {result.ShapeCount} total shapes

**Interpretation**:
{GetInterpretation(result)}

**Next Chord Suggestions**:
{FormatSuggestions(result.Suggestions)}";

        return response;
    }

    private static string GetInterpretation(ProgressionAnalysisResponse result)
    {
        var parts = new List<string>();

        // Entropy interpretation
        if (result.Entropy < 1.5)
        {
            parts.Add("- **Low entropy**: Very predictable progression, good for beginners");
        }
        else if (result.Entropy < 2.5)
        {
            parts.Add("- **Moderate entropy**: Balanced between predictability and interest");
        }
        else
        {
            parts.Add("- **High entropy**: Unpredictable and complex, challenging for learners");
        }

        // Complexity interpretation
        if (result.Complexity < 0.3)
        {
            parts.Add("- **Low complexity**: Simple chord changes, easy to learn");
        }
        else if (result.Complexity < 0.7)
        {
            parts.Add("- **Moderate complexity**: Moderate difficulty, good for practice");
        }
        else
        {
            parts.Add("- **High complexity**: Complex transitions, advanced level");
        }

        // Predictability interpretation
        if (result.Predictability > 0.7)
        {
            parts.Add("- **High predictability**: Follows common patterns, familiar sound");
        }
        else if (result.Predictability > 0.4)
        {
            parts.Add("- **Moderate predictability**: Mix of familiar and unexpected changes");
        }
        else
        {
            parts.Add("- **Low predictability**: Unexpected changes, creative progression");
        }

        return string.Join("\n", parts);
    }

    private static string FormatSuggestions(List<NextShapeSuggestion> suggestions)
    {
        if (suggestions == null || suggestions.Count == 0)
        {
            return "No suggestions available.";
        }

        var formatted = suggestions.Take(5).Select((s, i) =>
            $"{i + 1}. **{s.ShapeId}** (info gain: {s.InformationGain:F2}, probability: {s.Probability:F2})");

        return string.Join("\n", formatted);
    }
}
