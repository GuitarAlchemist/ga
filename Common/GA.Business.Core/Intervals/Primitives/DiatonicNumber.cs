using System.Runtime.CompilerServices;
using GA.Business.Core.Notes.Primitives;

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
public readonly record struct DiatonicNumber : IValue<DiatonicNumber>, IAll<DiatonicNumber>
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

    public Semitones ToSemitones()
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

    /// <summary>
    /// Indicates whether the diatonic number is perfect.(Unison, Fourth, Fifth, Octave)
    /// </summary>
    /// <returns>True is the diatonic number represents a perfect interval</returns>
    public bool IsPerfect()
    {
        switch (Value)
        {
            case 1:
            case 4:
            case 5:
            case 8:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Indicates whether the diatonic number is imperfect.(Second, Third, Sixth, Seventh)
    /// </summary>
    /// <returns>True is the diatonic number represents a perfect interval</returns>
    public bool IsImperfect() => !IsPerfect();

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

