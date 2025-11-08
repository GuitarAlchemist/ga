namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Service for handling enharmonic equivalents and context-aware accidental choices
/// </summary>
public static class EnharmonicNamingService
{
    /// <summary>
    ///     Musical context for enharmonic choices
    /// </summary>
    public enum MusicalContext
    {
        /// <summary>Sharp keys (G, D, A, E, B, F#, C#)</summary>
        SharpKey,

        /// <summary>Flat keys (F, Bb, Eb, Ab, Db, Gb, Cb)</summary>
        FlatKey,

        /// <summary>C major or A minor (no accidentals)</summary>
        Natural,

        /// <summary>Jazz context (prefers certain enharmonics)</summary>
        Jazz,

        /// <summary>Classical context (follows traditional rules)</summary>
        Classical,

        /// <summary>Popular music context (simpler notation)</summary>
        Popular
    }

    /// <summary>
    ///     Gets the best enharmonic spelling for a chord in a given context
    /// </summary>
    public static string GetContextualChordName(PitchClass root, ChordTemplate template, MusicalContext context)
    {
        var contextualRoot = GetContextualNoteName(root, context);
        var suffix = BasicChordExtensionsService.GetExtensionNotation(template.Extension, template.Quality);
        return $"{contextualRoot}{suffix}";
    }

    /// <summary>
    ///     Gets all enharmonic equivalents for a chord
    /// </summary>
    public static IEnumerable<string> GetEnharmonicEquivalents(PitchClass root, ChordTemplate template)
    {
        var names = new List<string>();
        var suffix = BasicChordExtensionsService.GetExtensionNotation(template.Extension, template.Quality);

        // Add sharp version
        var sharpName = GetSharpNoteName(root);
        if (!string.IsNullOrEmpty(sharpName))
        {
            names.Add($"{sharpName}{suffix}");
        }

        // Add flat version
        var flatName = GetFlatNoteName(root);
        if (!string.IsNullOrEmpty(flatName))
        {
            names.Add($"{flatName}{suffix}");
        }

        // Add natural version if applicable
        var naturalName = GetNaturalNoteName(root);
        if (!string.IsNullOrEmpty(naturalName))
        {
            names.Add($"{naturalName}{suffix}");
        }

        return names.Distinct();
    }

    /// <summary>
    ///     Analyzes enharmonic choices for a chord
    /// </summary>
    public static EnharmonicResult AnalyzeEnharmonicChoices(PitchClass root, MusicalContext context)
    {
        var sharpName = GetSharpNoteName(root);
        var flatName = GetFlatNoteName(root);
        var naturalName = GetNaturalNoteName(root);

        // Determine preferred name based on context
        var (preferred, alternate, reasoning) = context switch
        {
            MusicalContext.SharpKey => (sharpName ?? naturalName, flatName, "Sharp keys prefer sharp accidentals"),
            MusicalContext.FlatKey => (flatName ?? naturalName, sharpName, "Flat keys prefer flat accidentals"),
            MusicalContext.Natural => (naturalName ?? sharpName, flatName,
                "Natural keys avoid accidentals when possible"),
            MusicalContext.Jazz => GetJazzPreference(root),
            MusicalContext.Classical => GetClassicalPreference(root),
            MusicalContext.Popular => GetPopularPreference(root),
            _ => (sharpName ?? flatName ?? naturalName, "", "Default preference")
        };

        return new EnharmonicResult(
            preferred ?? GetDefaultNoteName(root),
            alternate ?? "",
            context,
            reasoning);
    }

    /// <summary>
    ///     Gets contextual note name based on musical context
    /// </summary>
    private static string GetContextualNoteName(PitchClass pitchClass, MusicalContext context)
    {
        var analysis = AnalyzeEnharmonicChoices(pitchClass, context);
        return analysis.PreferredName;
    }

    /// <summary>
    ///     Gets sharp version of note name
    /// </summary>
    private static string? GetSharpNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C",
            1 => "C#",
            2 => "D",
            3 => "D#",
            4 => "E",
            5 => "F",
            6 => "F#",
            7 => "G",
            8 => "G#",
            9 => "A",
            10 => "A#",
            11 => "B",
            _ => null
        };
    }

    /// <summary>
    ///     Gets flat version of note name
    /// </summary>
    private static string? GetFlatNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C",
            1 => "Db",
            2 => "D",
            3 => "Eb",
            4 => "E",
            5 => "F",
            6 => "Gb",
            7 => "G",
            8 => "Ab",
            9 => "A",
            10 => "Bb",
            11 => "B",
            _ => null
        };
    }

    /// <summary>
    ///     Gets natural note name (if it exists)
    /// </summary>
    private static string? GetNaturalNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C",
            2 => "D",
            4 => "E",
            5 => "F",
            7 => "G",
            9 => "A",
            11 => "B",
            _ => null // Black keys don't have natural names
        };
    }

    /// <summary>
    ///     Gets default note name (sharp preference)
    /// </summary>
    private static string GetDefaultNoteName(PitchClass pitchClass)
    {
        return GetSharpNoteName(pitchClass) ?? GetFlatNoteName(pitchClass) ?? "?";
    }

    /// <summary>
    ///     Gets jazz context preference
    /// </summary>
    private static (string preferred, string alternate, string reasoning) GetJazzPreference(PitchClass root)
    {
        return root.Value switch
        {
            1 => ("Db", "C#", "Jazz prefers Db for flat keys and blues"),
            3 => ("Eb", "D#", "Jazz prefers Eb for flat keys"),
            6 => ("Gb", "F#", "Jazz prefers Gb for flat keys"),
            8 => ("Ab", "G#", "Jazz prefers Ab for flat keys"),
            10 => ("Bb", "A#", "Jazz prefers Bb for flat keys"),
            _ => (GetDefaultNoteName(root), "", "No jazz preference")
        };
    }

    /// <summary>
    ///     Gets classical context preference
    /// </summary>
    private static (string preferred, string alternate, string reasoning) GetClassicalPreference(PitchClass root)
    {
        return root.Value switch
        {
            1 => ("C#", "Db", "Classical prefers C# in sharp keys"),
            3 => ("D#", "Eb", "Classical prefers D# in sharp keys"),
            6 => ("F#", "Gb", "Classical prefers F# in sharp keys"),
            8 => ("G#", "Ab", "Classical prefers G# in sharp keys"),
            10 => ("A#", "Bb", "Classical prefers A# in sharp keys"),
            _ => (GetDefaultNoteName(root), "", "No classical preference")
        };
    }

    /// <summary>
    ///     Gets popular music context preference
    /// </summary>
    private static (string preferred, string alternate, string reasoning) GetPopularPreference(PitchClass root)
    {
        return root.Value switch
        {
            1 => ("C#", "Db", "Popular music prefers simpler sharp notation"),
            3 => ("Eb", "D#", "Popular music prefers Eb"),
            6 => ("F#", "Gb", "Popular music prefers F#"),
            8 => ("Ab", "G#", "Popular music prefers Ab"),
            10 => ("Bb", "A#", "Popular music prefers Bb"),
            _ => (GetDefaultNoteName(root), "", "No popular preference")
        };
    }

    /// <summary>
    ///     Determines musical context from key signature
    /// </summary>
    public static MusicalContext DetermineContextFromKey(PitchClass keyCenter, bool isMajor = true)
    {
        // Sharp keys: G, D, A, E, B, F#, C#
        var sharpKeys = new[] { 7, 2, 9, 4, 11, 6, 1 };

        // Flat keys: F, Bb, Eb, Ab, Db, Gb, Cb
        var flatKeys = new[] { 5, 10, 3, 8, 1, 6, 11 };

        if (keyCenter.Value is 0 or 9) // C major or A minor
        {
            return MusicalContext.Natural;
        }

        if (sharpKeys.Contains(keyCenter.Value))
        {
            return MusicalContext.SharpKey;
        }

        if (flatKeys.Contains(keyCenter.Value))
        {
            return MusicalContext.FlatKey;
        }

        return MusicalContext.Natural;
    }

    /// <summary>
    ///     Gets recommended enharmonic spelling for chord progressions
    /// </summary>
    public static string GetProgressionSpelling(PitchClass root, PitchClass previousRoot, PitchClass nextRoot)
    {
        // Analyze the melodic motion to determine best spelling
        var prevInterval = (root.Value - previousRoot.Value + 12) % 12;
        var nextInterval = (nextRoot.Value - root.Value + 12) % 12;

        // Prefer spellings that create smooth voice leading
        if (prevInterval is <= 2 or >= 10) // Small intervals
        {
            // Prefer spelling that maintains direction
            return GetSharpNoteName(root) ?? GetFlatNoteName(root) ?? GetDefaultNoteName(root);
        }

        return GetDefaultNoteName(root);
    }

    /// <summary>
    ///     Gets common enharmonic chord pairs
    /// </summary>
    public static IEnumerable<(string Chord1, string Chord2, string Context)> GetCommonEnharmonicPairs()
    {
        return
        [
            ("C#maj7", "Dbmaj7", "C# in sharp keys, Db in flat keys"),
            ("D#m7", "Ebm7", "D# in sharp keys, Eb in flat keys"),
            ("F#7", "Gb7", "F# in sharp keys, Gb in flat keys"),
            ("G#dim", "Abdim", "G# in sharp keys, Ab in flat keys"),
            ("A#sus4", "Bbsus4", "A# in sharp keys, Bb in flat keys"),
            ("C#/F", "Db/F", "Slash chord enharmonics"),
            ("F#m/A", "Gbm/A", "Minor chord enharmonics"),
            ("G#aug", "Abaug", "Augmented chord enharmonics")
        ];
    }

    /// <summary>
    ///     Enharmonic naming result
    /// </summary>
    public record EnharmonicResult(
        string PreferredName,
        string AlternateName,
        MusicalContext RecommendedContext,
        string Reasoning);
}
