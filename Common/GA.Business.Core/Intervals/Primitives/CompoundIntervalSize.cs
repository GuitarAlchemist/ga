namespace GA.Business.Core.Intervals.Primitives;

using GA.Core.Collections;

/// <summary>
/// An compound diatonic interval number
/// </summary>
/// <remarks>
/// https://en.wikipedia.org/wiki/Interval_(Objects)#Compound_intervals
/// </remarks>
[PublicAPI]
public readonly record struct CompoundIntervalSize : IIntervalSize<CompoundIntervalSize>
{
    #region Relational members

    public int CompareTo(CompoundIntervalSize other) => _value.CompareTo(other._value);
    public static bool operator <(CompoundIntervalSize left, CompoundIntervalSize right) => left.CompareTo(right) < 0;
    public static bool operator >(CompoundIntervalSize left, CompoundIntervalSize right) => left.CompareTo(right) > 0;
    public static bool operator <=(CompoundIntervalSize left, CompoundIntervalSize right) => left.CompareTo(right) <= 0;
    public static bool operator >=(CompoundIntervalSize left, CompoundIntervalSize right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 9;
    private const int _maxValue = 16;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CompoundIntervalSize FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static CompoundIntervalSize Min => FromValue(_minValue);
    public static CompoundIntervalSize Max => FromValue(_maxValue);
    public static int CheckRange(int value) => ValueObjectUtils<CompoundIntervalSize>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<CompoundIntervalSize>.CheckRange(value, minValue, maxValue);

    public static implicit operator CompoundIntervalSize(int value) => new() { Value = value };
    public static implicit operator int(CompoundIntervalSize size) => size._value;

    public static CompoundIntervalSize Ninth => FromValue(9);
    public static CompoundIntervalSize Tenth => FromValue(10);
    public static CompoundIntervalSize Eleventh => FromValue(11);
    public static CompoundIntervalSize Twelfth => FromValue(12);
    public static CompoundIntervalSize Thirteenth => FromValue(13);
    public static CompoundIntervalSize Fourteenth => FromValue(14);
    public static CompoundIntervalSize Fifteenth => FromValue(15);
    public static CompoundIntervalSize DoubleOctave => FromValue(16);

    public static IReadOnlyCollection<CompoundIntervalSize> Items => ValueObjectUtils<CompoundIntervalSize>.Items;
    public static ImmutableList<int> Values => Items.Select(number => number .Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public IntervalSize ToSimple() => new() {Value = _value - 8};

    /// <summary>
    /// Get the semitones distance for the interval.
    /// </summary>
    /// <returns>The <see cref="Semitones"/></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public Semitones ToSemitones()
    {
        return Value switch
        {
            9 => 12, // Octave
            10 => 14, // Octave + Tone (+2)
            11 => 16, // Octave +Tone (+2)
            12 => 17, // Octave + Half-Tone (+1)
            13 => 19, // Octave + Tone (+2)
            14 => 21, // Octave + Tone (+2)
            15 => 23, // Octave + Tone (+2)
            16 => 24, // Octave + Half-Tone (+1)
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public override string ToString() => Value.ToString();
    public IntervalSizeConsonance Consonance => ToSimple().Consonance;
}

