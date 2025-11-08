namespace GA.Business.Core.Fretboard.Biomechanics;

/// <summary>
///     Represents the picking technique used to play a chord or passage
/// </summary>
public enum PickingTechnique
{
    /// <summary>
    ///     Standard picking - using only a pick (plectrum)
    ///     Common in rock, metal, punk
    /// </summary>
    Standard,

    /// <summary>
    ///     Fingerstyle - using only fingers (no pick)
    ///     Common in classical, flamenco, folk
    /// </summary>
    Fingerstyle,

    /// <summary>
    ///     Hybrid picking - combination of pick and fingers
    ///     Pick typically plays bass notes (strings 4-6)
    ///     Fingers play treble notes (strings 1-3)
    ///     Common in country, bluegrass, jazz, modern guitar
    /// </summary>
    Hybrid,

    /// <summary>
    ///     Unknown or not applicable
    /// </summary>
    Unknown
}

/// <summary>
///     Detailed information about picking technique detection
/// </summary>
public record PickingAnalysis(
    PickingTechnique Technique,
    int PickedStringCount,
    int FingeredStringCount,
    double Confidence,
    string Reason)
{
    /// <summary>
    ///     Creates a standard picking analysis (pick only)
    /// </summary>
    public static PickingAnalysis Standard(int stringCount, string reason = "All strings played with pick")
    {
        return new PickingAnalysis(PickingTechnique.Standard, stringCount, 0, 1.0, reason);
    }

    /// <summary>
    ///     Creates a fingerstyle analysis (fingers only)
    /// </summary>
    public static PickingAnalysis Fingerstyle(int stringCount, string reason = "All strings played with fingers")
    {
        return new PickingAnalysis(PickingTechnique.Fingerstyle, 0, stringCount, 1.0, reason);
    }

    /// <summary>
    ///     Creates a hybrid picking analysis (pick + fingers)
    /// </summary>
    public static PickingAnalysis Hybrid(int pickedCount, int fingeredCount, double confidence, string reason)
    {
        return new PickingAnalysis(PickingTechnique.Hybrid, pickedCount, fingeredCount, confidence, reason);
    }

    /// <summary>
    ///     Creates an unknown picking analysis
    /// </summary>
    public static PickingAnalysis Unknown(string reason = "Unable to determine picking technique")
    {
        return new PickingAnalysis(PickingTechnique.Unknown, 0, 0, 0.0, reason);
    }
}

/// <summary>
///     Detects picking technique based on chord voicing patterns
/// </summary>
public static class PickingTechniqueDetector
{
    /// <summary>
    ///     Analyzes a chord voicing to determine the most likely picking technique
    /// </summary>
    /// <param name="playedStrings">List of string numbers that are played (1-6, where 1 is high E)</param>
    /// <returns>Picking analysis with technique, confidence, and reasoning</returns>
    public static PickingAnalysis Analyze(IReadOnlyList<int> playedStrings)
    {
        if (playedStrings.Count == 0)
        {
            return PickingAnalysis.Unknown("No strings played");
        }

        if (playedStrings.Count == 1)
        {
            return PickingAnalysis.Standard(1, "Single note - typically picked");
        }

        // Separate bass (4-6) and treble (1-3) strings
        var bassStrings = playedStrings.Where(s => s >= 4).ToList();
        var trebleStrings = playedStrings.Where(s => s <= 3).ToList();
        var middleStrings = playedStrings.Where(s => s is 3 or 4).ToList();

        // All bass strings - likely standard picking or fingerstyle
        if (trebleStrings.Count == 0)
        {
            return PickingAnalysis.Standard(playedStrings.Count, "Bass strings only - typically picked");
        }

        // All treble strings - could be fingerstyle or pick
        if (bassStrings.Count == 0)
        {
            // 3+ treble strings suggests fingerstyle
            if (trebleStrings.Count >= 3)
            {
                return PickingAnalysis.Fingerstyle(playedStrings.Count,
                    "Multiple treble strings - suggests fingerstyle");
            }

            return PickingAnalysis.Standard(playedStrings.Count,
                "Few treble strings - could be picked");
        }

        // Mix of bass and treble - check for hybrid vs fingerstyle
        // Full 6-string chord with both bass and treble - typically fingerstyle
        if (playedStrings.Count == 6)
        {
            return PickingAnalysis.Fingerstyle(playedStrings.Count,
                "Full 6-string chord - typically fingerstyle");
        }

        // Classic hybrid pattern: bass note(s) with pick, treble notes with fingers
        if (bassStrings.Count >= 1 && trebleStrings.Count >= 2)
        {
            var confidence = CalculateHybridConfidence(bassStrings.Count, trebleStrings.Count);
            var reason =
                $"Bass strings ({bassStrings.Count}) + treble strings ({trebleStrings.Count}) - classic hybrid pattern";
            return PickingAnalysis.Hybrid(bassStrings.Count, trebleStrings.Count, confidence, reason);
        }

        // Ambiguous case - default to standard
        return PickingAnalysis.Standard(playedStrings.Count,
            "Mixed strings but pattern unclear - defaulting to standard picking");
    }

    /// <summary>
    ///     Calculates confidence level for hybrid picking detection
    ///     Higher confidence when there's a clear separation between bass and treble
    /// </summary>
    private static double CalculateHybridConfidence(int bassCount, int trebleCount)
    {
        // Ideal hybrid pattern: 1-2 bass notes, 2-3 treble notes
        var bassScore = bassCount switch
        {
            1 => 1.0, // Single bass note is ideal
            2 => 0.9, // Two bass notes is common
            3 => 0.7, // Three bass notes is less common
            _ => 0.5 // More than 3 is unusual
        };

        var trebleScore = trebleCount switch
        {
            2 => 1.0, // Two treble notes is ideal
            3 => 1.0, // Three treble notes is ideal
            1 => 0.7, // Single treble note is less typical
            _ => 0.5 // More than 3 is unusual
        };

        return (bassScore + trebleScore) / 2.0;
    }

    /// <summary>
    ///     Determines if a chord voicing is well-suited for hybrid picking
    /// </summary>
    public static bool IsHybridPickingFriendly(IReadOnlyList<int> playedStrings)
    {
        var analysis = Analyze(playedStrings);
        return analysis.Technique == PickingTechnique.Hybrid && analysis.Confidence >= 0.7;
    }

    /// <summary>
    ///     Suggests the optimal picking technique for a chord voicing
    /// </summary>
    public static PickingTechnique SuggestTechnique(IReadOnlyList<int> playedStrings)
    {
        var analysis = Analyze(playedStrings);

        // High confidence in detected technique
        if (analysis.Confidence >= 0.8)
        {
            return analysis.Technique;
        }

        // Low confidence - suggest based on string count and pattern
        // Full 6-string chords are typically fingerstyle
        if (playedStrings.Count >= 5)
        {
            return PickingTechnique.Fingerstyle;
        }

        // 4 strings could be either, but favor fingerstyle
        if (playedStrings.Count >= 4)
        {
            return PickingTechnique.Fingerstyle;
        }

        return PickingTechnique.Standard; // Default to standard picking
    }
}
