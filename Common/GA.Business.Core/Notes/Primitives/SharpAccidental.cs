namespace GA.Business.Core.Notes.Primitives;

using GA.Business.Core.Intervals.Primitives;

/// <summary>
/// Sharp accidental (# | x)
/// </summary>
/// <remarks>
/// Implements <see cref="IRangeValueObject{SharpAccidental}"/>
/// </remarks>
[PublicAPI]
public readonly record struct SharpAccidental : IRangeValueObject<SharpAccidental>, IParsable<SharpAccidental>
{
    #region IRangeValueObject Members

    public static SharpAccidental Min => new() { Value = _minValue };
    public static SharpAccidental Max => new() { Value = _maxValue };
    public int Value { get => _value; init => _value = CheckRange(value); }

    public static implicit operator SharpAccidental(int value) => new() { Value = value };
    public static implicit operator int(SharpAccidental item) => item._value;

    public static int CheckRange(int value) => ValueObjectUtils<SharpAccidental>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<SharpAccidental>.CheckRange(value, minValue, maxValue);

    #endregion
    
    #region IParsable Members

    //language=regexp
    public static readonly string RegexPattern = "^(#|x)$";
    private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

    /// <inheritdoc />
    public static SharpAccidental Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out SharpAccidental result)
    {
        result = default;
        var match = _regex.Match(s);
        if (!match.Success) return false; // Failure

        var group = match.Groups[1];
        SharpAccidental? parsedSharpAccidental = group.Value.ToUpperInvariant() switch
        {
            "#" => Sharp,
            "x" => DoubleSharp,
            _ => null
        };

        if (!parsedSharpAccidental.HasValue) return false; // Failure

        // Success
        result = parsedSharpAccidental.Value;
        return true;
    }    
    
    #endregion

    public static SharpAccidental Sharp => FromValue(1);
    public static SharpAccidental DoubleSharp => FromValue(2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SharpAccidental FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    private const int _minValue = 1;
    private const int _maxValue = 2;
    private readonly int _value;

    public static implicit operator Semitones(SharpAccidental value) => value.ToSemitones();
    public Semitones ToSemitones() => new() {Value = _value};

    /// <inheritdoc />
    public override string ToString() => _value switch
    {
        1 => "#",
        2 => "x",
        _ => string.Empty
    };
}