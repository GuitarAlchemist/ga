using PCRE;

namespace GA.Business.Core.Notes.Primitives;

[PublicAPI]
public readonly record struct SharpAccidental : IValue<SharpAccidental>
{
    private const int _minValue = 1;
    private const int _maxValue = 2;

    public static SharpAccidental Min => new() { Value = _minValue };
    public static SharpAccidental Max => new() { Value = _maxValue };

    public static readonly SharpAccidental Sharp = new() { Value = 1 };
    public static readonly SharpAccidental DoubleSharp = new() { Value = 2 };

    //language=regexp
    public static readonly string RegexPattern = "^(#|x)$";
    private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

    public static bool TryParse(string s, out SharpAccidental parsedAccidental)
    {
        parsedAccidental = default;
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
        parsedAccidental = parsedSharpAccidental.Value;
        return true;
    }

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueUtils<SharpAccidental>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<SharpAccidental>.CheckRange(value, minValue, maxValue);

    public override string ToString()
    {
        return _value switch
        {
            1 => "#",
            2 => "x",
            _ => string.Empty
        };
    }
}