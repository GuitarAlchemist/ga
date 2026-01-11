namespace GA.Business.Core.Notes.Primitives;

using System;
using System.Runtime.CompilerServices;
using GA.Core.Abstractions;
using GA.Core.Collections;
using Intervals.Primitives;
using JetBrains.Annotations;
using PCRE;

/// <summary>
///     Sharp accidental (# | x)
/// </summary>
/// <remarks>
///     Implements <see cref="IRangeValueObject{TSelf}" />
/// </remarks>
[PublicAPI]
public readonly record struct SharpAccidental : IRangeValueObject<SharpAccidental>, IParsable<SharpAccidental>
{
    private const int _minValue = 1;
    private const int _maxValue = 2;
    private readonly int _value;

    public static SharpAccidental Sharp => FromValue(1);
    public static SharpAccidental DoubleSharp => FromValue(2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SharpAccidental FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new()
            { Value = value };
    }

    public static implicit operator Semitones(SharpAccidental value)
    {
        return value.ToSemitones();
    }

    public Semitones ToSemitones()
    {
        return new()
            { Value = _value };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _value switch
        {
            1 => "#",
            2 => "x",
            _ => string.Empty
        };
    }

    #region IRangeValueObject Members

    public static SharpAccidental Min => new() { Value = _minValue };
    public static SharpAccidental Max => new() { Value = _maxValue };

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    public static implicit operator SharpAccidental(int value)
    {
        return new()
            { Value = value };
    }

    public static implicit operator int(SharpAccidental item)
    {
        return item._value;
    }

    public static int CheckRange(int value)
    {
        return ValueObjectUtils<SharpAccidental>.EnsureValueRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return ValueObjectUtils<SharpAccidental>.EnsureValueRange(value, minValue, maxValue);
    }

    #endregion

    #region IParsable Members

    //language=regexp
    public static readonly string RegexPattern = "^(#|x)$";
    private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

    /// <inheritdoc />
    public static SharpAccidental Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        }

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out SharpAccidental result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false; // Failure (Empty string)
        }

        var match = _regex.Match(s);
        if (!match.Success)
        {
            return false; // Failure (No match)
        }

        var group = match.Groups[1];
        SharpAccidental? accidental = group.Value.ToUpperInvariant() switch
        {
            "#" => Sharp,
            "x" => DoubleSharp,
            _ => null
        };

        if (!accidental.HasValue)
        {
            return false; // Failure
        }

        // Success
        result = accidental.Value;
        return true;
    }

    #endregion
}
