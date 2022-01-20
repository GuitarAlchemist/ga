using System.Runtime.CompilerServices;

namespace GA.Business.Core.Intervals.Primitives;

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

    public static Quality Min => Create(_minValue);
    public static Quality Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<Quality>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Quality>.CheckRange(value, minValue, maxValue);

    public static implicit operator Quality(int value) => new() { Value = value };
    public static implicit operator int(Quality quality) => quality._value;
    public static Quality operator !(Quality quality) => quality.ToInverse();

    public static Quality DoublyDiminished => Create(-3);
    public static Quality Diminished => Create(-2);
    public static Quality Minor => Create(-1);
    public static Quality Perfect => Create(0);
    public static Quality Major => Create(1);
    public static Quality Augmented => Create(2);
    public static Quality DoublyAugmented => Create(3);

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

    public string Name => Value switch
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
            "V" => Value.ToString(),
            "N" => Name,
            "S" => ShortName,
            _ => throw new FormatException($"The {format} format string is not supported.")
        };
    }
}

