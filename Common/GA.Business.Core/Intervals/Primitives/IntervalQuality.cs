namespace GA.Business.Core.Intervals.Primitives;

// ReSharper disable GrammarMistakeInComment
/// <summary>
/// Interval quality class
/// </summary>
/// <remarks>
/// Implements <see cref="IEquatable{String}" /> | <see cref="IComparable{String}" /> | <see cref="IComparable" />
/// <code>
///               #                            #<br/>
/// Major =================&gt; Augmented  &lt;================= Perfect<br/>
///       ====&gt; Minor =====&gt; Diminished &lt;=================<br/>
///         b           b
/// </code>
/// <br/>
/// </remarks>
// ReSharper restore GrammarMistakeInComment
[PublicAPI]
public readonly record struct IntervalQuality : IParsable<IntervalQuality>,
                                                IRangeValueObject<IntervalQuality>,
                                                IFormattable
{
    #region IParsable{IntervalQuality}

    /// <inheritdoc />
    public static IntervalQuality Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out IntervalQuality result)
    {
        result = default!;

        IntervalQuality? intervalQuality = s switch
        {
            "dd" => DoublyDiminished,
            "d" => Diminished,
            "m" => Minor,
            "P" => Perfect,
            "M" => Major,
            "A" => Augmented,
            "AA" => DoublyAugmented,
            _ => null
        };

        if (intervalQuality is not { } quality) return false; // Failure

        // Success
        result = quality;
        return true;
    }

    #endregion

    #region Relational members

    public int CompareTo(IntervalQuality other) => _value.CompareTo(other._value);
    public static bool operator <(IntervalQuality left, IntervalQuality right) => left.CompareTo(right) < 0;
    public static bool operator >(IntervalQuality left, IntervalQuality right) => left.CompareTo(right) > 0;
    public static bool operator <=(IntervalQuality left, IntervalQuality right) => left.CompareTo(right) <= 0;
    public static bool operator >=(IntervalQuality left, IntervalQuality right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = DoublyDiminishedValue;
    private const int _maxValue = DoublyAugmentedValue;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntervalQuality FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public const int DoublyDiminishedValue = -3;
    public const int DiminishedValue = -2;
    public const int MinorValue = -1;
    public const int PerfectValue = 0;
    public const int MajorValue = 1;
    public const int AugmentedValue = 2;
    public const int DoublyAugmentedValue = 3;

    public static IntervalQuality Min => FromValue(_minValue);
    public static IntervalQuality Max => FromValue(_maxValue);
    public static int CheckRange(int value) => ValueObjectUtils<IntervalQuality>.EnsureValueRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<IntervalQuality>.EnsureValueRange(value, minValue, maxValue);

    public static implicit operator IntervalQuality(int value) => new() { Value = value };
    public static implicit operator int(IntervalQuality intervalQuality) => intervalQuality._value;
    public static IntervalQuality operator !(IntervalQuality intervalQuality) => intervalQuality.ToInverse();

    public static IntervalQuality DoublyDiminished => FromValue(DoublyDiminishedValue);
    public static IntervalQuality Diminished => FromValue(DiminishedValue);
    public static IntervalQuality Minor => FromValue(MinorValue);
    public static IntervalQuality Perfect => FromValue(PerfectValue);
    public static IntervalQuality Major => FromValue(MajorValue);
    public static IntervalQuality Augmented => FromValue(AugmentedValue);
    public static IntervalQuality DoublyAugmented => FromValue(DoublyAugmentedValue);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    /// <summary>
    /// Create a new Quality instance with the inverse quality value
    /// </summary>
    /// <returns>
    /// The inverse <see cref="IntervalQuality"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntervalQuality ToInverse() => FromValue(-Value);

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

    /// <summary>
    /// Gets the accidental, given the consonance
    /// </summary>
    /// <param name="consonance">The <see cref="IntervalConsonance"/></param>
    /// <returns>The <see cref="Nullable{Accidental}"/></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Accidental? ToAccidental(IntervalConsonance consonance)
    {
        return consonance == IntervalConsonance.Perfect
            ? GetPerfectIntervalAccidental(_value)
            : GetImperfectIntervalAccidental(_value);

        static Accidental? GetPerfectIntervalAccidental(in int value) => value switch
        {
            DoublyDiminishedValue => Accidental.DoubleFlat, // dd => bb
            DiminishedValue => Accidental.Flat, // d => b
            PerfectValue => null,
            AugmentedValue => Accidental.Sharp, // A => #
            DoublyAugmentedValue => Accidental.DoubleSharp, // AA => ##
            _ => throw new InvalidOperationException()
        };

        static Accidental? GetImperfectIntervalAccidental(in int value) => value switch
        {
            DoublyDiminishedValue => Accidental.TripleFlat, // dd => bbb
            DiminishedValue => Accidental.DoubleFlat, // d => bb
            MinorValue => Accidental.Flat, // m => b
            MajorValue => null,
            AugmentedValue => Accidental.Sharp, // A => #
            DoublyAugmentedValue => Accidental.DoubleSharp, // AA => ##
            _ => throw new InvalidOperationException()
        };
    }

    public static bool TryGetFromAccidental(
        IntervalConsonance consonance, 
        Accidental? accidental, 
        out IntervalQuality quality)
    {
        var foundQuality =
            consonance == IntervalConsonance.Perfect
                ? GetPerfectIntervalAccidental(accidental)
                : GetImperfectIntervalAccidental(accidental);

        if (!foundQuality.HasValue)
        {
            // Failure
            quality = default!;
            return false;
        }

        // Success
        quality = foundQuality.Value;
        return true;

        static IntervalQuality? GetPerfectIntervalAccidental(Accidental? accidental) => accidental?.Value switch
        {
            -2 => DoublyDiminished,
            -1 => Diminished,
            1 => Augmented,
            2 => DoublyAugmented,
            _ => Perfect
        };

        static IntervalQuality? GetImperfectIntervalAccidental(Accidental? accidental) => accidental?.Value switch
        {
            -3 => DoublyDiminished,
            -2 => Diminished,
            -1 => Minor,
            1 => Augmented,
            2 => DoublyAugmented,
            _ => Major
        };
    }
}

