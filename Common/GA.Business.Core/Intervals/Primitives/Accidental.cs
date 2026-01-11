namespace GA.Business.Core.Intervals.Primitives;

using System;
using GA.Core.Abstractions;
using JetBrains.Annotations;
using Notes.Primitives;

/// <summary>
///     Accidental record struct (𝄫♭ | 𝄫 | ♭ | ♮ | ♯ | 𝄪 | ♯𝄪)
/// </summary>
/// <see href="http://en.wikipedia.org/wiki/Accidental_(Objects)" />
public readonly record struct Accidental : IRangeValueObject<Accidental>, IParsable<Accidental>
{
    private const int _minValue = -3;
    private const int _maxValue = 3;

    /// <summary>
    ///     Gets the "𝄫♭" <see cref="Accidental" />
    /// </summary>
    public static Accidental TripleFlat => FromValue(-3);

    /// <summary>
    ///     Gets the "𝄫" <see cref="Accidental" />
    /// </summary>
    public static Accidental DoubleFlat => FromValue(-2);

    /// <summary>
    ///     Gets the "♭" <see cref="Accidental" />
    /// </summary>
    public static Accidental Flat => FromValue(-1);

    /// <summary>
    ///     Gets the "♮" <see cref="Accidental" />
    /// </summary>
    public static Accidental Natural => FromValue(0);

    /// <summary>
    ///     Gets the "♯" <see cref="Accidental" />
    /// </summary>
    public static Accidental Sharp => FromValue(1);

    /// <summary>
    ///     Gets the "𝄪" <see cref="Accidental" />
    /// </summary>
    public static Accidental DoubleSharp => FromValue(2);

    /// <summary>
    ///     Gets the "♯𝄪" <see cref="Accidental" />
    /// </summary>
    public static Accidental TripleSharp => FromValue(3);

    public static Accidental FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new()
            { Value = value };
    }

    public static implicit operator Accidental(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(Accidental fret)
    {
        return fret.Value;
    }

    public static Accidental operator !(Accidental accidental)
    {
        return FromValue(-accidental.Value);
    }

    public static Accidental operator -(Accidental accidental)
    {
        return FromValue(-accidental.Value);
    }

    public static Accidental operator ++(Accidental accidental)
    {
        return FromValue(accidental.Value + 1);
    }

    public static Accidental operator --(Accidental accidental)
    {
        return FromValue(accidental.Value - 1);
    }

    public static Accidental operator +(Accidental a, Accidental b)
    {
        return FromValue(a.Value + b.Value);
    }

    public static Accidental operator -(Accidental a, Accidental b)
    {
        return FromValue(a.Value - b.Value);
    }

    public static implicit operator Accidental?(SharpAccidental? sharpAccidental)
    {
        return sharpAccidental == null ? null : FromValue(sharpAccidental.Value.Value);
    }

    public static implicit operator Accidental?(FlatAccidental? flatAccidental)
    {
        return flatAccidental == null ? null : FromValue(flatAccidental.Value.Value);
    }

    public static implicit operator Semitones(Accidental value)
    {
        return value.ToSemitones();
    }

    public override string ToString()
    {
        return _value switch
        {
            -3 => "bbb",
            -2 => "bb",
            -1 => "b",
            0 => "♮",
            1 => "♯",
            2 => "x",
            3 => "♯##",
            _ => string.Empty
        };
    }

    public Semitones ToSemitones()
    {
        return new()
            { Value = _value };
    }

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

    #region IParsable<Accidental> Members

    public static Accidental Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"Invalid accidental string format: '{s}'.");
        }

        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out Accidental result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default;
            return false;
        }

        // Normalize the input
        s = s.Trim().ToLowerInvariant();

        switch (s)
        {
            case "bbb" or "𝄫♭":
                result = TripleFlat;
                return true;
            case "bb" or "𝄫":
                result = DoubleFlat;
                return true;
            case "b" or "♭":
                result = Flat;
                return true;
            case "" or "n" or "♮":
                result = Natural;
                return true;
            case "#" or "♯":
                result = Sharp;
                return true;
            case "##" or "x" or "𝄪":
                result = DoubleSharp;
                return true;
            case "###" or "♯𝄪":
                result = TripleSharp;
                return true;
            default:
                result = default;
                return false;
        }
    }

    #endregion
}
