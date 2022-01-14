using System.Runtime.CompilerServices;

namespace GA.Business.Core.Intervals;

[PublicAPI]
public readonly record struct Octave : IValue<Octave>, IFormattable
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
    private static Octave Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Octave Min => Create(_minValue);
    public static Octave Max => Create(_maxValue);

    public static Octave DoubleContra => Create(-1);
    public static Octave SubContra => Create(0);
    public static Octave Contra =>  Create(1);
    public static Octave Great => Create(2);
    public static Octave Small => Create(3);
    public static Octave OneLine => Create(4);
    public static Octave TwoLine => Create(5);
    public static Octave ThreeLine => Create(6);
    public static Octave FourLine => Create(7);
    public static Octave FiveLine => Create(8);
    public static Octave SixLine => Create(9);

    public static Octave operator ++(Octave octave) => Create(octave.Value + 1);
    public static Octave operator --(Octave octave) => Create(octave.Value - 1);
    public static implicit operator Octave(int value) => Create(value);
    public static implicit operator int(Octave octave) => octave.Value;

    public static int CheckRange(int value) => ValueUtils<Octave>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Octave>.CheckRange(value, minValue, maxValue);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

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

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return format switch
        {
            "d" => Value.ToString(),
            _ => ToString()
        };
    }
}

