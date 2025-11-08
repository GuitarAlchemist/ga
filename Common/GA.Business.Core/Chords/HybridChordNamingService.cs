namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Hybrid service that tries tonal analysis first, then falls back to atonal methods
/// </summary>
public static class HybridChordNamingService
{
    /// <summary>
    ///     Analysis strategy used for naming
    /// </summary>
    public enum AnalysisStrategy
    {
        /// <summary>Traditional tonal analysis</summary>
        Tonal,

        /// <summary>Atonal/set theory analysis</summary>
        Atonal,

        /// <summary>Hybrid approach (tonal first, atonal fallback)</summary>
        Hybrid
    }

    /// <summary>
    ///     Analyzes a chord using hybrid tonal-atonal approach
    /// </summary>
    public static HybridAnalysis AnalyzeChord(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        var requiresAtonal = AtonalChordAnalysisService.RequiresAtonalAnalysis(template);

        // Try tonal analysis first
        var tonalName = TryTonalAnalysis(template, root, bassNote);
        var tonalDescription = GetTonalDescription(template, root, bassNote);

        // Try atonal analysis
        string? atonalName = null;
        string? atonalDescription = null;

        if (requiresAtonal || tonalName == null)
        {
            atonalName = AtonalChordAnalysisService.GenerateAtonalChordName(template, root);
            atonalDescription = AtonalChordAnalysisService.GetAtonalDescription(template, root);
        }

        // Determine best strategy and name
        var (recommendedName, strategyUsed, reasoning) = DetermineRecommendedApproach(
            tonalName, atonalName, requiresAtonal, template);

        return new HybridAnalysis(
            recommendedName, strategyUsed, tonalName, atonalName,
            tonalDescription, atonalDescription, requiresAtonal, reasoning);
    }

    /// <summary>
    ///     Gets the best chord name using hybrid analysis
    /// </summary>
    public static string GetBestChordName(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        var analysis = AnalyzeChord(template, root, bassNote);

        // Check for iconic chord matches first - they often provide more meaningful names
        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(template, root, bassNote);
        if (!string.IsNullOrEmpty(comprehensive.IconicName))
        {
            return comprehensive.IconicName;
        }

        return analysis.RecommendedName;
    }

    /// <summary>
    ///     Gets detailed analysis explanation
    /// </summary>
    public static string GetAnalysisExplanation(ChordTemplate template, PitchClass root, PitchClass? bassNote = null)
    {
        var analysis = AnalyzeChord(template, root, bassNote);

        var explanation = $"Strategy: {analysis.StrategyUsed}\n";
        explanation += $"Recommended: {analysis.RecommendedName}\n";
        explanation += $"Reasoning: {analysis.Reasoning}\n";

        if (analysis.TonalName != null)
        {
            explanation += $"Tonal: {analysis.TonalName} - {analysis.TonalDescription}\n";
        }

        if (analysis.AtonalName != null)
        {
            explanation += $"Atonal: {analysis.AtonalName} - {analysis.AtonalDescription}\n";
        }

        return explanation;
    }

    /// <summary>
    ///     Determines if a chord is better analyzed tonally or atonally
    /// </summary>
    public static AnalysisStrategy RecommendStrategy(ChordTemplate template)
    {
        // Check for clear atonal indicators
        if (AtonalChordAnalysisService.RequiresAtonalAnalysis(template))
        {
            return AnalysisStrategy.Atonal;
        }

        // Check for clear tonal indicators
        if (IsClearlyTonal(template))
        {
            return AnalysisStrategy.Tonal;
        }

        // Default to hybrid approach
        return AnalysisStrategy.Hybrid;
    }

    /// <summary>
    ///     Tries tonal analysis and returns result
    /// </summary>
    private static string? TryTonalAnalysis(ChordTemplate template, PitchClass root, PitchClass? bassNote)
    {
        try
        {
            // Use existing tonal naming services
            var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(template, root, bassNote);

            // Check if we got a meaningful tonal name
            if (IsMeaningfulTonalName(comprehensive.Primary, template))
            {
                return comprehensive.Primary;
            }

            // Try with alterations
            if (!string.IsNullOrEmpty(comprehensive.WithAlterations))
            {
                return comprehensive.WithAlterations;
            }

            return null;
        }
        catch
        {
            // Tonal analysis failed
            return null;
        }
    }

    /// <summary>
    ///     Gets tonal description
    /// </summary>
    private static string? GetTonalDescription(ChordTemplate template, PitchClass root, PitchClass? bassNote)
    {
        try
        {
            var quality = template.Quality.ToString();
            var extension = template.Extension.ToString();
            var stacking = template.StackingType.ToString();

            var description = $"{quality} {extension} chord";
            if (template.StackingType != ChordStackingType.Tertian)
            {
                description += $" ({stacking} stacking)";
            }

            if (bassNote.HasValue && !bassNote.Value.Equals(root))
            {
                description += $" with {GetNoteName(bassNote.Value)} bass";
            }

            return description;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Determines the recommended approach
    /// </summary>
    private static (string name, AnalysisStrategy strategy, string reasoning) DetermineRecommendedApproach(
        string? tonalName, string? atonalName, bool requiresAtonal, ChordTemplate template)
    {
        // If atonal analysis is required and we have an atonal name
        if (requiresAtonal && atonalName != null)
        {
            return (atonalName, AnalysisStrategy.Atonal,
                "Chord structure requires atonal analysis (complex intervals, clusters, or symmetrical patterns)");
        }

        // If we have a good tonal name and it's not overly complex
        if (tonalName != null && IsMeaningfulTonalName(tonalName, template))
        {
            return (tonalName, AnalysisStrategy.Tonal,
                "Chord fits traditional tonal harmony patterns");
        }

        // If tonal analysis failed but atonal succeeded
        if (tonalName == null && atonalName != null)
        {
            return (atonalName, AnalysisStrategy.Atonal,
                "Traditional tonal analysis insufficient, using set theory approach");
        }

        // If both failed, prefer atonal as fallback
        if (atonalName != null)
        {
            return (atonalName, AnalysisStrategy.Hybrid,
                "Hybrid analysis: atonal approach provides better description");
        }

        // Last resort - use whatever tonal name we have
        return (tonalName ?? "Unknown chord", AnalysisStrategy.Tonal,
            "Fallback to basic tonal naming");
    }

    /// <summary>
    ///     Checks if a chord is clearly tonal
    /// </summary>
    private static bool IsClearlyTonal(ChordTemplate template)
    {
        // Simple triads and 7ths are clearly tonal
        if (template.Extension <= ChordExtension.Seventh && template.StackingType == ChordStackingType.Tertian)
        {
            return true;
        }

        // Suspended chords are tonal
        if (template.Quality == ChordQuality.Suspended)
        {
            return true;
        }

        // Basic extensions are tonal
        if (template.Extension is ChordExtension.Add9 or ChordExtension.Sixth or ChordExtension.SixNine)
        {
            return true;
        }

        // Check interval count - simple chords are likely tonal
        if (template.Intervals.Count() <= 4)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if a tonal name is meaningful
    /// </summary>
    private static bool IsMeaningfulTonalName(string tonalName, ChordTemplate template)
    {
        // Avoid overly complex tonal names
        if (tonalName.Length > 20)
        {
            return false;
        }

        // Avoid names with too many alterations
        var alterationCount = tonalName.Count(c => c is 'b' or '#');
        if (alterationCount > 3)
        {
            return false;
        }

        // Avoid generic fallback names
        if (tonalName.Contains("Unknown") || tonalName.Contains("?"))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Gets examples of chords that benefit from each approach
    /// </summary>
    public static IEnumerable<(string ChordName, AnalysisStrategy Strategy, string Reason)> GetAnalysisExamples()
    {
        return
        [
            ("Cmaj7", AnalysisStrategy.Tonal, "Clear tertian harmony"),
            ("C7alt", AnalysisStrategy.Tonal, "Jazz harmony with alterations"),
            ("C cluster(5)", AnalysisStrategy.Atonal, "Cluster chord requires set theory"),
            ("C [5-35]", AnalysisStrategy.Atonal, "Complex pitch class set"),
            ("C whole tone", AnalysisStrategy.Atonal, "Symmetrical structure"),
            ("C quartal", AnalysisStrategy.Hybrid, "Can be analyzed both ways"),
            ("C(b9,#11,b13)", AnalysisStrategy.Hybrid, "Complex alterations may need atonal backup"),
            ("C diminished 7th", AnalysisStrategy.Atonal, "Symmetrical diminished structure")
        ];
    }

    /// <summary>
    ///     Gets the note name for a pitch class
    /// </summary>
    private static string GetNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C", 1 => "C#", 2 => "D", 3 => "D#", 4 => "E", 5 => "F",
            6 => "F#", 7 => "G", 8 => "G#", 9 => "A", 10 => "A#", 11 => "B",
            _ => "?"
        };
    }

    /// <summary>
    ///     Comprehensive analysis result combining tonal and atonal approaches
    /// </summary>
    public record HybridAnalysis(
        string RecommendedName,
        AnalysisStrategy StrategyUsed,
        string? TonalName,
        string? AtonalName,
        string? TonalDescription,
        string? AtonalDescription,
        bool RequiresAtonalAnalysis,
        string Reasoning);
}
