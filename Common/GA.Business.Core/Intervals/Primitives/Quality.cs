namespace GA.Business.Core.Intervals.Primitives;

using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An interval quality
/// </summary>
[PublicAPI]
public readonly record struct Quality : IValue<Quality>, IFormattable
{
    #region Relational members

    public int CompareTo(Quality other) => _value.CompareTo(other._value);
    public static bool operator <(Quality left, Quality right) => left.CompareTo(right) < 0;
    public static bool operator >(Quality left, Quality right) => left.CompareTo(right) > 0;
    public static bool operator <=(Quality left, Quality right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Quality left, Quality right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -3;
    private const int _maxValue = 3;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Quality Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public const int DoublyDiminishedValue = -3;
    public const int DiminishedValue = -2;
    public const int MinorValue = -1;
    public const int PerfectValue = 0;
    public const int MajorValue = 1;
    public const int AugmentedValue = 2;
    public const int DoublyAugmentedValue = 3;

    public static Quality Min => Create(_minValue);
    public static Quality Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<Quality>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Quality>.CheckRange(value, minValue, maxValue);

    public static implicit operator Quality(int value) => new() { Value = value };
    public static implicit operator int(Quality quality) => quality._value;
    public static Quality operator !(Quality quality) => quality.ToInverse();

    public static Quality DoublyDiminished => Create(DoublyDiminishedValue);
    public static Quality Diminished => Create(DiminishedValue);
    public static Quality Minor => Create(MinorValue);
    public static Quality Perfect => Create(PerfectValue);
    public static Quality Major => Create(MajorValue);
    public static Quality Augmented => Create(AugmentedValue);
    public static Quality DoublyAugmented => Create(DoublyAugmentedValue);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    /// <summary>
    /// Create a new Quality instance with the inverse quality value..
    /// </summary>
    /// <returns>
    /// The inverse <see cref="Quality"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quality ToInverse() => Create(-Value);

    public string LongName => Value switch
    {
        -3 => nameof(DoublyDiminished),
        -2 => nameof(Diminished),
        -1 => nameof(Minor),
        0 => nameof(Perfect),
        1 => nameof(Major),
        2 => nameof(Augmented),
        3 => nameof(DoublyAugmented),
        _ => throw new ArgumentOutOfRangeException()
    };

    public string ShortName => Value switch
    {
        -3 => "dd",
        -2 => "d",
        -1 => "m",
        0 => "P",
        1 => "M",
        2 => "A",
        3 => "AA",
        _ => throw new ArgumentOutOfRangeException()
    };

    public override string ToString() => ToString("G");
    public string ToString(string format) => ToString(format, null);
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        format ??= "G";
        return format.ToUpperInvariant() switch
        {
            "G" => ShortName,
            "L" => LongName,
            "S" => ShortName,
            _ => throw new FormatException($"The {format} format string is not supported.")
        };
    }

    public Accidental? ToAccidental(bool isPerfectInterval)
    {
        return isPerfectInterval
            ? GetPerfectIntervalAccidental(_value)
            : GetImperfectIntervalAccidental(_value);

        static Accidental? GetPerfectIntervalAccidental(in int value)
        {
            return value switch
            {
                DoublyDiminishedValue => Accidental.DoubleFlat,
                DiminishedValue => Accidental.Flat,
                PerfectValue => null,
                AugmentedValue => Accidental.Sharp,
                DoublyAugmentedValue => Accidental.DoubleSharp,
                _ => throw new InvalidOperationException()
            };
        }

        static Accidental? GetImperfectIntervalAccidental(in int value)
        {
            return value switch
            {
                DoublyDiminishedValue => Accidental.TripleFlat,
                DiminishedValue => Accidental.DoubleFlat,
                MinorValue => Accidental.Flat,
                MajorValue => null,
                AugmentedValue => Accidental.Sharp,
                DoublyAugmentedValue => Accidental.DoubleSharp,
                _ => throw new InvalidOperationException()
            };
        }
    }
}

