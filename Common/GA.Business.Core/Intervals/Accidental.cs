using GA.Business.Core.Notes;

namespace GA.Business.Core.Intervals;

/// <summary>
/// Accidental.
/// </summary>
/// <see href="http://en.wikipedia.org/wiki/Accidental_(music)" />
public readonly record struct Accidental : IValue<Accidental>, IAll<Accidental>
{
    private const int _minValue = -3;
    private const int _maxValue = 2;

    public static Accidental Min => new() { Value = _minValue };
    public static Accidental Max => new() { Value = _maxValue };

    public static readonly Accidental TripleFlat = new() { Value = -3 };
    public static readonly Accidental DoubleFlat = new() { Value = -2 };
    public static readonly Accidental Flat = new() { Value = -1 };
    public static readonly Accidental Natural = new() { Value = 0 };
    public static readonly Accidental Sharp = new() { Value = 1 };
    public static readonly Accidental DoubleSharp = new() { Value = 2 };
    public static IReadOnlyCollection<Accidental> All => ValueUtils<Accidental>.All();

    public static Accidental operator !(Accidental accidental) => new() { Value = -accidental.Value };
    public static Accidental operator ++(Accidental accidental) => new() { Value = accidental.Value + 1 };
    public static Accidental operator --(Accidental accidental) => new() { Value = accidental.Value - 1 };
    public static Accidental operator +(Accidental a, Accidental b) => new() { Value = a.Value + b.Value };
    public static Accidental operator -(Accidental a, Accidental b) => new() { Value = a.Value - b.Value };

    public static implicit operator Accidental(SharpAccidental sharpAccidental) => new() { Value = sharpAccidental.Value };
    public static implicit operator Accidental(FlatAccidental flatAccidental) => new() { Value = flatAccidental.Value };

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueUtils<Accidental>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Accidental>.CheckRange(value, minValue, maxValue);

    public override string ToString()
    {
        return _value switch
        {
            -3 => "bbb",
            -2 => "bb",
            -1 => "b",
            0 => "\u266E",
            1 => "#",
            2 => "x",
            3 => "???",
            _ => string.Empty
        };
    }
}


