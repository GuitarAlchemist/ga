namespace GA.Business.Core.Intervals.Primitives;

/// <summary>
///     A compound interval size (Between 9 and 16 semitones)
/// </summary>
/// <remarks>
///     https://en.wikipedia.org/wiki/Interval_(Objects)#Compound_intervals
///     Implements <see cref="IIntervalSize{CompoundIntervalSize}" />
/// </remarks>
[PublicAPI]
public readonly record struct CompoundIntervalSize : IParsable<CompoundIntervalSize>,
    IIntervalSize<CompoundIntervalSize>
{
    private const int _minValue = 9;
    private const int _maxValue = 16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CompoundIntervalSize FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new CompoundIntervalSize { Value = value };
    }

    public static CompoundIntervalSize Min => FromValue(_minValue);
    public static CompoundIntervalSize Max => FromValue(_maxValue);

    public static implicit operator CompoundIntervalSize(int value)
    {
        return new CompoundIntervalSize { Value = value };
    }

    public static implicit operator int(CompoundIntervalSize size)
    {
        return size._value;
    }

    public static int CheckRange(int value)
    {
        return ValueObjectUtils<CompoundIntervalSize>.EnsureValueRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return ValueObjectUtils<CompoundIntervalSize>.EnsureValueRange(value, minValue, maxValue);
    }

    /// <summary>
    ///     Gets the simple interval for the current compound interval
    /// </summary>
    /// <returns>The <see cref="SimpleIntervalSize" /></returns>
    public SimpleIntervalSize ToSimple()
    {
        return new SimpleIntervalSize { Value = _value - 8 };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }

    #region Inner Classes

    private static class CompoundIntervalSizeValues
    {
        public const int NinthValue = 9;
        public const int TenthValue = 10;
        public const int EleventhValue = 11;
        public const int TwelfthValue = 12;
        public const int ThirteenthValue = 13;
        public const int FourteenthValue = 14;
        public const int FifteenthValue = 15;
        public const int SixteenthValue = 16;
    }

    #endregion

    #region IStaticValueObjectList<CompoundIntervalSize> Members

    public static IReadOnlyCollection<CompoundIntervalSize> Items => ValueObjectUtils<CompoundIntervalSize>.Items;
    public static IReadOnlyList<int> Values => Items.Select(number => number.Value).ToImmutableList();

    #endregion

    #region IParsable Members

    /// <inheritdoc />
    public static CompoundIntervalSize Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        }

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out CompoundIntervalSize result)
    {
        if (!int.TryParse(s, out var i))
        {
            throw new ArgumentException("Invalid format");
        }

        result = FromValue(i);
        return true;
    }

    #endregion

    #region IValueObject<CompoundIntervalSize>

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    #endregion

    #region IIntervalSize Members

    public IntervalConsonance Consonance => _value switch
    {
        CompoundIntervalSizeValues.EleventhValue
            or
            CompoundIntervalSizeValues.TwelfthValue
            or
            CompoundIntervalSizeValues.SixteenthValue
            => IntervalConsonance.Perfect,
        _ => IntervalConsonance.Imperfect
    };

    /// <summary>
    ///     Get the semitones distance for the interval.
    /// </summary>
    /// <returns>The <see cref="Primitives.Semitones" /></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public Semitones Semitones => Value switch
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

    #endregion

    #region Relational members

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return 1;
        }

        if (obj is IIntervalSize intervalSize)
        {
            return _value.CompareTo(intervalSize.Value);
        }

        return 1;
    }

    public static bool operator <(CompoundIntervalSize left, CompoundIntervalSize right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(CompoundIntervalSize left, CompoundIntervalSize right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(CompoundIntervalSize left, CompoundIntervalSize right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(CompoundIntervalSize left, CompoundIntervalSize right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion

    #region Well-known Values

    public static CompoundIntervalSize Ninth => FromValue(CompoundIntervalSizeValues.NinthValue);
    public static CompoundIntervalSize Tenth => FromValue(CompoundIntervalSizeValues.TenthValue);
    public static CompoundIntervalSize Eleventh => FromValue(CompoundIntervalSizeValues.EleventhValue);
    public static CompoundIntervalSize Twelfth => FromValue(CompoundIntervalSizeValues.TwelfthValue);
    public static CompoundIntervalSize Thirteenth => FromValue(CompoundIntervalSizeValues.ThirteenthValue);
    public static CompoundIntervalSize Fourteenth => FromValue(CompoundIntervalSizeValues.FourteenthValue);
    public static CompoundIntervalSize Fifteenth => FromValue(CompoundIntervalSizeValues.FifteenthValue);
    public static CompoundIntervalSize DoubleOctave => FromValue(CompoundIntervalSizeValues.SixteenthValue);

    #endregion
}
