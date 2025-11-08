namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Service for handling basic chord extensions (7th, 9th, 11th, 13th) with standard naming
/// </summary>
public static class BasicChordExtensionsService
{
    /// <summary>
    ///     Gets standard notation for a chord extension
    /// </summary>
    public static string GetExtensionNotation(ChordExtension extension, ChordQuality quality)
    {
        return extension switch
        {
            ChordExtension.Triad => "",
            ChordExtension.Sixth => "6",
            ChordExtension.Seventh => GetSeventhNotation(quality),
            ChordExtension.Ninth => GetNinthNotation(quality),
            ChordExtension.Eleventh => GetEleventhNotation(quality),
            ChordExtension.Thirteenth => GetThirteenthNotation(quality),
            ChordExtension.Add9 => "add9",
            ChordExtension.Add11 => "add11",
            ChordExtension.SixNine => "6/9",
            ChordExtension.Sus2 => "sus2",
            ChordExtension.Sus4 => "sus4",
            _ => ""
        };
    }

    /// <summary>
    ///     Gets detailed information about a chord extension
    /// </summary>
    public static ChordExtensionInfo GetExtensionInfo(ChordExtension extension)
    {
        return extension switch
        {
            ChordExtension.Triad => new ChordExtensionInfo(
                extension, "", "", "Basic triad (1-3-5)", new[] { 3, 7 }.ToList().AsReadOnly()),

            ChordExtension.Sixth => new ChordExtensionInfo(
                extension, "6", "6", "Sixth chord (1-3-5-6)", new[] { 3, 7, 9 }.ToList().AsReadOnly()),

            ChordExtension.Seventh => new ChordExtensionInfo(
                extension, "7", "7", "Seventh chord (1-3-5-7)", new[] { 3, 7, 10 }.ToList().AsReadOnly()),

            ChordExtension.Ninth => new ChordExtensionInfo(
                extension, "9", "9", "Ninth chord (1-3-5-7-9)", new[] { 3, 7, 10, 2 }.ToList().AsReadOnly()),

            ChordExtension.Eleventh => new ChordExtensionInfo(
                extension, "11", "11", "Eleventh chord (1-3-5-7-9-11)", new[] { 3, 7, 10, 2, 5 }.ToList().AsReadOnly()),

            ChordExtension.Thirteenth => new ChordExtensionInfo(
                extension, "13", "13", "Thirteenth chord (1-3-5-7-9-11-13)",
                new[] { 3, 7, 10, 2, 5, 9 }.ToList().AsReadOnly()),

            ChordExtension.Add9 => new ChordExtensionInfo(
                extension, "add9", "add9", "Added ninth (1-3-5-9, no 7th)", new[] { 3, 7, 2 }.ToList().AsReadOnly()),

            ChordExtension.Add11 => new ChordExtensionInfo(
                extension, "add11", "add11", "Added eleventh (1-3-5-11, no 7th)",
                new[] { 3, 7, 5 }.ToList().AsReadOnly()),

            ChordExtension.SixNine => new ChordExtensionInfo(
                extension, "6/9", "6/9", "Sixth/ninth chord (1-3-5-6-9)", new[] { 3, 7, 9, 2 }.ToList().AsReadOnly()),

            ChordExtension.Sus2 => new ChordExtensionInfo(
                extension, "sus2", "sus2", "Suspended second (1-2-5)", new[] { 2, 7 }.ToList().AsReadOnly()),

            ChordExtension.Sus4 => new ChordExtensionInfo(
                extension, "sus4", "sus4", "Suspended fourth (1-4-5)", new[] { 5, 7 }.ToList().AsReadOnly()),

            _ => new ChordExtensionInfo(
                extension, "", "", "Unknown extension", new List<int>().AsReadOnly())
        };
    }

    /// <summary>
    ///     Determines if a chord template matches a specific extension
    /// </summary>
    public static bool MatchesExtension(ChordTemplate template, ChordExtension targetExtension)
    {
        var expectedInfo = GetExtensionInfo(targetExtension);
        var actualIntervals = template.Intervals.Select(i => i.Interval.Semitones.Value).ToHashSet();
        var expectedIntervals = expectedInfo.RequiredIntervals.ToHashSet();

        // Check if all expected intervals are present
        return expectedIntervals.IsSubsetOf(actualIntervals);
    }

    /// <summary>
    ///     Gets all possible extensions for a given set of intervals
    /// </summary>
    public static IEnumerable<ChordExtension> GetPossibleExtensions(IEnumerable<int> intervals)
    {
        var intervalSet = intervals.ToHashSet();
        var allExtensions = Enum.GetValues<ChordExtension>();

        foreach (var extension in allExtensions)
        {
            var info = GetExtensionInfo(extension);
            if (info.RequiredIntervals.ToHashSet().IsSubsetOf(intervalSet))
            {
                yield return extension;
            }
        }
    }

    /// <summary>
    ///     Gets the highest extension present in a chord
    /// </summary>
    public static ChordExtension GetHighestExtension(IEnumerable<int> intervals)
    {
        var intervalSet = intervals.ToHashSet();

        // Check from highest to lowest extension
        if (intervalSet.Contains(9))
        {
            return ChordExtension.Thirteenth;
        }

        if (intervalSet.Contains(5) && intervalSet.Contains(10))
        {
            return ChordExtension.Eleventh;
        }

        if (intervalSet.Contains(2) && intervalSet.Contains(10))
        {
            return ChordExtension.Ninth;
        }

        if (intervalSet.Contains(10) || intervalSet.Contains(11))
        {
            return ChordExtension.Seventh;
        }

        if (intervalSet.Contains(9) && !intervalSet.Contains(10))
        {
            return ChordExtension.Sixth;
        }

        if (intervalSet.Contains(2) && !intervalSet.Contains(10))
        {
            return ChordExtension.Add9;
        }

        if (intervalSet.Contains(5) && !intervalSet.Contains(10) && !intervalSet.Contains(3) &&
            !intervalSet.Contains(4))
        {
            return ChordExtension.Sus4;
        }

        if (intervalSet.Contains(2) && !intervalSet.Contains(3) && !intervalSet.Contains(4))
        {
            return ChordExtension.Sus2;
        }

        return ChordExtension.Triad;
    }

    /// <summary>
    ///     Generates a complete chord name with root and extension
    /// </summary>
    public static string GenerateChordName(PitchClass root, ChordQuality quality, ChordExtension extension)
    {
        var rootName = GetNoteName(root);
        var extensionNotation = GetExtensionNotation(extension, quality);

        // For triads, we need to add quality notation separately
        if (extension == ChordExtension.Triad)
        {
            var qualityNotation = GetQualityNotation(quality);
            return $"{rootName}{qualityNotation}";
        }

        // For extensions, the quality is already included in the extension notation
        return $"{rootName}{extensionNotation}";
    }

    /// <summary>
    ///     Gets the seventh chord notation based on quality
    /// </summary>
    private static string GetSeventhNotation(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => "maj7",
            ChordQuality.Minor => "m7",
            ChordQuality.Diminished => "dim7",
            ChordQuality.Augmented => "aug7",
            _ => "7" // Dominant 7th
        };
    }

    /// <summary>
    ///     Gets the ninth chord notation based on quality
    /// </summary>
    private static string GetNinthNotation(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => "maj9",
            ChordQuality.Minor => "m9",
            ChordQuality.Diminished => "dim9",
            ChordQuality.Augmented => "aug9",
            _ => "9" // Dominant 9th
        };
    }

    /// <summary>
    ///     Gets the eleventh chord notation based on quality
    /// </summary>
    private static string GetEleventhNotation(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => "maj11",
            ChordQuality.Minor => "m11",
            _ => "11" // Dominant 11th
        };
    }

    /// <summary>
    ///     Gets the thirteenth chord notation based on quality
    /// </summary>
    private static string GetThirteenthNotation(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => "maj13",
            ChordQuality.Minor => "m13",
            _ => "13" // Dominant 13th
        };
    }

    /// <summary>
    ///     Gets the quality notation
    /// </summary>
    private static string GetQualityNotation(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Augmented => "aug",
            ChordQuality.Suspended => "",
            ChordQuality.Major => "",
            _ => ""
        };
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
    ///     Validates that a chord extension is properly formed
    /// </summary>
    public static bool IsValidExtension(ChordExtension extension, IEnumerable<int> intervals)
    {
        var intervalSet = intervals.ToHashSet();

        return extension switch
        {
            ChordExtension.Ninth => intervalSet.Contains(10), // 9th requires 7th
            ChordExtension.Eleventh => intervalSet.Contains(10) && intervalSet.Contains(2), // 11th requires 7th and 9th
            ChordExtension.Thirteenth => intervalSet.Contains(10) && intervalSet.Contains(2) &&
                                         intervalSet.Contains(5), // 13th requires 7th, 9th, and 11th
            _ => true // Other extensions are always valid if intervals match
        };
    }

    /// <summary>
    ///     Gets common chord progressions using extensions
    /// </summary>
    public static IEnumerable<(string Name, string Description)> GetCommonExtendedProgressions()
    {
        return
        [
            ("ii7-V7-Imaj7", "Minor 7th - Dominant 7th - Major 7th"),
            ("vi7-ii7-V7-Imaj7", "Extended ii-V-I with vi7"),
            ("Imaj7-vi7-ii7-V7", "Extended I-vi-ii-V"),
            ("ii9-V13-Imaj9", "Extended ii-V-I with 9th and 13th"),
            ("iiÃ¸7-V7alt-im7", "Half-diminished - Altered dominant - Minor 7th"),
            ("Imaj7-IV7-viim7b5-iii7", "Extended I-IV-vii-iii"),
            ("vi7-V7/ii-ii7-V7", "Secondary dominant progression"),
            ("Imaj9-bVIImaj7-VImaj7-V7sus4", "Modern jazz progression")
        ];
    }

    /// <summary>
    ///     Basic chord extension information
    /// </summary>
    public record ChordExtensionInfo(
        ChordExtension Extension,
        string StandardNotation,
        string JazzNotation,
        string Description,
        IReadOnlyList<int> RequiredIntervals);
}
