namespace GA.Business.Core.Notes;

[PublicAPI]
public readonly record struct SharpAccidental : IValue<SharpAccidental>
{
    private const int _minValue = 1;
    private const int _maxValue = 2;

    public static SharpAccidental Min => new() { Value = _minValue };
    public static SharpAccidental Max => new() { Value = _maxValue };

    public static readonly SharpAccidental Sharp = new() { Value = 1 };
    public static readonly SharpAccidental DoubleSharp = new() { Value = 2 };

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