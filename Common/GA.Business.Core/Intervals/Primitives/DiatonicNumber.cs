namespace GA.Business.Core.Intervals.Primitives;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An diatonic interval number (Inclusive count of encompassed staff positions, e.g. C to G = 5th)
/// </summary>
/// <remarks>
/// https://en.wikipedia.org/wiki/Interval_(music)
/// https://hellomusictheory.com/learn/intervals/
/// </remarks>
[PublicAPI]
public readonly record struct DiatonicNumber : IDiatonicNumber<DiatonicNumber>
{
    #region Relational members

    public int CompareTo(DiatonicNumber other) => _value.CompareTo(other._value);
    public static bool operator <(DiatonicNumber left, DiatonicNumber right) => left.CompareTo(right) < 0;
    public static bool operator >(DiatonicNumber left, DiatonicNumber right) => left.CompareTo(right) > 0;
    public static bool operator <=(DiatonicNumber left, DiatonicNumber right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DiatonicNumber left, DiatonicNumber right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 8;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DiatonicNumber Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static DiatonicNumber Min => Create(_minValue);
    public static DiatonicNumber Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<DiatonicNumber>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<DiatonicNumber>.CheckRange(value, minValue, maxValue);

    public static implicit operator DiatonicNumber(int value) => new() { Value = value };
    public static implicit operator int(DiatonicNumber diatonicNumber) => diatonicNumber._value;
    public static DiatonicNumber operator +(DiatonicNumber diatonicNumber, int increment) => new() { Value = diatonicNumber.Value + increment % 7 };
    public static DiatonicNumber operator ++(DiatonicNumber diatonicNumber) => diatonicNumber + 1;
    public static DiatonicNumber operator !(DiatonicNumber diatonicNumber) => diatonicNumber.ToInverse();

    public static DiatonicNumber Unison => Create(1);
    public static DiatonicNumber Second => Create(2);
    public static DiatonicNumber Third => Create(3);
    public static DiatonicNumber Fourth => Create(4);
    public static DiatonicNumber Fifth => Create(5);
    public static DiatonicNumber Sixth => Create(6);
    public static DiatonicNumber Seventh => Create(7);
    public static DiatonicNumber Octave => Create(8);

    public static IReadOnlyCollection<DiatonicNumber> All => ValueUtils<DiatonicNumber>.GetAll();
    public static IReadOnlyCollection<DiatonicNumber> Range(int start, int count) => ValueUtils<DiatonicNumber>.GetRange(start, count);
    public static IReadOnlyCollection<DiatonicNumber> Range(int count) => ValueUtils<DiatonicNumber>.GetRange(-_minValue, count);

    public CompoundDiatonicNumber ToCompound() => new() {Value = _value + 8};

    /// <summary>
    /// Formula available for a perfect interval
    /// </summary>
    /// <remarks>
    /// See https://musictheory.pugetsound.edu/mt21c/AugmentedAndDiminishedIntervals.html
    /// </remarks>
    public static readonly IReadOnlySet<Quality> PerfectQualities = new[] {Quality.Diminished, Quality.Perfect, Quality.Augmented}.ToImmutableHashSet();

    /// <summary>
    /// Formula available for an imperfect interval.
    /// </summary>
    /// <remarks>
    /// See https://musictheory.pugetsound.edu/mt21c/AugmentedAndDiminishedIntervals.html
    /// </remarks>
    public static readonly IReadOnlySet<Quality> ImperfectQualities = new[] {Quality.Diminished, Quality.Minor, Quality.Major, Quality.Augmented }.ToImmutableHashSet();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    /// <summary>
    /// Gets available qualities ({d, P, A} if perfect interval, {d, m, M, A} if imperfect interval) 
    /// </summary>
    /// <returns>The <see cref="IReadOnlyCollection{Quality}"/>.</returns>
    public IReadOnlySet<Quality> AvailableQualities => IsPerfect ? PerfectQualities : ImperfectQualities;

    /// <summary>
    /// Indicates whether the diatonic number represent a perfect interval.
    /// </summary>
    /// <returns>True for perfect interval, false otherwise.</returns>
    public bool IsPerfect => IsPerfectInternal(this);

    /// <summary>
    /// Indicates whether the diatonic number represent an imperfect interval.
    /// </summary>
    /// <returns>True for imperfect interval, false otherwise.</returns>
    public bool IsImperfect => !IsPerfectInternal(this);

    /// <summary>
    /// Gets the inverse interval diatonic interval number.
    /// </summary>
    /// <remarks>
    /// Inverse diatonic intervals add up to 9 - see explanation here: https://www.essential-music-theory.com/inverted-intervals.html
    /// </remarks>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DiatonicNumber ToInverse() => Create(9 - Value);

    /// <summary>
    /// Create a chromatic interval
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public Semitones ToChromatic()
    {
        return Value switch
        {
            1 => 0,
            2 => 2, // Tone (+2)
            3 => 4, // Tone (+2)
            4 => 5, // Half-Tone (+1)
            5 => 7, // Tone (+2)
            6 => 9, // Tone (+2)
            7 => 11, // Tone (+2)
            8 => 12, // Half-Tone (+1)
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    private static bool IsPerfectInternal(DiatonicNumber number) => number.Value switch
    {
        1 or 4 or 5 or 8 => true, // Unison, Fourth, Fifth, Octave
        _ => false,
    };

    public override string ToString() => Value.ToString();
}

