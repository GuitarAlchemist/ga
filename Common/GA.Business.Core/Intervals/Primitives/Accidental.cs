namespace GA.Business.Core.Intervals.Primitives;

using GA.Core;
using GA.Business.Core.Notes.Primitives;

/// <summary>
/// Accidental record struct (𝄫♭ | 𝄫 | ♭ | ♮ | ♯ | 𝄪 | ♯𝄪)
/// </summary>
/// <see href="http://en.wikipedia.org/wiki/Accidental_(Objects)" />
public readonly record struct Accidental : IRangeValueObject<Accidental>
{
    #region IRangeValueObject

    public static Accidental Min => FromValue(_minValue);
    public static Accidental Max => FromValue(_maxValue);

    public int Value
    {
        get => _value;
        init => _value = IRangeValueObject<Accidental>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    private readonly int _value;

    #endregion

    private const int _minValue = -3;
    private const int _maxValue = 3;

    /// <summary>
    /// Gets the "𝄫♭" <see cref="Accidental"/>
    /// </summary>
    public static Accidental TripleFlat => FromValue(-3);

    /// <summary>
    /// Gets the "𝄫" <see cref="Accidental"/>
    /// </summary>
    public static Accidental DoubleFlat => FromValue(-2);

    /// <summary>
    /// Gets the "♭" <see cref="Accidental"/>
    /// </summary>
    public static Accidental Flat => FromValue(-1);

    /// <summary>
    /// Gets the "♮" <see cref="Accidental"/>
    /// </summary>
    public static Accidental Natural => FromValue(0);

    /// <summary>
    /// Gets the "♯" <see cref="Accidental"/>
    /// </summary>
    public static Accidental Sharp => FromValue(1);

    /// <summary>
    /// Gets the "𝄪" <see cref="Accidental"/>
    /// </summary>
    public static Accidental DoubleSharp => FromValue(2);

    /// <summary>
    /// Gets the "♯𝄪" <see cref="Accidental"/>
    /// </summary>
    public static Accidental TripleSharp => FromValue(3);

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

    public override string ToString() => _value switch
    {
        -3 => "𝄫♭",
        -2 => "𝄫",
        -1 => "♭",
        0 => "♮",
        1 => "♯",
        2 => "𝄪",
        3 => "♯𝄪",
        _ => string.Empty
    };

    public Semitones ToSemitones() => new() { Value = _value };
}


