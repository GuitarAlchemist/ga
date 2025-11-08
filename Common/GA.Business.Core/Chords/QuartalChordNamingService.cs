namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Provides basic detection and naming support for quartal harmony (chords built in fourths).
/// </summary>
public static class QuartalChordNamingService
{
    public enum QuartalChordType
    {
        PureFourths, // All perfect fourths (5 semitones)
        AugmentedFourths, // All augmented fourths/tritones (6 semitones)
        DiminishedFourths, // All diminished fourths (4 semitones)
        Mixed, // Mix of different fourth types
        NotQuartal // Not quartal harmony
    }

    /// <summary>
    ///     Detects quartal harmony by analyzing actual intervals, not just stacking type.
    /// </summary>
    public static bool IsQuartalHarmony(ChordTemplate template)
    {
        // First check the obvious case
        if (template.StackingType == ChordStackingType.Quartal)
        {
            return true;
        }

        // Also check by analyzing intervals for quartal structures
        return AnalyzeIntervalStructure(template).Type != QuartalChordType.NotQuartal;
    }

    /// <summary>
    ///     Analyzes a chord for quartal characteristics and returns detailed analysis.
    /// </summary>
    public static QuartalChordAnalysis AnalyzeQuartalChord(ChordTemplate template, PitchClass root)
    {
        var intervalAnalysis = AnalyzeIntervalStructure(template);
        var rootName = ToNoteName(root);

        var (name, description) = intervalAnalysis.Type switch
        {
            QuartalChordType.PureFourths => ($"{rootName} quartal", "Built from perfect fourths"),
            QuartalChordType.AugmentedFourths => ($"{rootName} aug4", "Built from augmented fourths (tritones)"),
            QuartalChordType.DiminishedFourths => ($"{rootName} dim4", "Built from diminished fourths"),
            QuartalChordType.Mixed => ($"{rootName} mixed-quartal", "Mixed fourth intervals"),
            QuartalChordType.NotQuartal => ($"{rootName}", "Not quartal harmony"),
            _ => ($"{rootName} quartal", "Quartal harmony")
        };

        return new QuartalChordAnalysis(intervalAnalysis.Type, name, intervalAnalysis.IntervalSizes, description);
    }

    /// <summary>
    ///     Analyzes the interval structure to determine quartal characteristics
    /// </summary>
    private static (QuartalChordType Type, IReadOnlyList<int> IntervalSizes) AnalyzeIntervalStructure(
        ChordTemplate template)
    {
        // Get the pitch class set from the template
        var pitchClasses = template.Formula.Intervals
            .Select(interval => interval.Interval.Semitones.Value)
            .Prepend(0) // Add root
            .OrderBy(x => x)
            .ToList();

        if (pitchClasses.Count < 3)
        {
            return (QuartalChordType.NotQuartal, []);
        }

        // Calculate intervals between adjacent notes
        var intervals = new List<int>();
        for (var i = 1; i < pitchClasses.Count; i++)
        {
            var interval = pitchClasses[i] - pitchClasses[i - 1];
            intervals.Add(interval);
        }

        // Analyze the intervals for quartal patterns
        var fourthCount = intervals.Count(i => i == 5); // Perfect fourth
        var tritoneCount = intervals.Count(i => i == 6); // Augmented fourth
        var dimFourthCount = intervals.Count(i => i == 4); // Diminished fourth
        var totalIntervals = intervals.Count;

        // Determine quartal type based on interval analysis
        var type = (fourthCount, tritoneCount, dimFourthCount, totalIntervals) switch
        {
            var (p4, aug4, dim4, total) when p4 == total => QuartalChordType.PureFourths,
            var (p4, aug4, dim4, total) when aug4 == total => QuartalChordType.AugmentedFourths,
            var (p4, aug4, dim4, total) when dim4 == total => QuartalChordType.DiminishedFourths,
            var (p4, aug4, dim4, total) when p4 + aug4 + dim4 >= total * 0.7 => QuartalChordType.Mixed,
            _ => QuartalChordType.NotQuartal
        };

        return (type, intervals.AsReadOnly());
    }

    private static string ToNoteName(PitchClass pitchClass)
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
            _ => "?"
        };
    }

    public record QuartalChordAnalysis(
        QuartalChordType Type,
        string PrimaryName,
        IReadOnlyList<int> IntervalSizes,
        string DetailedDescription);
}
