using GA.Business.Core.Notes;
using GA.Business.Core.Notes.Primitives;

namespace GA.Business.Core.Intervals;

/// <summary>
/// Signature.
/// </summary>
/// <see href="http://en.wikipedia.org/wiki/Accidental_(music)" />
public readonly record struct Accidental : IValue<Accidental>, IAll<Accidental>
{
    private const int _minValue = -3;
    private const int _maxValue = 2;

    public static Accidental Min => Create(_minValue);
    public static Accidental Max => Create(_maxValue);

    public static readonly Accidental TripleFlat = Create(-3);
    public static readonly Accidental DoubleFlat = Create(-2);
    public static readonly Accidental Flat = Create(-1);
    public static readonly Accidental Natural = Create(0);
    public static readonly Accidental Sharp = Create(1);
    public static readonly Accidental DoubleSharp = Create(2);
    public static IReadOnlyCollection<Accidental> All => ValueUtils<Accidental>.All();
    public static Accidental Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Accidental operator !(Accidental accidental) => Create(-accidental.Value);
    public static Accidental operator ++(Accidental accidental) => Create(accidental.Value + 1);
    public static Accidental operator --(Accidental accidental) => Create(accidental.Value - 1);
    public static Accidental operator +(Accidental a, Accidental b) => Create(a.Value + b.Value);
    public static Accidental operator -(Accidental a, Accidental b) => Create(a.Value - b.Value);

    public static implicit operator Accidental(int value) => Create(value);
    public static implicit operator Accidental?(SharpAccidental? sharpAccidental) => sharpAccidental == null ? null : Create(sharpAccidental.Value.Value);
    public static implicit operator Accidental?(FlatAccidental? flatAccidental) => flatAccidental == null ? null : Create(flatAccidental.Value.Value);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueUtils<Accidental>.CheckRange(value, _minValue, _maxValue);

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


