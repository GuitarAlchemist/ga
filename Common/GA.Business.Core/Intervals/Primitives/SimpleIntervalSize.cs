﻿namespace GA.Business.Core.Intervals.Primitives;

/// <summary>
/// A simple interval size (Between 1 and 8 semitones)
/// </summary>
/// <remarks>
/// https://en.wikipedia.org/wiki/Interval_(Objects)
/// https://hellomusictheory.com/learn/intervals/
///
/// Implements <see cref="IIntervalSize{IntervalSize}" />
/// </remarks>
[PublicAPI]
public readonly record struct SimpleIntervalSize : IParsable<SimpleIntervalSize>, IIntervalSize<SimpleIntervalSize>
{
    #region IParsable Members

    /// <inheritdoc />
    public static SimpleIntervalSize Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out SimpleIntervalSize result)
    {
        if (!int.TryParse(s, out var i)) throw new ArgumentException("Invalid format");
        result = FromValue(i);
        return true;
    }

    #endregion

    #region IIntervalSize<IntervalSize> Members

    public static IReadOnlyCollection<SimpleIntervalSize> Items => ValueObjectCollection<SimpleIntervalSize>.Create();
    public static IReadOnlyList<int> Values => Items.ToValueList();

    #endregion

    #region IValueObject<IntervalSize>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        if (obj is IIntervalSize intervalSize) return _value.CompareTo(intervalSize.Value);
        return 1;
    }

    public static bool operator <(SimpleIntervalSize left, SimpleIntervalSize right) => left.CompareTo(right) < 0;
    public static bool operator >(SimpleIntervalSize left, SimpleIntervalSize right) => left.CompareTo(right) > 0;
    public static bool operator <=(SimpleIntervalSize left, SimpleIntervalSize right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SimpleIntervalSize left, SimpleIntervalSize right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = IntervalSizeValues.UnisonValue;
    private const int _maxValue = IntervalSizeValues.OctaveValue;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SimpleIntervalSize FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static SimpleIntervalSize Min => FromValue(_minValue);
    public static SimpleIntervalSize Max => FromValue(_maxValue);
    public static int CheckRange(int value) => ValueObjectUtils<SimpleIntervalSize>.EnsureValueRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<SimpleIntervalSize>.EnsureValueRange(value, minValue, maxValue);

    public static implicit operator SimpleIntervalSize(int value) => new() { Value = value };
    public static implicit operator int(SimpleIntervalSize intervalSize) => intervalSize._value;
    public static SimpleIntervalSize operator +(SimpleIntervalSize intervalSize, int increment) => new() { Value = intervalSize.Value + increment % 7 };
    public static SimpleIntervalSize operator ++(SimpleIntervalSize intervalSize) => intervalSize + 1;
    public static SimpleIntervalSize operator !(SimpleIntervalSize intervalSize) => intervalSize.ToInverse();

    public static SimpleIntervalSize Unison => FromValue(IntervalSizeValues.UnisonValue);
    public static SimpleIntervalSize Second => FromValue(IntervalSizeValues.SecondValue);
    public static SimpleIntervalSize Third => FromValue(IntervalSizeValues.ThirdValue);
    public static SimpleIntervalSize Fourth => FromValue(IntervalSizeValues.FourthValue);
    public static SimpleIntervalSize Fifth => FromValue(IntervalSizeValues.FifthValue);
    public static SimpleIntervalSize Sixth => FromValue(IntervalSizeValues.SixthValue);
    public static SimpleIntervalSize Seventh => FromValue(IntervalSizeValues.SeventhValue);
    public static SimpleIntervalSize Octave => FromValue(IntervalSizeValues.OctaveValue);

    public static readonly IReadOnlySet<SimpleIntervalSize> Perfect = new HashSet<SimpleIntervalSize>([1, 4, 5, 8]); // 1, 4, 5, 8, 11, 12, 15 => Perfect
    public static readonly IReadOnlySet<SimpleIntervalSize> Imperfect = new HashSet<SimpleIntervalSize>([2, 3, 6, 7]); // 2, 3, 6, 7, 9, 10, 13, 14 => Imperfect

    public static IReadOnlyCollection<SimpleIntervalSize> Range(int start, int count) => ValueObjectUtils<SimpleIntervalSize>.GetItems(start, count);
    public static IReadOnlyCollection<SimpleIntervalSize> Range(int count) => ValueObjectUtils<SimpleIntervalSize>.GetItems(-_minValue, count);

    public CompoundIntervalSize ToCompound() => new() {Value = _value + 7};

    /// <summary>
    /// Formula available for a perfect interval
    /// </summary>
    /// <remarks>
    /// See https://musictheory.pugetsound.edu/mt21c/AugmentedAndDiminishedIntervals.html
    /// </remarks>
    public static readonly IReadOnlySet<IntervalQuality> PerfectQualities = new[] {IntervalQuality.Diminished, IntervalQuality.Perfect, IntervalQuality.Augmented}.ToImmutableHashSet();

    /// <summary>
    /// Formula available for an imperfect interval.
    /// </summary>
    /// <remarks>
    /// See https://musictheory.pugetsound.edu/mt21c/AugmentedAndDiminishedIntervals.html
    /// </remarks>
    public static readonly IReadOnlySet<IntervalQuality> ImperfectQualities = new[] {IntervalQuality.Diminished, IntervalQuality.Minor, IntervalQuality.Major, IntervalQuality.Augmented }.ToImmutableHashSet();

    /// <summary>
    /// Gets available qualities ({d, P, Am} if perfect interval, {d, m, M, Am} if imperfect interval) 
    /// </summary>
    /// <returns>The <see cref="IReadOnlyCollection{Quality}"/>.</returns>
    public IReadOnlySet<IntervalQuality> AvailableQualities => Consonance == IntervalConsonance.Perfect
        ? PerfectQualities
        : ImperfectQualities;

    public IntervalConsonance Consonance => _value switch
    {
        IntervalSizeValues.UnisonValue or IntervalSizeValues.FourthValue or IntervalSizeValues.FifthValue or IntervalSizeValues.OctaveValue => IntervalConsonance.Perfect,
        _ => IntervalConsonance.Imperfect
    };

    /// <summary>
    /// Gets the inverse interval diatonic interval number.
    /// </summary>
    /// <remarks>
    /// Inverse diatonic intervals add up to 9 - see explanation here: https://www.essential-Objects-theory.com/inverted-intervals.html
    /// </remarks>
    /// <returns>The <see cref="SimpleIntervalSize"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleIntervalSize ToInverse() => FromValue(9 - Value);

    /// <summary>
    /// Get the semitones distance for the interval.
    /// </summary>
    /// <returns>The <see cref="Primitives.Semitones"/></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public Semitones Semitones => Value switch
    {
        IntervalSizeValues.UnisonValue => new() {Value = 0},
        IntervalSizeValues.SecondValue => new() {Value = 2}, // Tone (+2)
        IntervalSizeValues.ThirdValue => new() {Value = 4}, // Tone (+2)
        IntervalSizeValues.FourthValue => new() {Value = 5}, // Half-Tone (+1)
        IntervalSizeValues.FifthValue => new() {Value = 7}, // Tone (+2)
        IntervalSizeValues.SixthValue => new() {Value = 9}, // Tone (+2)
        IntervalSizeValues.SeventhValue => new() {Value = 11}, // Tone (+2)
        IntervalSizeValues.OctaveValue => new() {Value = 12}, // Half-Tone (+1)
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public override string ToString() => Value.ToString();

    private static class IntervalSizeValues
    {
        public const int UnisonValue = 1;
        public const int SecondValue = 2;
        public const int ThirdValue = 3;
        public const int FourthValue = 4;
        public const int FifthValue = 5;
        public const int SixthValue = 6;
        public const int SeventhValue = 7;
        public const int OctaveValue = 8;
    }
}

