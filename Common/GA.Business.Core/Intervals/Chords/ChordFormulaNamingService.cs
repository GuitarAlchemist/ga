namespace GA.Business.Core.Intervals.Chords;

using Primitives;
using Tonal.Modes;

/// <summary>
/// Service responsible for generating intelligent names for chord formulas.
/// Separates naming logic from chord formula structure definition.
/// </summary>
public static class ChordFormulaNamingService
{
    /// <summary>
    /// Generates an intelligent name for a modal chord formula based on its context
    /// </summary>
    public static string GenerateModalChordName(ScaleMode parentMode, int scaleDegree, ChordExtension extension, ChordStackingType stackingType = ChordStackingType.Tertian)
    {
        var baseName = GenerateBaseName(parentMode, scaleDegree);
        var chordSymbol = GenerateChordSymbol(parentMode, scaleDegree, extension);
        var stackingSuffix = GetStackingSuffix(stackingType);

        return $"{baseName}{chordSymbol}{stackingSuffix}";
    }

    /// <summary>
    /// Generates an intelligent name for a custom chord formula based on its intervals
    /// </summary>
    public static string GenerateCustomChordName(string baseName, IEnumerable<ChordFormulaInterval> intervals)
    {
        var intervalList = intervals.ToList();

        // Analyze the intervals to determine chord quality and extensions using standard jazz notation
        var chordSymbol = AnalyzeChordSymbol(intervalList);

        return $"{baseName}{chordSymbol}";
    }

    /// <summary>
    /// Updates the name of an existing chord formula with a new base name
    /// </summary>
    public static string UpdateChordName(ChordFormula formula, string newBaseName)
    {
        return formula switch
        {
            Modal modal => GenerateModalChordName(modal.ParentMode, modal.ScaleDegree, modal.Extension, modal.StackingType)
                .Replace($"Degree{modal.ScaleDegree}", newBaseName),
            Custom custom => GenerateCustomChordName(newBaseName, custom.Intervals),
            _ => newBaseName
        };
    }

    private static string GenerateBaseName(ScaleMode parentMode, int scaleDegree)
    {
        // For now, use simple degree-based naming
        // This could be enhanced to use Roman numeral analysis or other naming conventions
        return $"Degree{scaleDegree}";
    }

    private static string GetStackingSuffix(ChordStackingType stackingType) => stackingType switch
    {
        ChordStackingType.Tertian => "", // Default, no suffix needed
        ChordStackingType.Quartal => " (4ths)",
        ChordStackingType.Quintal => " (5ths)",
        _ => ""
    };

    /// <summary>
    /// Generates a proper chord symbol using standard jazz notation conventions
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
    /// Analyzes intervals to create a proper chord symbol using standard jazz notation
    /// </summary>
    private static string AnalyzeChordSymbol(IList<ChordFormulaInterval> intervals)
    {
        var third = intervals.FirstOrDefault(i => i.Size == SimpleIntervalSize.Third);
        var fifth = intervals.FirstOrDefault(i => i.Size == SimpleIntervalSize.Fifth);
        var seventh = intervals.FirstOrDefault(i => i.Size == SimpleIntervalSize.Seventh);

        // Start with triad quality
        var symbol = "";

        if (third?.Quality == IntervalQuality.Minor)
        {
            if (fifth?.Quality == IntervalQuality.Diminished)
                symbol = "dim"; // Diminished triad
            else
                symbol = "m"; // Minor triad
        }
        else if (third?.Quality == IntervalQuality.Major && fifth?.Quality == IntervalQuality.Augmented)
        {
            symbol = "aug"; // Augmented triad
        }
        // Major triad is default (no symbol needed)

        // Add seventh if present
        if (seventh != null)
        {
            if (seventh.Quality == IntervalQuality.Major)
            {
                symbol += "maj7"; // Major seventh
            }
            else if (seventh.Quality == IntervalQuality.Minor)
            {
                if (symbol == "dim")
                    symbol = "ø7"; // Half-diminished seventh
                else if (symbol == "m")
                    symbol += "7"; // Minor seventh
                else
                    symbol += "7"; // Dominant seventh (major triad + minor seventh)
            }
            else if (seventh.Quality == IntervalQuality.Diminished && symbol == "dim")
            {
                symbol = "°7"; // Fully diminished seventh
            }
        }

        return symbol;
    }
}
