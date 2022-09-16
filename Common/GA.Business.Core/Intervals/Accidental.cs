namespace GA.Business.Core.Intervals;

using Primitives;
using GA.Business.Core.Notes.Primitives;

/// <summary>
/// Signature.
/// </summary>
/// <see href="http://en.wikipedia.org/wiki/Accidental_(Objects)" />
public readonly record struct Accidental : IValueObject<Accidental>

{
    private const int _minValue = -3;
    private const int _maxValue = 3;

    public static Accidental Min => FromValue(_minValue);
    public static Accidental Max => FromValue(_maxValue);

    public static readonly Accidental TripleFlat = FromValue(-3);
    public static readonly Accidental DoubleFlat = FromValue(-2);
    public static readonly Accidental Flat = FromValue(-1);
    public static readonly Accidental Natural = FromValue(0);
    public static readonly Accidental Sharp = FromValue(1);
    public static readonly Accidental DoubleSharp = FromValue(2);
    public static readonly Accidental TripleSharp = FromValue(3);
    public static Accidental FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Accidental operator !(Accidental accidental) => FromValue(-accidental.Value);
    public static Accidental operator -(Accidental accidental) => FromValue(-accidental.Value);
    public static Accidental operator ++(Accidental accidental) => FromValue(accidental.Value + 1);
    public static Accidental operator --(Accidental accidental) => FromValue(accidental.Value - 1);
    public static Accidental operator +(Accidental a, Accidental b) => FromValue(a.Value + b.Value);
    public static Accidental operator -(Accidental a, Accidental b) => FromValue(a.Value - b.Value);

    public static implicit operator Accidental(int value) => FromValue(value);
    public static implicit operator int(Accidental fret) => fret.Value;

    public static implicit operator Accidental?(SharpAccidental? sharpAccidental) => sharpAccidental == null ? null : FromValue(sharpAccidental.Value.Value);
    public static implicit operator Accidental?(FlatAccidental? flatAccidental) => flatAccidental == null ? null : FromValue(flatAccidental.Value.Value);
    public static implicit operator Semitones(Accidental value) => value.ToSemitones();

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = IValueObject<Accidental>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public override string ToString() => _value switch
    {
        -3 => "bbb",
        -2 => "bb",
        -1 => "b",
        0 => "\u266E",
        1 => "#",
        2 => "x",
        3 => "#x",
        _ => string.Empty
    };

    public Semitones ToSemitones() => new() {Value = _value};
}


