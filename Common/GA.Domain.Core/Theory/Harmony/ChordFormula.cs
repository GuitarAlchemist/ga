namespace GA.Domain.Core.Theory.Harmony;

using System.Diagnostics.CodeAnalysis;
using Primitives.Intervals;
using Interval = Primitives.Intervals.Interval;

/// <summary>
///     Represents a chord formula defining the intervals that make up a chord type
///     (<see href="https://en.wikipedia.org/wiki/Chord_(music)" />)
/// </summary>
/// <remarks>
///     Example: Major triad (1, 3, 5) — root, major third (4 semitones), perfect fifth (7 semitones).
/// </remarks>
[PublicAPI]
public sealed class ChordFormula : IEquatable<ChordFormula>
{
    public static readonly ChordFormula Major =
        FromSemitones("Major", Semitones.MajorThird, Semitones.PerfectFifth);
    public static readonly ChordFormula Minor =
        FromSemitones("Minor", Semitones.MinorThird, Semitones.PerfectFifth);
    public static readonly ChordFormula Diminished =
        FromSemitones("Diminished", Semitones.MinorThird, Semitones.DiminishedFifth);
    public static readonly ChordFormula Augmented =
        FromSemitones("Augmented", Semitones.MajorThird, Semitones.AugmentedFifth);
    public static readonly ChordFormula Dominant7 = FromSemitones("Dominant 7th", Semitones.MajorThird,
        Semitones.PerfectFifth, Semitones.MinorSeventh);
    public static readonly ChordFormula Major7 =
        FromSemitones("Major 7th", Semitones.MajorThird, Semitones.PerfectFifth, Semitones.MajorSeventh);
    public static readonly ChordFormula Minor7 =
        FromSemitones("Minor 7th", Semitones.MinorThird, Semitones.PerfectFifth, Semitones.MinorSeventh);
    public static readonly ChordFormula Suspended2 =
        FromSemitones("Sus2", Semitones.Tone, Semitones.PerfectFifth);
    public static readonly ChordFormula Suspended4 =
        FromSemitones("Sus4", Semitones.PerfectFourth, Semitones.PerfectFifth);

    /// <summary>
    ///     Initializes a new instance of the ChordFormula class
    /// </summary>
    public ChordFormula(
        string name,
        IEnumerable<ChordFormulaInterval> intervals,
        ChordStackingType stackingType = ChordStackingType.Tertian)
    {
        Name = name;
        Intervals = intervals.ToList().AsReadOnly();
        StackingType = stackingType;
        Quality = DetermineQuality();
        Extension = DetermineExtension();
    }

    /// <summary>
    ///     Gets the name of the chord formula
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the collection of intervals that define this chord
    /// </summary>
    public IReadOnlyList<ChordFormulaInterval> Intervals { get; }

    /// <summary>
    ///     Gets the chord extension represented by this formula
    /// </summary>
    public ChordExtension Extension { get; }

    /// <summary>
    ///     Gets the chord quality represented by this formula
    /// </summary>
    public ChordQuality Quality { get; }

    /// <summary>
    ///     Gets the stacking type of this chord formula
    /// </summary>
    public ChordStackingType StackingType { get; }

    /// <summary>
    ///     Gets whether this formula represents a suspended chord
    /// </summary>
    public bool IsSuspended => Intervals.Any(i =>
        i is { Function: ChordFunction.Third, Interval.Semitones.Value: 2 or 5 });

    /// <summary>
    ///     Gets the essential intervals (cannot be omitted)
    /// </summary>
    public IEnumerable<ChordFormulaInterval> EssentialIntervals =>
        Intervals.Where(i => i.IsEssential);

    /// <summary>
    ///     Gets the optional intervals (can be omitted in certain voicings)
    /// </summary>
    public IEnumerable<ChordFormulaInterval> OptionalIntervals =>
        Intervals.Where(i => i.IsOptional);

    public bool Equals([NotNullWhen(true)] ChordFormula? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Intervals.Count == other.Intervals.Count &&
               Intervals.All(i => other.Intervals.Any(oi => oi.Interval.Equals(i.Interval)));
    }

    /// <summary>
    ///     Creates a chord formula from interval semitones
    /// </summary>
    public static ChordFormula FromSemitones(string name, params ReadOnlySpan<int> semitones)
    {
        if (semitones.IsEmpty)
        {
            throw new ArgumentException("At least one interval required", nameof(semitones));
        }

        List<ChordFormulaInterval> intervals = [];
        foreach (var s in semitones)
        {
            var interval = new Interval.Chromatic(Semitones.FromValue(s));
            var function = ChordFunctionExtensions.FromSemitones(s);
            intervals.Add(new(interval, function));
        }

        return new(name, intervals);
    }

    /// <summary>
    ///     Creates a chord formula from interval names (Span overload - preferred)
    /// </summary>
    [OverloadResolutionPriority(1)]
    public static ChordFormula FromIntervalNames(string name, params ReadOnlySpan<string> intervalNames)
    {
        List<ChordFormulaInterval> intervals = [];
        foreach (var intervalName in intervalNames)
        {
            var interval = Interval.Simple.Parse(intervalName, null);
            var function = ChordFunctionExtensions.FromSemitones(interval.Semitones.Value);
            intervals.Add(new(interval, function));
        }

        return new(name, intervals);
    }

    /// <summary>
    ///     Creates a chord formula from interval names (array overload - legacy)
    /// </summary>
    [OverloadResolutionPriority(0)]
    public static ChordFormula FromIntervalNames(string name, params string[] intervalNames)
    {
        var intervals = intervalNames.Select(intervalName =>
        {
            var interval = Interval.Simple.Parse(intervalName, null);
            var function = ChordFunctionExtensions.FromSemitones(interval.Semitones.Value);
            return new ChordFormulaInterval(interval, function);
        });

        return new(name, intervals);
    }

    private ChordQuality DetermineQuality()
    {
        // Grounded in standard chord-quality definitions and dominant seventh conventions.
        // https://en.wikipedia.org/wiki/Chord_(music)#Chord_quality
        var hasMinorThird = Intervals.Any(i => i.Interval.Semitones == Semitones.MinorThird);
        var hasMajorThird = Intervals.Any(i => i.Interval.Semitones == Semitones.MajorThird);
        var hasDiminishedFifth = Intervals.Any(i => i.Interval.Semitones == Semitones.DiminishedFifth);
        var hasAugmentedFifth = Intervals.Any(i => i.Interval.Semitones == Semitones.AugmentedFifth);
        var hasMinorSeventh = Intervals.Any(i => i.Interval.Semitones == Semitones.MinorSeventh);

        if (IsSuspended)
        {
            return ChordQuality.Suspended;
        }

        // Dominant: Major 3rd + Minor 7th
        if (hasMajorThird && hasMinorSeventh)
        {
            return ChordQuality.Dominant;
        }

        if (hasDiminishedFifth && hasMinorThird)
        {
            return ChordQuality.Diminished;
        }

        if (hasAugmentedFifth && hasMajorThird)
        {
            return ChordQuality.Augmented;
        }

        if (hasMinorThird)
        {
            return ChordQuality.Minor;
        }

        if (hasMajorThird)
        {
            return ChordQuality.Major;
        }

        return ChordQuality.Other;
    }

    private ChordExtension DetermineExtension()
    {
        // For non-tertian stackings (quartal, quintal, secundal), treat extension by note count
        // rather than tertian-specific interval presence. This ensures that a 3-note quartal chord
        // is classified as a Triad, and a 4-note quartal chord as a Seventh, etc.
        if (StackingType != ChordStackingType.Tertian)
        {
            return Intervals.Count switch
            {
                <= 2 => ChordExtension.Triad,
                3 => ChordExtension.Seventh,
                4 => ChordExtension.Ninth,
                5 => ChordExtension.Eleventh,
                _ => ChordExtension.Thirteenth
            };
        }

        var hasSeventh = Intervals.Any(i =>
            i.Interval.Semitones == Semitones.MinorSeventh || i.Interval.Semitones == Semitones.MajorSeventh);

        var hasNinth = Intervals.Any(i =>
            i.Interval.Semitones == Semitones.Tone ||
            i.Interval.Semitones == Semitones.MajorNinth ||
            (hasSeventh && (
                i.Interval.Semitones == Semitones.Semitone ||
                i.Interval.Semitones == Semitones.MinorNinth ||
                ((i.Interval.Semitones == Semitones.MinorThird || i.Interval.Semitones == Semitones.FromValue(15)) &&
                 i.Function != ChordFunction.Third)
            )));

        var hasEleventh = Intervals.Any(i =>
            i.Interval.Semitones == Semitones.PerfectFourth || i.Interval.Semitones == Semitones.PerfectEleventh);
        var hasThirteenth = Intervals.Any(i =>
            i.Interval.Semitones == Semitones.MajorSixth || i.Interval.Semitones == Semitones.MajorThirteenth);
        var hasSixth = Intervals.Any(i => i.Interval.Semitones == Semitones.MajorSixth);

        if (IsSuspended)
        {
            var hasSus2 = Intervals.Any(i => i.Interval.Semitones == Semitones.Tone);
            return hasSus2 ? ChordExtension.Sus2 : ChordExtension.Sus4;
        }

        if (hasThirteenth && hasSeventh)
        {
            return ChordExtension.Thirteenth;
        }

        if (hasEleventh && hasSeventh)
        {
            return ChordExtension.Eleventh;
        }

        if (hasNinth && hasSeventh)
        {
            return ChordExtension.Ninth;
        }

        if (hasSeventh)
        {
            return ChordExtension.Seventh;
        }

        if (hasSixth && hasNinth)
        {
            return ChordExtension.SixNine;
        }

        if (hasNinth && !hasSeventh)
        {
            return ChordExtension.Add9;
        }

        if (hasSixth)
        {
            return ChordExtension.Sixth;
        }

        return ChordExtension.Triad;
    }

    /// <summary>
    ///     Gets the chord symbol suffix for this formula
    /// </summary>
    public string GetSymbolSuffix()
    {
        var suffix = Quality switch
        {
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Augmented => "aug",
            _ => ""
        };

        suffix += Extension switch
        {
            ChordExtension.Seventh => "7",
            ChordExtension.Ninth => "9",
            ChordExtension.Eleventh => "11",
            ChordExtension.Thirteenth => "13",
            ChordExtension.Add9 => "add9",
            ChordExtension.Add11 => "add11",
            ChordExtension.Sixth => "6",
            ChordExtension.SixNine => "6/9",
            ChordExtension.Sus2 => "sus2",
            ChordExtension.Sus4 => "sus4",
            _ => ""
        };

        return suffix;
    }

    /// <summary>
    ///     Creates a new chord formula with additional intervals
    /// </summary>
    public ChordFormula WithInterval(ChordFormulaInterval interval)
    {
        var newIntervals = Intervals.Concat([interval]);
        return new($"{Name} + {interval.Function}", newIntervals, StackingType);
    }

    /// <summary>
    ///     Creates a new chord formula without the specified interval
    /// </summary>
    public ChordFormula WithoutInterval(ChordFunction function)
    {
        var newIntervals = Intervals.Where(i => i.Function != function);
        return new($"{Name} - {function}", newIntervals, StackingType);
    }

    public override bool Equals(object? obj) => Equals(obj as ChordFormula);

    public override int GetHashCode() => Intervals.Aggregate(0, (hash, interval) =>
        HashCode.Combine(hash, interval.Interval.GetHashCode()));

    public override string ToString() => $"{Name} ({GetSymbolSuffix()})";
}
