namespace GA.Business.Core.Intervals.Primitives;

using Extensions;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// The size of a diatonic number
/// </summary>
/// <remarks>
/// https://en.wikipedia.org/wiki/Interval_(music)
/// https://hellomusictheory.com/learn/intervals/
/// </remarks>
[PublicAPI]
public readonly record struct IntervalSize : IIntervalSize<IntervalSize>
{
    #region Relational members

    public int CompareTo(IntervalSize other) => _value.CompareTo(other._value);
    public static bool operator <(IntervalSize left, IntervalSize right) => left.CompareTo(right) < 0;
    public static bool operator >(IntervalSize left, IntervalSize right) => left.CompareTo(right) > 0;
    public static bool operator <=(IntervalSize left, IntervalSize right) => left.CompareTo(right) <= 0;
    public static bool operator >=(IntervalSize left, IntervalSize right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = IntervalSizeValues.UnisonValue;
    private const int _maxValue = IntervalSizeValues.OctaveValue;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntervalSize FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static IntervalSize Min => FromValue(_minValue);
    public static IntervalSize Max => FromValue(_maxValue);
    public static int CheckRange(int value) => ValueObjectUtils<IntervalSize>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<IntervalSize>.CheckRange(value, minValue, maxValue);

    public static implicit operator IntervalSize(int value) => new() { Value = value };
    public static implicit operator int(IntervalSize intervalSize) => intervalSize._value;
    public static IntervalSize operator +(IntervalSize intervalSize, int increment) => new() { Value = intervalSize.Value + increment % 7 };
    public static IntervalSize operator ++(IntervalSize intervalSize) => intervalSize + 1;
    public static IntervalSize operator !(IntervalSize intervalSize) => intervalSize.ToInverse();

    public static IntervalSize Unison => FromValue(IntervalSizeValues.UnisonValue);
    public static IntervalSize Second => FromValue(IntervalSizeValues.SecondValue);
    public static IntervalSize Third => FromValue(IntervalSizeValues.ThirdValue);
    public static IntervalSize Fourth => FromValue(IntervalSizeValues.FourthValue);
    public static IntervalSize Fifth => FromValue(IntervalSizeValues.FifthValue);
    public static IntervalSize Sixth => FromValue(IntervalSizeValues.SixthValue);
    public static IntervalSize Seventh => FromValue(IntervalSizeValues.SeventhValue);
    public static IntervalSize Octave => FromValue(IntervalSizeValues.OctaveValue);

    public static IReadOnlyCollection<IntervalSize> Items => ValueObjectCollection<IntervalSize>.Create();
    public static IReadOnlyCollection<int> Values => Items.ToValues();
    public static IReadOnlyCollection<IntervalSize> Range(int start, int count) => ValueObjectUtils<IntervalSize>.GetRange(start, count);
    public static IReadOnlyCollection<IntervalSize> Range(int count) => ValueObjectUtils<IntervalSize>.GetRange(-_minValue, count);

    public CompoundIntervalSize ToCompound() => new() {Value = _value + 8};

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

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    /// <summary>
    /// Gets available qualities ({d, P, A} if perfect interval, {d, m, M, A} if imperfect interval) 
    /// </summary>
    /// <returns>The <see cref="IReadOnlyCollection{Quality}"/>.</returns>
    public IReadOnlySet<IntervalQuality> AvailableQualities => Consonance == IntervalSizeConsonance.Perfect ? PerfectQualities : ImperfectQualities;

    public IntervalSizeConsonance Consonance => _value switch
    {
        IntervalSizeValues.UnisonValue or IntervalSizeValues.FourthValue or IntervalSizeValues.FifthValue or IntervalSizeValues.OctaveValue => IntervalSizeConsonance.Perfect,
        _ => IntervalSizeConsonance.Imperfect
    };

    /// <summary>
    /// Gets the inverse interval diatonic interval number.
    /// </summary>
    /// <remarks>
    /// Inverse diatonic intervals add up to 9 - see explanation here: https://www.essential-music-theory.com/inverted-intervals.html
    /// </remarks>
    /// <returns>The <see cref="IntervalSize"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntervalSize ToInverse() => FromValue(9 - Value);

    /// <summary>
    /// Get the semitones distance for the interval.
    /// </summary>
    /// <returns>The <see cref="Semitones"/></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public Semitones ToSemitones() => Value switch
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

