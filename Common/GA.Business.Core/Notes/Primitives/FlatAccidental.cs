namespace GA.Business.Core.Notes.Primitives;

using GA.Business.Core.Intervals.Primitives;

/// <summary>
/// Flat accidental (bbb | bbb | bb)
/// </summary>
/// <remarks>
/// Implements <see cref="IRangeValueObject{FlatAccidental}"/>
/// </remarks>
[PublicAPI]
public readonly record struct FlatAccidental : IRangeValueObject<FlatAccidental>, IParsable<FlatAccidental>
{
    #region IRangeValueObject Members

    public static FlatAccidental Min => new() { Value = _minValue };
    public static FlatAccidental Max => new() { Value = _maxValue };
    public int Value { get => _value; init => _value = CheckRange(value); }

    public static implicit operator FlatAccidental(int value) => new() { Value = value };
    public static implicit operator int(FlatAccidental item) => item.Value;

    public static int CheckRange(int value) => ValueObjectUtils<FlatAccidental>.EnsureValueRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<FlatAccidental>.EnsureValueRange(value, minValue, maxValue);

    #endregion

    #region IParsable Members

    //language=regexp
    public static readonly string RegexPattern = "^(#|x)$";
    private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

    /// <inheritdoc />
    public static FlatAccidental Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        return result;
    }
    
    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out FlatAccidental result)
    {
        result = default;
        
        if (string.IsNullOrWhiteSpace(s)) return false; // Failure (Empty string)
        var match = _regex.Match(s);
        if (!match.Success) return false; // Failure

        var group = match.Groups[1];
        FlatAccidental? parsedFlatAccidental = group.Value.ToUpperInvariant() switch
        {
            "#" => Flat,
            "x" => DoubleFlat,
            _ => null
        };

        if (!parsedFlatAccidental.HasValue) return false; // Failure

        // Success
        result = parsedFlatAccidental.Value;
        return true;
    }

    #endregion

    public static readonly FlatAccidental TripleFlat = new() { Value = -3 };
    public static readonly FlatAccidental DoubleFlat = new() { Value = -2 };
    public static readonly FlatAccidental Flat = new() { Value = -1 };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FlatAccidental FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    private const int _minValue = -3;
    private const int _maxValue = -1;
    private readonly int _value;

    public static implicit operator Semitones(FlatAccidental value) => value.ToSemitones();
    public Semitones ToSemitones() => new() { Value = _value };

    public override string ToString() => _value switch
    {
        -3 => "bbb",
        -2 => "bb",
        -1 => "b",
        _ => string.Empty
    };
}