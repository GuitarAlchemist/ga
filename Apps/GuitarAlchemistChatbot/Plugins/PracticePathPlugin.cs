namespace GuitarAlchemistChatbot.Plugins;

using Services;

/// <summary>
///     Semantic Kernel plugin for generating optimal practice paths
/// </summary>
public class PracticePathPlugin(GaApiClient gaApiClient, ILogger<PracticePathPlugin> logger)
{
    /// <summary>
    ///     Generate an optimal practice path for learning chord voicings
    /// </summary>
    /// <param name="pitchClasses">Comma-separated pitch classes (0-11, e.g., "0,4,7" for C major triad)</param>
    /// <param name="tuning">Guitar tuning (e.g., "E2 A2 D3 G3 B3 E4" for standard tuning)</param>
    /// <param name="pathLength">Number of shapes in the practice path (default: 8)</param>
    /// <param name="strategy">
    ///     Optimization strategy: balanced, minimizevoiceleading, maximizeinformationgain, explorefamilies,
    ///     followattractors (default: balanced)
    /// </param>
    /// <returns>Optimal practice path with quality metrics</returns>
    [Description(
        "Generate an optimal practice path for learning chord voicings using spectral graph analysis and progression optimization")]
    public async Task<string> GeneratePracticePathAsync(
        [Description("Comma-separated pitch classes (0-11, e.g., '0,4,7' for C major triad)")]
        string pitchClasses,
        [Description("Guitar tuning (e.g., 'E2 A2 D3 G3 B3 E4' for standard tuning)")]
        string tuning = "E2 A2 D3 G3 B3 E4",
        [Description("Number of shapes in the practice path (default: 8)")]
        int pathLength = 8,
        [Description(
            "Optimization strategy: balanced, minimizevoiceleading, maximizeinformationgain, explorefamilies, followattractors (default: balanced)")]
        string strategy = "balanced")
    {
        logger.LogInformation(
            "Generating practice path: pitchClasses={PitchClasses}, tuning={Tuning}, length={Length}, strategy={Strategy}",
            pitchClasses,
            tuning,
            pathLength,
            strategy);

        // Parse pitch classes
        var pitchClassArray = pitchClasses
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(pc => int.TryParse(pc.Trim(), out var value) ? value : -1)
            .Where(pc => pc >= 0 && pc <= 11)
            .ToArray();

        if (pitchClassArray.Length == 0)
        {
            return
                "Error: No valid pitch classes provided. Please provide pitch classes as comma-separated numbers (0-11).";
        }

        var result = await gaApiClient.GeneratePracticePathAsync(
            pitchClassArray,
            tuning,
            pathLength,
            strategy.ToLowerInvariant());

        if (result == null)
        {
            return "Error: Failed to generate practice path. The GaApi service may be unavailable.";
        }

        // Format the response
        var response = $@"**Optimal Practice Path Generated**

**Chord**: Pitch classes [{string.Join(", ", pitchClassArray)}]
**Tuning**: {tuning}
**Strategy**: {strategy}
**Path Length**: {result.ShapeIds.Count} shapes

**Quality Metrics**:
- **Quality Score**: {result.Quality:F2} (0.0 = poor, 1.0 = excellent)
- **Entropy**: {result.Entropy:F2} bits
- **Complexity**: {result.Complexity:F2}
- **Predictability**: {result.Predictability:F2}
- **Diversity**: {result.Diversity:F2}

**Practice Path**:
{FormatPracticePath(result)}

**Recommendations**:
{GetRecommendations(result)}";

        return response;
    }

    private static string FormatPracticePath(OptimizedPracticePathResponse result)
    {
        var steps = new List<string>();

        for (var i = 0; i < result.Shapes.Count; i++)
        {
            var shape = result.Shapes[i];
            var positions = string.Join(", ", shape.Positions
                .Where(p => !p.IsMuted)
                .Select(p => $"Str{p.String}:Fret{p.Fret}"));

            steps.Add($"{i + 1}. **{shape.Id}** (span: {shape.Span}, ergonomics: {shape.Ergonomics:F2})");
            steps.Add($"   Positions: {positions}");
        }

        return string.Join("\n", steps);
    }

    private static string GetRecommendations(OptimizedPracticePathResponse result)
    {
        var recommendations = new List<string>();

        if (result.Quality > 0.7)
        {
            recommendations.Add("- **Excellent path**: High quality progression with good balance");
        }
        else if (result.Quality > 0.5)
        {
            recommendations.Add("- **Good path**: Solid progression for practice");
        }
        else
        {
            recommendations.Add("- **Challenging path**: May require more practice time");
        }

        if (result.Diversity > 0.7)
        {
            recommendations.Add("- **High diversity**: Explores many different voicings");
        }
        else if (result.Diversity < 0.3)
        {
            recommendations.Add("- **Low diversity**: Focuses on similar voicings, good for mastery");
        }

        if (result.Complexity > 0.7)
        {
            recommendations.Add("- **Advanced level**: Complex transitions, take your time");
        }
        else if (result.Complexity < 0.3)
        {
            recommendations.Add("- **Beginner friendly**: Simple transitions, easy to learn");
        }

        return string.Join("\n", recommendations);
    }
}
