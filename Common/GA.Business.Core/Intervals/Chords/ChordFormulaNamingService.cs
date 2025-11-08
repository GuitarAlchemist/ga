namespace GA.Business.Core.Intervals.Chords;

using Core.Chords;
using Tonal.Modes;

/// <summary>
///     Service responsible for generating intelligent names for chord formulas.
///     Separates naming logic from chord formula structure definition.
/// </summary>
public static class ChordFormulaNamingService
{
    /// <summary>
    ///     Generates an intelligent name for a modal chord formula based on its context
    /// </summary>
    public static string GenerateModalChordName(ScaleMode parentMode, int scaleDegree, ChordExtension extension,
        ChordStackingType stackingType = ChordStackingType.Tertian)
    {
        var baseName = GenerateBaseName(parentMode, scaleDegree);
        var chordSymbol = GenerateChordSymbol(parentMode, scaleDegree, extension);
        var stackingSuffix = GetStackingSuffix(stackingType);

        return $"{baseName}{chordSymbol}{stackingSuffix}";
    }

    /// <summary>
    ///     Generates an intelligent name for a custom chord formula based on its intervals
    /// </summary>
    public static string GenerateCustomChordName(string baseName, IEnumerable<ChordFormulaInterval> intervals)
    {
        var intervalList = intervals.ToList();

        // Analyze the intervals to determine chord quality and extensions using standard jazz notation
        var chordSymbol = AnalyzeChordSymbol(intervalList);

        return $"{baseName}{chordSymbol}";
    }

    /// <summary>
    ///     Updates the name of an existing chord formula with a new base name
    /// </summary>
    public static string UpdateChordName(ChordFormula formula, string newBaseName)
    {
        // For now, just return the new base name
        // TODO: Implement proper chord name generation based on formula
        return newBaseName;
    }

    private static string GenerateBaseName(ScaleMode parentMode, int scaleDegree)
    {
        // For now, use simple degree-based naming
        // This could be enhanced to use Roman numeral analysis or other naming conventions
        return $"Degree{scaleDegree}";
    }

    private static string GetStackingSuffix(ChordStackingType stackingType)
    {
        return stackingType switch
        {
            ChordStackingType.Tertian => "", // Default, no suffix needed
            ChordStackingType.Quartal => " (4ths)",
            ChordStackingType.Quintal => " (5ths)",
            _ => ""
        };
    }

    /// <summary>
    ///     Generates a proper chord symbol using standard jazz notation conventions
    /// </summary>
    private static string GenerateChordSymbol(ScaleMode parentMode, int scaleDegree, ChordExtension extension)
    {
        // For now, return a simplified symbol based on extension
        // A full implementation would analyze the actual scale intervals
        return extension switch
        {
            ChordExtension.Triad => "", // Major triad is default (no symbol)
            ChordExtension.Seventh => "7", // Dominant 7th is default
            ChordExtension.Ninth => "9",
            ChordExtension.Eleventh => "11",
            ChordExtension.Thirteenth => "13",
            _ => ""
        };
    }

    /// <summary>
    ///     Analyzes intervals to create a proper chord symbol using standard jazz notation
    /// </summary>
    private static string AnalyzeChordSymbol(IList<ChordFormulaInterval> intervals)
    {
        var third = intervals.FirstOrDefault(i => i.Interval.Semitones.Value is 3 or 4);
        var fifth = intervals.FirstOrDefault(i => i.Interval.Semitones.Value is 6 or 7 or 8);
        var seventh = intervals.FirstOrDefault(i => i.Interval.Semitones.Value is 10 or 11);

        // Start with triad quality
        var symbol = "";

        var thirdSemitones = third?.Interval.Semitones.Value;
        var fifthSemitones = fifth?.Interval.Semitones.Value;

        if (thirdSemitones == 3) // Minor third
        {
            if (fifthSemitones == 6) // Diminished fifth
            {
                symbol = "dim"; // Diminished triad
            }
            else
            {
                symbol = "m"; // Minor triad
            }
        }
        else if (thirdSemitones == 4 && fifthSemitones == 8) // Major third + Augmented fifth
        {
            symbol = "aug"; // Augmented triad
        }
        // Major triad is default (no symbol needed)

        // Add seventh if present
        if (seventh != null)
        {
            var seventhSemitones = seventh.Interval.Semitones.Value;
            if (seventhSemitones == 11) // Major seventh
            {
                symbol += "maj7"; // Major seventh
            }
            else if (seventhSemitones == 10) // Minor seventh
            {
                if (symbol == "dim")
                {
                    symbol = "ø7"; // Half-diminished seventh
                }
                else if (symbol == "m")
                {
                    symbol += "7"; // Minor seventh
                }
                else
                {
                    symbol += "7"; // Dominant seventh (major triad + minor seventh)
                }
            }
            else if (seventhSemitones == 9 && symbol == "dim") // Diminished seventh
            {
                symbol = "°7"; // Fully diminished seventh
            }
        }

        return symbol;
    }
}
