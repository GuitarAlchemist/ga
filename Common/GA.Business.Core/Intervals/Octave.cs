namespace GA.Business.Core.Intervals;

using GA.Core;
using GA.Core.Collections;

[PublicAPI]
public readonly record struct Octave : IValueObject<Octave>, 
                                       IFormattable
{
    #region Relational members

    public int CompareTo(Octave other) => _value.CompareTo(other._value);
    public static bool operator <(Octave left, Octave right) => left.CompareTo(right) < 0;
    public static bool operator >(Octave left, Octave right) => left.CompareTo(right) > 0;
    public static bool operator <=(Octave left, Octave right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Octave left, Octave right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -1;
    private const int _maxValue = 8;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Octave FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Octave Min => FromValue(_minValue);
    public static Octave Max => FromValue(_maxValue);

    public static Octave DoubleContra => FromValue(-1);
    public static Octave SubContra => FromValue(0);
    public static Octave Contra =>  FromValue(1);
    public static Octave Great => FromValue(2);
    public static Octave Small => FromValue(3);
    public static Octave OneLine => FromValue(4);
    public static Octave TwoLine => FromValue(5);
    public static Octave ThreeLine => FromValue(6);
    public static Octave FourLine => FromValue(7);
    public static Octave FiveLine => FromValue(8);
    public static Octave SixLine => FromValue(9);

    public static Octave operator ++(Octave octave) => FromValue(octave.Value + 1);
    public static Octave operator --(Octave octave) => FromValue(octave.Value - 1);
    public static implicit operator Octave(int value) => FromValue(value);
    public static implicit operator int(Octave octave) => octave.Value;

    public static int CheckRange(int value) => ValueObjectUtils<Octave>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<Octave>.CheckRange(value, minValue, maxValue);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value switch
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

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return format switch
        {
            "d" => Value.ToString(),
            _ => ToString()
        };
    }
}

