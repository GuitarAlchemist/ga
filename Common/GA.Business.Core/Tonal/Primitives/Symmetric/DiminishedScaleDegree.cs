namespace GA.Business.Core.Tonal.Primitives.Symmetric;

/// <summary>
/// A diminished scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Octatonic_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct DiminishedScaleDegree : IRangeValueObject<DiminishedScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(DiminishedScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(DiminishedScaleDegree left, DiminishedScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(DiminishedScaleDegree left, DiminishedScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(DiminishedScaleDegree left, DiminishedScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DiminishedScaleDegree left, DiminishedScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 8;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DiminishedScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public DiminishedScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static DiminishedScaleDegree Min => FromValue(_minValue);
    public static DiminishedScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<DiminishedScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<DiminishedScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator DiminishedScaleDegree(int value) => FromValue(value);
    public static implicit operator int(DiminishedScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<DiminishedScaleDegree> All => ValueObjectUtils<DiminishedScaleDegree>.Items;
    public static IReadOnlyCollection<DiminishedScaleDegree> Items => ValueObjectUtils<DiminishedScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static DiminishedScaleDegree HalfWhole => new(1);
    public static DiminishedScaleDegree WholeHalf => new(2);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Half-whole diminished",
        2 => "Whole-half diminished",
        _ => $"Diminished mode {Value}"
    };

    public string ToShortName() => Value switch
    {
        1 => "H-W",
        2 => "W-H",
        _ => $"Dim{Value}"
    };
}
