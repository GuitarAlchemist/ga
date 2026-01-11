namespace GA.Business.Core.Chords;

using System;
using System.Collections.Generic;
using System.Linq;
using Intervals;
using Intervals.Primitives;

/// <summary>
///     Represents a chord formula defining the intervals that make up a chord type
/// </summary>
public class ChordFormula : IEquatable<ChordFormula>
{
    /// <summary>
    ///     Initializes a new instance of the ChordFormula class
    /// </summary>
    public ChordFormula(string name, IEnumerable<ChordFormulaInterval> intervals,
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
    public bool IsSuspended => Intervals.Any(i => i.Function == ChordFunction.Third &&
                                                  (i.Interval.Semitones.Value == 2 || i.Interval.Semitones.Value == 5));

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

    public bool Equals(ChordFormula? other)
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
    public static ChordFormula FromSemitones(string name, params int[] semitones)
    {
        var intervals = semitones.Select(s =>
        {
            var interval = new Interval.Chromatic(Semitones.FromValue(s));
            var function = ChordFunctionExtensions.FromSemitones(s);
            return new ChordFormulaInterval(interval, function);
        });

        return new(name, intervals);
    }

    /// <summary>
    ///     Creates a chord formula from interval names
    /// </summary>
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
        var hasMinorThird = Intervals.Any(i => i.Interval.Semitones.Value == 3);
        var hasMajorThird = Intervals.Any(i => i.Interval.Semitones.Value == 4);
        var hasDiminishedFifth = Intervals.Any(i => i.Interval.Semitones.Value == 6);
        var hasAugmentedFifth = Intervals.Any(i => i.Interval.Semitones.Value == 8);
        var hasMinorSeventh = Intervals.Any(i => i.Interval.Semitones.Value == 10);

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

        var hasSeventh = Intervals.Any(i => i.Interval.Semitones.Value is 10 or 11);
        var hasNinth = Intervals.Any(i => i.Interval.Semitones.Value is 2 or 14 || (hasSeventh && i.Interval.Semitones.Value is 1 or 13 or 3 or 15));
        var hasEleventh = Intervals.Any(i => i.Interval.Semitones.Value is 5 or 17);
        var hasThirteenth = Intervals.Any(i => i.Interval.Semitones.Value is 9 or 21);
        var hasSixth = Intervals.Any(i => i.Interval.Semitones.Value == 9);

        if (IsSuspended)
        {
            var hasSus2 = Intervals.Any(i => i.Interval.Semitones.Value == 2);
            return hasSus2 ? ChordExtension.Sus2 : ChordExtension.Sus4;
        }

        if (hasThirteenth)
        {
            return ChordExtension.Thirteenth;
        }

        if (hasEleventh)
        {
            return ChordExtension.Eleventh;
        }

        if (hasNinth && hasSeventh)
        {
            return ChordExtension.Ninth;
        }

        if (hasNinth && !hasSeventh)
        {
            return ChordExtension.Add9;
        }

        if (hasSeventh)
        {
            return ChordExtension.Seventh;
        }

        if (hasSixth && hasNinth)
        {
            return ChordExtension.SixNine;
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

    public override bool Equals(object? obj)
    {
        return Equals(obj as ChordFormula);
    }

    public override int GetHashCode()
    {
        return Intervals.Aggregate(0, (hash, interval) =>
            HashCode.Combine(hash, interval.Interval.GetHashCode()));
    }

    public override string ToString()
    {
        return $"{Name} ({GetSymbolSuffix()})";
    }
}

/// <summary>
///     Provides common chord formulas
/// </summary>
public static class CommonChordFormulas
{
    /// <summary>
    ///     Major triad (1, 3, 5)
    /// </summary>
    public static ChordFormula Major => FromSemitones("Major", 4, 7);

    /// <summary>
    ///     Minor triad (1, b3, 5)
    /// </summary>
    public static ChordFormula Minor => FromSemitones("Minor", 3, 7);

    /// <summary>
    ///     Diminished triad (1, b3, b5)
    /// </summary>
    public static ChordFormula Diminished => FromSemitones("Diminished", 3, 6);

    /// <summary>
    ///     Augmented triad (1, 3, #5)
    /// </summary>
    public static ChordFormula Augmented => FromSemitones("Augmented", 4, 8);

    /// <summary>
    ///     Major seventh (1, 3, 5, 7)
    /// </summary>
    public static ChordFormula Major7 => FromSemitones("Major 7th", 4, 7, 11);

    /// <summary>
    ///     Minor seventh (1, b3, 5, b7)
    /// </summary>
    public static ChordFormula Minor7 => FromSemitones("Minor 7th", 3, 7, 10);

    /// <summary>
    ///     Dominant seventh (1, 3, 5, b7)
    /// </summary>
    public static ChordFormula Dominant7 => FromSemitones("Dominant 7th", 4, 7, 10);

    private static ChordFormula FromSemitones(string name, params int[] semitones)
    {
        return ChordFormula.FromSemitones(name, semitones);
    }
}
