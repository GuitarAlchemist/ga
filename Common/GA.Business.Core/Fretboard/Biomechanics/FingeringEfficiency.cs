namespace GA.Business.Core.Fretboard.Biomechanics;

using Primitives;

/// <summary>
///     Analysis of fingering efficiency for a chord voicing
/// </summary>
public record FingeringEfficiencyAnalysis(
    IReadOnlyDictionary<FingerType, int> FingerUsageCounts,
    double EfficiencyScore,
    double PinkyUsagePercentage,
    int FingerSpan,
    bool HasBarreChord,
    bool UsesThumb,
    string Reason,
    IReadOnlyList<string> Recommendations)
{
    /// <summary>
    ///     Create analysis indicating no fingering (empty chord)
    /// </summary>
    public static FingeringEfficiencyAnalysis None()
    {
        return new FingeringEfficiencyAnalysis(
            new Dictionary<FingerType, int>(),
            0.0,
            0.0,
            0,
            false,
            false,
            "No finger assignments to analyze",
            []);
    }

    /// <summary>
    ///     Create analysis for efficient fingering
    /// </summary>
    public static FingeringEfficiencyAnalysis Efficient(
        IReadOnlyDictionary<FingerType, int> fingerUsage,
        int fingerSpan,
        bool hasBarreChord,
        bool usesThumb,
        IReadOnlyList<string> recommendations,
        double efficiencyScore)
    {
        return new FingeringEfficiencyAnalysis(
            fingerUsage,
            efficiencyScore,
            CalculatePinkyPercentage(fingerUsage),
            fingerSpan,
            hasBarreChord,
            usesThumb,
            "Efficient finger distribution with minimal stretching",
            recommendations);
    }

    /// <summary>
    ///     Create analysis for moderate fingering efficiency
    /// </summary>
    public static FingeringEfficiencyAnalysis Moderate(
        IReadOnlyDictionary<FingerType, int> fingerUsage,
        int fingerSpan,
        bool hasBarreChord,
        bool usesThumb,
        IReadOnlyList<string> recommendations,
        double efficiencyScore)
    {
        return new FingeringEfficiencyAnalysis(
            fingerUsage,
            efficiencyScore,
            CalculatePinkyPercentage(fingerUsage),
            fingerSpan,
            hasBarreChord,
            usesThumb,
            "Moderate finger efficiency with some challenges",
            recommendations);
    }

    /// <summary>
    ///     Create analysis for inefficient fingering
    /// </summary>
    public static FingeringEfficiencyAnalysis Inefficient(
        IReadOnlyDictionary<FingerType, int> fingerUsage,
        int fingerSpan,
        bool hasBarreChord,
        bool usesThumb,
        IReadOnlyList<string> recommendations,
        double efficiencyScore)
    {
        return new FingeringEfficiencyAnalysis(
            fingerUsage,
            efficiencyScore,
            CalculatePinkyPercentage(fingerUsage),
            fingerSpan,
            hasBarreChord,
            usesThumb,
            "Inefficient fingering with significant challenges",
            recommendations);
    }

    private static double CalculatePinkyPercentage(IReadOnlyDictionary<FingerType, int> fingerUsage)
    {
        var totalNotes = fingerUsage.Values.Sum();
        if (totalNotes == 0)
        {
            return 0.0;
        }

        var pinkyNotes = fingerUsage.GetValueOrDefault(FingerType.Little, 0);
        return (double)pinkyNotes / totalNotes * 100.0;
    }
}

/// <summary>
///     Detects and analyzes fingering efficiency for chord voicings
/// </summary>
public static class FingeringEfficiencyDetector
{
    /// <summary>
    ///     Analyze fingering efficiency based on finger assignments
    /// </summary>
    public static FingeringEfficiencyAnalysis Analyze(
        List<(Position.Played Position, FingerType Finger)> fingerAssignments)
    {
        if (fingerAssignments.Count == 0)
        {
            return FingeringEfficiencyAnalysis.None();
        }

        // Count finger usage
        var fingerUsage = new Dictionary<FingerType, int>();
        foreach (var (_, finger) in fingerAssignments)
        {
            fingerUsage[finger] = fingerUsage.GetValueOrDefault(finger, 0) + 1;
        }

        // Calculate finger span (fret distance)
        var frets = fingerAssignments.Select(a => a.Position.Location.Fret.Value).ToList();
        var fingerSpan = frets.Max() - frets.Min();

        // Detect barre chord (same finger on same fret on multiple strings)
        var hasBarreChord = fingerAssignments
            .GroupBy(a => a.Finger)
            .Any(g => g.GroupBy(a => a.Position.Location.Fret.Value).Any(fretGroup => fretGroup.Count() >= 2));

        // Detect thumb usage
        var usesThumb = fingerUsage.ContainsKey(FingerType.Thumb);

        // Calculate efficiency metrics
        var totalNotes = fingerAssignments.Count;
        var pinkyUsage = fingerUsage.GetValueOrDefault(FingerType.Little, 0);
        var pinkyPercentage = (double)pinkyUsage / totalNotes * 100.0;

        // Generate recommendations
        var recommendations = new List<string>();

        // Calculate finger distribution first (needed for recommendations)
        var fingerDistribution = CalculateFingerDistribution(fingerUsage);

        // Check for pinky overuse
        if (pinkyPercentage > 40.0)
        {
            recommendations.Add("High pinky usage detected - consider alternative fingering");
        }

        // Check for large finger span
        if (fingerSpan > 4)
        {
            recommendations.Add($"Large finger span ({fingerSpan} frets) - may be difficult for smaller hands");
        }

        // Check for uneven finger distribution
        if (fingerDistribution < 0.5)
        {
            recommendations.Add("Uneven finger distribution - some fingers are overused");
        }

        // Check for single-finger overuse (excluding barre chords)
        if (!hasBarreChord)
        {
            var maxUsage = fingerUsage.Values.Max();
            if (maxUsage > 2)
            {
                recommendations.Add("One finger is used for multiple notes - consider spreading across fingers");
            }
        }

        // Determine efficiency score
        var efficiencyScore = CalculateEfficiencyScore(
            fingerSpan, pinkyPercentage, fingerDistribution, hasBarreChord, totalNotes);

        // Classify efficiency (always include recommendations)
        if (efficiencyScore >= 0.8)
        {
            return FingeringEfficiencyAnalysis.Efficient(
                fingerUsage, fingerSpan, hasBarreChord, usesThumb, recommendations, efficiencyScore);
        }

        if (efficiencyScore >= 0.5)
        {
            return FingeringEfficiencyAnalysis.Moderate(
                fingerUsage, fingerSpan, hasBarreChord, usesThumb, recommendations, efficiencyScore);
        }

        return FingeringEfficiencyAnalysis.Inefficient(
            fingerUsage, fingerSpan, hasBarreChord, usesThumb, recommendations, efficiencyScore);
    }

    /// <summary>
    ///     Calculate finger distribution evenness (0-1, higher is more even)
    /// </summary>
    private static double CalculateFingerDistribution(Dictionary<FingerType, int> fingerUsage)
    {
        if (fingerUsage.Count == 0)
        {
            return 0.0;
        }

        var counts = fingerUsage.Values.ToList();
        var average = counts.Average();
        var variance = counts.Select(c => Math.Pow(c - average, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        // Normalize: lower std dev = more even distribution
        // Use coefficient of variation (CV) and invert it
        if (average == 0)
        {
            return 0.0;
        }

        var cv = stdDev / average;

        // Convert to 0-1 scale (lower CV = higher score)
        return Math.Max(0.0, 1.0 - cv);
    }

    /// <summary>
    ///     Calculate overall efficiency score (0-1)
    /// </summary>
    private static double CalculateEfficiencyScore(
        int fingerSpan,
        double pinkyPercentage,
        double fingerDistribution,
        bool hasBarreChord,
        int totalNotes)
    {
        // Start with base score
        var score = 1.0;

        // Penalize large finger spans (more aggressive)
        if (fingerSpan > 4)
        {
            score -= 0.3 * Math.Min(1.0, (fingerSpan - 4) / 4.0); // Up to -0.3 for large spans
        }
        else if (fingerSpan > 3)
        {
            score -= 0.1; // Small penalty for 4-fret span
        }

        // Penalize high pinky usage (more aggressive)
        if (pinkyPercentage > 40.0)
        {
            score -= 0.4 * Math.Min(1.0, (pinkyPercentage - 40.0) / 60.0); // Up to -0.4 for high pinky usage
        }

        // Reward even finger distribution (less impact)
        score += 0.1 * fingerDistribution;

        // Penalty for barre chords (they're harder)
        if (hasBarreChord)
        {
            score -= 0.15;
        }

        // Reward compact voicings (fewer notes = easier)
        if (totalNotes <= 3)
        {
            score += 0.05;
        }

        // Clamp to 0-1 range
        return Math.Max(0.0, Math.Min(1.0, score));
    }
}
