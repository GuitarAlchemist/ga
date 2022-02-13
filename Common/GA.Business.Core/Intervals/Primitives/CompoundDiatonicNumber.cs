namespace GA.Business.Core.Intervals.Primitives;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An compound diatonic interval number
/// </summary>
/// <remarks>
/// https://en.wikipedia.org/wiki/Interval_(music)#Compound_intervals
/// </remarks>
[PublicAPI]
public readonly record struct CompoundDiatonicNumber : IDiatonicNumber<CompoundDiatonicNumber>
{
    #region Relational members

    public int CompareTo(CompoundDiatonicNumber other) => _value.CompareTo(other._value);
    public static bool operator <(CompoundDiatonicNumber left, CompoundDiatonicNumber right) => left.CompareTo(right) < 0;
    public static bool operator >(CompoundDiatonicNumber left, CompoundDiatonicNumber right) => left.CompareTo(right) > 0;
    public static bool operator <=(CompoundDiatonicNumber left, CompoundDiatonicNumber right) => left.CompareTo(right) <= 0;
    public static bool operator >=(CompoundDiatonicNumber left, CompoundDiatonicNumber right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 9;
    private const int _maxValue = 16;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CompoundDiatonicNumber Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static CompoundDiatonicNumber Min => Create(_minValue);
    public static CompoundDiatonicNumber Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<CompoundDiatonicNumber>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<CompoundDiatonicNumber>.CheckRange(value, minValue, maxValue);

    public static implicit operator CompoundDiatonicNumber(int value) => new() { Value = value };
    public static implicit operator int(CompoundDiatonicNumber number) => number._value;

    public static CompoundDiatonicNumber Ninth => Create(9);
    public static CompoundDiatonicNumber Tenth => Create(10);
    public static CompoundDiatonicNumber Eleventh => Create(11);
    public static CompoundDiatonicNumber Twelfth => Create(12);
    public static CompoundDiatonicNumber Thirteenth => Create(13);
    public static CompoundDiatonicNumber Fourteenth => Create(14);
    public static CompoundDiatonicNumber Fifteenth => Create(15);
    public static CompoundDiatonicNumber DoubleOctave => Create(16);

    public static IReadOnlyCollection<CompoundDiatonicNumber> All => ValueUtils<CompoundDiatonicNumber>.GetAll();
    public static IReadOnlyCollection<int> AllValues => All.Select(number => number .Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public DiatonicNumber ToSimple() => new() {Value = _value - 8};

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
    public bool IsPerfect => ToSimple().IsPerfect;
}

