namespace GA.Business.Core.Intervals;

/// <summary>
///     Octave (Double-contra | sub-contra | contra | great | small | 1 line | 2 lines | 3 lines | 4 lines | 5 lines | 6
///     lines)
/// </summary>
/// <remarks>
///     Implements <see cref="IRangeValueObject{Octave}" />, <see cref="IFormattable" />
/// </remarks>
[PublicAPI]
public readonly record struct Octave : IRangeValueObject<Octave>,
    IFormattable
{
    private const int _minValue = -1;
    private const int _maxValue = 8;

    private readonly int _value;

    public static Octave DoubleContra => FromValue(-1);
    public static Octave SubContra => FromValue(0);
    public static Octave Contra => FromValue(1);
    public static Octave Great => FromValue(2);
    public static Octave Small => FromValue(3);
    public static Octave OneLine => FromValue(4);
    public static Octave TwoLine => FromValue(5);
    public static Octave ThreeLine => FromValue(6);
    public static Octave FourLine => FromValue(7);
    public static Octave FiveLine => FromValue(8);
    public static Octave SixLine => FromValue(9);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return format switch
        {
            "d" => Value.ToString(),
            _ => ToString()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Octave FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new Octave { Value = value };
    }

    public static Octave Min => FromValue(_minValue);
    public static Octave Max => FromValue(_maxValue);

    public static implicit operator Octave(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(Octave octave)
    {
        return octave.Value;
    }

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    public static Octave operator ++(Octave octave)
    {
        return FromValue(octave.Value + 1);
    }

    public static Octave operator --(Octave octave)
    {
        return FromValue(octave.Value - 1);
    }

    public static int CheckRange(int value)
    {
        return ValueObjectUtils<Octave>.EnsureValueRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return ValueObjectUtils<Octave>.EnsureValueRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value switch
        {
            -1 => "double-contra (-1)",
            0 => "sub-contra (0)",
            1 => "contra (1)",
            2 => "great (2)",
            3 => "small (3)",
            4 => "one-line (4)",
            5 => "two-line (5)",
            6 => "three-line (6)",
            7 => "four-line (7)",
            8 => "five-line (8)",
            _ => ""
        };
    }

    #region Relational members

    public int CompareTo(Octave other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(Octave left, Octave right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Octave left, Octave right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Octave left, Octave right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Octave left, Octave right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
