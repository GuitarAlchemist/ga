namespace GA.Business.Core.Chords;

using Atonal;
using Intervals.Primitives;
using Tonal;

/// <summary>
///     Service for key-aware chord naming that analyzes chords within all major/minor keys
///     and orders results by key probability
/// </summary>
public static class KeyAwareChordNamingService
{
    /// <summary>
    ///     Chord function within a key
    /// </summary>
    public enum ChordFunction
    {
        Tonic, // I, i
        Supertonic, // ii, iiÂ°
        Mediant, // iii, III
        Subdominant, // IV, iv
        Dominant, // V, v
        Submediant, // vi, VI
        LeadingTone, // viiÂ°, VII
        Secondary, // V/V, etc.
        Chromatic, // Non-diatonic
        Unknown
    }

    /// <summary>
    ///     Analyzes a chord within all possible key contexts
    /// </summary>
    public static KeyAwareAnalysis AnalyzeInAllKeys(ChordTemplate template, PitchClass root)
    {
        var keyContexts = new List<KeyContextResult>();

        // Analyze in all major and minor keys
        foreach (var key in Key.Items)
        {
            var context = AnalyzeInKey(template, root, key);
            keyContexts.Add(context);
        }

        // Sort by probability (highest first)
        var sortedContexts = keyContexts.OrderByDescending(c => c.Probability).ToList().AsReadOnly();
        var mostProbable = sortedContexts.First();

        var recommendedName = GenerateRecommendedName(template, root, mostProbable);
        var recommendedSpelling = GetRecommendedEnharmonicSpelling(root, mostProbable.Key);

        return new KeyAwareAnalysis(
            root, template, sortedContexts, mostProbable, recommendedName, recommendedSpelling);
    }

    /// <summary>
    ///     Analyzes a chord within a specific key context
    /// </summary>
    public static KeyContextResult AnalyzeInKey(ChordTemplate template, PitchClass root, Key key)
    {
        var scaleDegree = GetScaleDegree(root, key);
        var function = DetermineChordFunction(scaleDegree, template, key);
        var romanNumeral = GenerateRomanNumeral(scaleDegree, template, key);
        var isNaturallyOccurring = IsNaturallyOccurringInKey(template, root, key);
        var requiresAccidentals = RequiresAccidentals(root, key);
        var probability = CalculateKeyProbability(template, root, key, isNaturallyOccurring, function);
        var chordName = GenerateKeyContextChordName(template, root, key);
        var functionalDescription = GenerateFunctionalDescription(function, scaleDegree, key);

        return new KeyContextResult(
            key, probability, chordName, romanNumeral, function, scaleDegree,
            isNaturallyOccurring, requiresAccidentals, functionalDescription);
    }

    /// <summary>
    ///     Gets the most probable keys for a chord
    /// </summary>
    public static IEnumerable<KeyContextResult> GetMostProbableKeys(ChordTemplate template, PitchClass root,
        int count = 5)
    {
        var analysis = AnalyzeInAllKeys(template, root);
        return analysis.KeyContexts.Take(count);
    }

    /// <summary>
    ///     Gets the scale degree of a pitch class within a key
    /// </summary>
    internal static int GetScaleDegree(PitchClass pitchClass, Key key)
    {
        var keyRoot = key.Root.PitchClass;
        var semitoneDistance = (pitchClass.Value - keyRoot.Value + 12) % 12;

        return semitoneDistance switch
        {
            0 => 1, // Tonic
            2 => 2, // Supertonic
            4 => 3, // Mediant
            5 => 4, // Subdominant
            7 => 5, // Dominant
            9 => 6, // Submediant
            11 => 7, // Leading tone
            _ => GetChromaticScaleDegree(semitoneDistance)
        };
    }

    /// <summary>
    ///     Gets chromatic scale degree for non-diatonic notes
    /// </summary>
    private static int GetChromaticScaleDegree(int semitones)
    {
        return semitones switch
        {
            1 => 1, // b2
            3 => 2, // b3
            6 => 4, // #4/b5
            8 => 5, // b6
            10 => 6, // b7
            _ => 1 // Default to 1
        };
    }

    /// <summary>
    ///     Determines chord function within a key
    /// </summary>
    internal static ChordFunction DetermineChordFunction(int scaleDegree, ChordTemplate template, Key key)
    {
        var quality = template.Quality;
        var isMajorKey = key.KeyMode == KeyMode.Major;

        return (scaleDegree, quality, isMajorKey) switch
        {
            (1, ChordQuality.Major, true) => ChordFunction.Tonic,
            (1, ChordQuality.Minor, false) => ChordFunction.Tonic,
            (2, ChordQuality.Minor, true) => ChordFunction.Supertonic,
            (2, ChordQuality.Diminished, false) => ChordFunction.Supertonic,
            (3, ChordQuality.Minor, true) => ChordFunction.Mediant,
            (3, ChordQuality.Major, false) => ChordFunction.Mediant,
            (4, ChordQuality.Major, true) => ChordFunction.Subdominant,
            (4, ChordQuality.Minor, false) => ChordFunction.Subdominant,
            (5, ChordQuality.Major, _) => ChordFunction.Dominant,
            (6, ChordQuality.Minor, true) => ChordFunction.Submediant,
            (6, ChordQuality.Major, false) => ChordFunction.Submediant,
            (7, ChordQuality.Diminished, true) => ChordFunction.LeadingTone,
            (7, ChordQuality.Major, false) => ChordFunction.LeadingTone,
            _ => ChordFunction.Chromatic
        };
    }

    /// <summary>
    ///     Generates Roman numeral notation
    /// </summary>
    private static string GenerateRomanNumeral(int scaleDegree, ChordTemplate template, Key key)
    {
        var baseRoman = scaleDegree switch
        {
            1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", _ => "?"
        };

        // Adjust case based on chord quality
        if (template.Quality is ChordQuality.Minor or ChordQuality.Diminished)
        {
            baseRoman = baseRoman.ToLower();
        }

        // Add quality symbols
        var qualitySymbol = template.Quality switch
        {
            ChordQuality.Diminished => "Â°",
            ChordQuality.Augmented => "+",
            _ => ""
        };

        // Add extension
        var extensionSymbol = template.Extension switch
        {
            ChordExtension.Seventh => "7",
            ChordExtension.Ninth => "9",
            ChordExtension.Eleventh => "11",
            ChordExtension.Thirteenth => "13",
            _ => ""
        };

        return $"{baseRoman}{qualitySymbol}{extensionSymbol}";
    }

    /// <summary>
    ///     Checks if chord naturally occurs in the key
    /// </summary>
    internal static bool IsNaturallyOccurringInKey(ChordTemplate template, PitchClass root, Key key)
    {
        var keyPitchClasses = key.PitchClassSet;
        var chordPitchClasses = GetChordPitchClasses(template, root);

        // Check if all chord tones are in the key
        return chordPitchClasses.All(pc => keyPitchClasses.Contains(pc));
    }

    /// <summary>
    ///     Gets pitch classes in the chord
    /// </summary>
    internal static IEnumerable<PitchClass> GetChordPitchClasses(ChordTemplate template, PitchClass root)
    {
        yield return root;

        foreach (var interval in template.Intervals)
        {
            var semitones = interval.Interval.Semitones.Value;
            var pitchClass = PitchClass.FromValue((root.Value + semitones) % 12);
            yield return pitchClass;
        }
    }

    /// <summary>
    ///     Checks if chord root requires accidentals in the key
    /// </summary>
    private static bool RequiresAccidentals(PitchClass root, Key key)
    {
        return !key.PitchClassSet.Contains(root);
    }

    /// <summary>
    ///     Calculates probability that chord belongs to the key
    /// </summary>
    private static double CalculateKeyProbability(ChordTemplate template, PitchClass root, Key key,
        bool isNaturallyOccurring, ChordFunction function)
    {
        var probability = 0.0;

        // Base probability for naturally occurring chords
        if (isNaturallyOccurring)
        {
            probability += 0.6;
        }
        else
        {
            probability += 0.1; // Low probability for chromatic chords
        }

        // Function-based probability boost
        probability += function switch
        {
            ChordFunction.Tonic => 0.3,
            ChordFunction.Dominant => 0.25,
            ChordFunction.Subdominant => 0.2,
            ChordFunction.Supertonic => 0.15,
            ChordFunction.Submediant => 0.15,
            ChordFunction.Mediant => 0.1,
            ChordFunction.LeadingTone => 0.1,
            ChordFunction.Secondary => 0.05,
            _ => 0.0
        };

        // Penalty for complex extensions in simple keys
        if (template.Extension >= ChordExtension.Eleventh)
        {
            probability -= 0.1;
        }

        // Boost for common chord progressions
        var scaleDegree = GetScaleDegree(root, key);
        if (IsCommonProgressionChord(scaleDegree, template, key))
        {
            probability += 0.1;
        }

        return Math.Max(0.0, Math.Min(1.0, probability));
    }

    /// <summary>
    ///     Checks if chord is part of common progressions
    /// </summary>
    private static bool IsCommonProgressionChord(int scaleDegree, ChordTemplate template, Key key)
    {
        // Common progressions: I-V-vi-IV, ii-V-I, vi-IV-I-V
        var commonDegrees = new[] { 1, 2, 4, 5, 6 };
        return commonDegrees.Contains(scaleDegree);
    }

    /// <summary>
    ///     Generates chord name within key context
    /// </summary>
    private static string GenerateKeyContextChordName(ChordTemplate template, PitchClass root, Key key)
    {
        var contextualRoot = GetContextualNoteName(root, key);
        var suffix = BasicChordExtensionsService.GetExtensionNotation(template.Extension, template.Quality);
        return $"{contextualRoot}{suffix}";
    }

    /// <summary>
    ///     Gets contextual note name based on key
    /// </summary>
    private static string GetContextualNoteName(PitchClass pitchClass, Key key)
    {
        var context = key.AccidentalKind switch
        {
            AccidentalKind.Sharp => EnharmonicNamingService.MusicalContext.SharpKey,
            AccidentalKind.Flat => EnharmonicNamingService.MusicalContext.FlatKey,
            _ => EnharmonicNamingService.MusicalContext.Natural
        };

        var analysis = EnharmonicNamingService.AnalyzeEnharmonicChoices(pitchClass, context);
        return analysis.PreferredName;
    }

    /// <summary>
    ///     Generates functional description
    /// </summary>
    private static string GenerateFunctionalDescription(ChordFunction function, int scaleDegree, Key key)
    {
        var functionName = function.ToString();
        var keyName = key.ToString();
        return $"{functionName} function (degree {scaleDegree}) in {keyName}";
    }

    /// <summary>
    ///     Generates recommended chord name based on most probable key
    /// </summary>
    private static string GenerateRecommendedName(ChordTemplate template, PitchClass root,
        KeyContextResult mostProbable)
    {
        return mostProbable.ChordName;
    }

    /// <summary>
    ///     Gets recommended enharmonic spelling based on key context
    /// </summary>
    private static string GetRecommendedEnharmonicSpelling(PitchClass root, Key key)
    {
        return GetContextualNoteName(root, key);
    }

    /// <summary>
    ///     Gets chord analysis summary for display
    /// </summary>
    public static string GetAnalysisSummary(KeyAwareAnalysis analysis)
    {
        var topKeys = analysis.KeyContexts.Take(3);
        var keyDescriptions = topKeys.Select(k =>
            $"{k.Key} ({k.Probability:P0}) - {k.RomanNumeral} ({k.Function})");

        return $"Recommended: {analysis.RecommendedName} | " +
               $"Top Keys: {string.Join(", ", keyDescriptions)}";
    }

    /// <summary>
    ///     Key-aware chord analysis result
    /// </summary>
    public record KeyAwareAnalysis(
        PitchClass Root,
        ChordTemplate? Template,
        IReadOnlyList<KeyContextResult> KeyContexts,
        KeyContextResult MostProbableKey,
        string RecommendedName,
        string RecommendedEnharmonicSpelling);

    /// <summary>
    ///     Chord analysis within a specific key context
    /// </summary>
    public record KeyContextResult(
        Key Key,
        double Probability,
        string ChordName,
        string RomanNumeral,
        ChordFunction Function,
        int ScaleDegree,
        bool IsNaturallyOccurring,
        bool RequiresAccidentals,
        string FunctionalDescription);
}
