namespace GA.Business.Core.Notes.Primitives;

[PublicAPI]
public readonly record struct FlatAccidental : IValue<FlatAccidental>
{
    private const int _minValue = -3;
    private const int _maxValue = 2;

    public static FlatAccidental Min => new() { Value = _minValue };
    public static FlatAccidental Max => new() { Value = _maxValue };

    public static readonly FlatAccidental TripleFlat = new() { Value = -3 };
    public static readonly FlatAccidental DoubleFlat = new() { Value = -2 };
    public static readonly FlatAccidental Flat = new() { Value = -1 };

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueUtils<FlatAccidental>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<FlatAccidental>.CheckRange(value, minValue, maxValue);

    public override string ToString()
    {
        return _value switch
        {
            -3 => "bbb",
            -2 => "bb",
            -1 => "b",
            _ => string.Empty
        };
    }
}