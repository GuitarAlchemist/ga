using System.Runtime.CompilerServices;

namespace GA.Business.Core.Intervals.Primitives;

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
public readonly record struct DiatonicNumber : IValue<DiatonicNumber>
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
    public static DiatonicNumber operator !(DiatonicNumber diatonicNumber) => diatonicNumber.ToInverse();

    public static DiatonicNumber Unison => Create(1);
    public static DiatonicNumber Second => Create(2);
    public static DiatonicNumber Third => Create(3);
    public static DiatonicNumber Fourth => Create(4);
    public static DiatonicNumber Fifth => Create(5);
    public static DiatonicNumber Sixth => Create(6);
    public static DiatonicNumber Seventh => Create(7);
    public static DiatonicNumber Octave => Create(8);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    /// <summary>
    /// Gets the inverse interval diatonic interval number.
    /// </summary>
    /// <remarks>
    /// Inverse diatonic intervals add up to 9 - see explanation here:  https://www.essential-music-theory.com/inverted-intervals.html
    /// </remarks>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DiatonicNumber ToInverse() => Create(9 - Value);

    public override string ToString() => Value.ToString();
}

