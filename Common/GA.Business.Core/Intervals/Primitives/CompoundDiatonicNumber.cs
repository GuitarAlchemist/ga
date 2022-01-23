using System.Runtime.CompilerServices;

namespace GA.Business.Core.Intervals.Primitives;

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
    private const int _maxValue = 15;
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
    public static CompoundDiatonicNumber DoubleOctave => Create(15);

    public static IReadOnlyCollection<CompoundDiatonicNumber> All => ValueUtils<CompoundDiatonicNumber>.GetAll();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public DiatonicNumber ToSimple() => new() {Value = _value - 8};

    public override string ToString() => Value.ToString();
}

