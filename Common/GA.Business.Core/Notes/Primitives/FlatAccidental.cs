namespace GA.Business.Core.Notes.Primitives;

using GA.Core;
using GA.Core.Collections;
using GA.Business.Core.Intervals.Primitives;

[PublicAPI]
public readonly record struct FlatAccidental : IRangeValueObject<FlatAccidental>
{
    private const int _minValue = -3;
    private const int _maxValue = 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FlatAccidental FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static FlatAccidental Min => new() { Value = _minValue };
    public static FlatAccidental Max => new() { Value = _maxValue };

    public static readonly FlatAccidental TripleFlat = new() { Value = -3 };
    public static readonly FlatAccidental DoubleFlat = new() { Value = -2 };
    public static readonly FlatAccidental Flat = new() { Value = -1 };

    public static implicit operator FlatAccidental(int value) => new() {Value = value};
    public static implicit operator int(FlatAccidental item) => item.Value;
    public static implicit operator Semitones(FlatAccidental value) => value.ToSemitones();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }
    public static int CheckRange(int value) => ValueObjectUtils<FlatAccidental>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<FlatAccidental>.CheckRange(value, minValue, maxValue);

    public override string ToString() => _value switch
    {
        -3 => "bbb",
        -2 => "bb",
        -1 => "b",
        _ => string.Empty
    };

    public Semitones ToSemitones() => new() {Value = _value};
}