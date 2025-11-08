namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Service for detecting and naming chord alterations (b9, #9, b5, #5, #11, b13)
/// </summary>
public static class ChordAlterationService
{
    /// <summary>
    ///     Types of chord alterations
    /// </summary>
    public enum AlterationType
    {
        FlatNinth, // b9
        SharpNinth, // #9
        FlatFifth, // b5
        SharpFifth, // #5
        SharpEleventh, // #11
        FlatThirteenth // b13
    }

    /// <summary>
    ///     Analyzes a chord template for alterations
    /// </summary>
    public static AlterationAnalysis AnalyzeAlterations(ChordTemplate template)
    {
        var alterations = DetectAlterations(template.Intervals);
        var alterationString = GenerateAlterationString(alterations);
        var isAlteredDominant = IsAlteredDominant(template, alterations);
        var suggestedNotation = GenerateSuggestedNotation(template, alterations, isAlteredDominant);

        return new AlterationAnalysis(alterations, alterationString, isAlteredDominant, suggestedNotation);
    }

    /// <summary>
    ///     Generates chord name with alterations
    /// </summary>
    public static string GenerateNameWithAlterations(PitchClass root, ChordTemplate template)
    {
        var analysis = AnalyzeAlterations(template);
        var rootName = GetNoteName(root);

        if (analysis.IsAlteredDominant && template.Extension == ChordExtension.Seventh)
        {
            return $"{rootName}7alt";
        }

        var baseName = BasicChordExtensionsService.GenerateChordName(root, template.Quality, template.Extension);

        if (!string.IsNullOrEmpty(analysis.AlterationString))
        {
            return $"{baseName}({analysis.AlterationString})";
        }

        return baseName;
    }

    /// <summary>
    ///     Detects alterations in chord intervals
    /// </summary>
    private static IReadOnlyList<AlterationType> DetectAlterations(IReadOnlyCollection<ChordFormulaInterval> intervals)
    {
        var alterations = new List<AlterationType>();

        foreach (var interval in intervals)
        {
            var semitones = interval.Interval.Semitones.Value;
            var alteration = DetectAlterationFromSemitones(semitones, interval.Function);

            if (alteration.HasValue && !alterations.Contains(alteration.Value))
            {
                alterations.Add(alteration.Value);
            }
        }

        return alterations.AsReadOnly();
    }

    /// <summary>
    ///     Detects alteration type from semitones and function
    /// </summary>
    private static AlterationType? DetectAlterationFromSemitones(int semitones, ChordFunction function)
    {
        return (semitones, function) switch
        {
            (1, ChordFunction.Ninth) => AlterationType.FlatNinth,
            (3, ChordFunction.Ninth) => AlterationType.SharpNinth,
            (6, ChordFunction.Fifth) => AlterationType.FlatFifth,
            (8, ChordFunction.Fifth) => AlterationType.SharpFifth,
            (6, ChordFunction.Eleventh) => AlterationType.SharpEleventh,
            (8, ChordFunction.Thirteenth) => AlterationType.FlatThirteenth,
            // Also check by semitone value regardless of function
            (1, _) => AlterationType.FlatNinth,
            (3, _) when function != ChordFunction.Third => AlterationType.SharpNinth,
            (6, _) when function != ChordFunction.Eleventh => AlterationType.FlatFifth,
            (8, _) when function != ChordFunction.Fifth => AlterationType.SharpFifth,
            _ => null
        };
    }

    /// <summary>
    ///     Generates alteration string notation
    /// </summary>
    private static string GenerateAlterationString(IReadOnlyList<AlterationType> alterations)
    {
        if (!alterations.Any())
        {
            return string.Empty;
        }

        var alterationStrings = alterations.Select(GetAlterationSymbol).ToList();
        return string.Join(",", alterationStrings);
    }

    /// <summary>
    ///     Gets the symbol for an alteration
    /// </summary>
    private static string GetAlterationSymbol(AlterationType alteration)
    {
        return alteration switch
        {
            AlterationType.FlatNinth => "b9",
            AlterationType.SharpNinth => "#9",
            AlterationType.FlatFifth => "b5",
            AlterationType.SharpFifth => "#5",
            AlterationType.SharpEleventh => "#11",
            AlterationType.FlatThirteenth => "b13",
            _ => ""
        };
    }

    /// <summary>
    ///     Determines if this is an altered dominant chord
    /// </summary>
    private static bool IsAlteredDominant(ChordTemplate template, IReadOnlyList<AlterationType> alterations)
    {
        // Must be a dominant 7th chord
        if (template.Extension != ChordExtension.Seventh || template.Quality == ChordQuality.Major)
        {
            return false;
        }

        // Must have at least 2 alterations, including 9th alterations
        if (alterations.Count < 2)
        {
            return false;
        }

        var hasNinthAlteration = alterations.Any(a =>
            a is AlterationType.FlatNinth or AlterationType.SharpNinth);

        var hasFifthAlteration = alterations.Any(a =>
            a is AlterationType.FlatFifth or AlterationType.SharpFifth);

        return hasNinthAlteration && hasFifthAlteration;
    }

    /// <summary>
    ///     Generates suggested notation based on alterations
    /// </summary>
    private static string GenerateSuggestedNotation(ChordTemplate template, IReadOnlyList<AlterationType> alterations,
        bool isAlteredDominant)
    {
        if (isAlteredDominant)
        {
            return "alt (altered dominant)";
        }

        if (!alterations.Any())
        {
            return "No alterations";
        }

        var descriptions = alterations.Select(GetAlterationDescription).ToList();
        return string.Join(", ", descriptions);
    }

    /// <summary>
    ///     Gets description for an alteration
    /// </summary>
    private static string GetAlterationDescription(AlterationType alteration)
    {
        return alteration switch
        {
            AlterationType.FlatNinth => "Flat ninth",
            AlterationType.SharpNinth => "Sharp ninth",
            AlterationType.FlatFifth => "Flat fifth (tritone substitution)",
            AlterationType.SharpFifth => "Sharp fifth (augmented)",
            AlterationType.SharpEleventh => "Sharp eleventh (Lydian)",
            AlterationType.FlatThirteenth => "Flat thirteenth",
            _ => "Unknown alteration"
        };
    }

    /// <summary>
    ///     Gets common altered chord examples
    /// </summary>
    public static IEnumerable<(string Name, string Description)> GetCommonAlteredChords(PitchClass root)
    {
        var rootName = GetNoteName(root);

        return
        [
            ($"{rootName}7alt", "Altered dominant (b9, #9, #11, b13)"),
            ($"{rootName}7(b9)", "Dominant with flat ninth"),
            ($"{rootName}7(#9)", "Dominant with sharp ninth (Hendrix chord)"),
            ($"{rootName}7(b5)", "Dominant with flat fifth"),
            ($"{rootName}7(#5)", "Dominant with sharp fifth"),
            ($"{rootName}7(#11)", "Dominant with sharp eleventh"),
            ($"{rootName}7(b13)", "Dominant with flat thirteenth"),
            ($"{rootName}7(b9,#11)", "Dominant with flat ninth and sharp eleventh"),
            ($"{rootName}maj7(#11)", "Major seventh with sharp eleventh (Lydian)")
        ];
    }

    /// <summary>
    ///     Validates alteration combinations
    /// </summary>
    public static bool IsValidAlterationCombination(IReadOnlyList<AlterationType> alterations)
    {
        // Can't have both flat and sharp of the same interval
        var hasConflict =
            (alterations.Contains(AlterationType.FlatNinth) && alterations.Contains(AlterationType.SharpNinth)) ||
            (alterations.Contains(AlterationType.FlatFifth) && alterations.Contains(AlterationType.SharpFifth));

        return !hasConflict;
    }

    /// <summary>
    ///     Gets theoretical analysis of alterations
    /// </summary>
    public static string GetTheoreticalAnalysis(IReadOnlyList<AlterationType> alterations)
    {
        if (!alterations.Any())
        {
            return "Diatonic chord (no alterations)";
        }

        var analysis = new List<string>();

        if (alterations.Contains(AlterationType.FlatNinth))
        {
            analysis.Add("b9 creates harmonic tension");
        }

        if (alterations.Contains(AlterationType.SharpNinth))
        {
            analysis.Add("#9 adds blues/rock color");
        }

        if (alterations.Contains(AlterationType.FlatFifth))
        {
            analysis.Add("b5 suggests tritone substitution");
        }

        if (alterations.Contains(AlterationType.SharpFifth))
        {
            analysis.Add("#5 creates augmented quality");
        }

        if (alterations.Contains(AlterationType.SharpEleventh))
        {
            analysis.Add("#11 indicates Lydian mode");
        }

        if (alterations.Contains(AlterationType.FlatThirteenth))
        {
            analysis.Add("b13 adds harmonic complexity");
        }

        return string.Join("; ", analysis);
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
    ///     Alteration analysis result
    /// </summary>
    public record AlterationAnalysis(
        IReadOnlyList<AlterationType> Alterations,
        string AlterationString,
        bool IsAlteredDominant,
        string SuggestedNotation);
}
