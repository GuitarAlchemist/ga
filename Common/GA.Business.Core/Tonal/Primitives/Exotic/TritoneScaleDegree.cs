namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
/// A Tritone scale degree (Petrushka scale)
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Tritone_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct TritoneScaleDegree : IRangeValueObject<TritoneScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(TritoneScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(TritoneScaleDegree left, TritoneScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(TritoneScaleDegree left, TritoneScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(TritoneScaleDegree left, TritoneScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(TritoneScaleDegree left, TritoneScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TritoneScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public TritoneScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static TritoneScaleDegree Min => FromValue(_minValue);
    public static TritoneScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<TritoneScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<TritoneScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator TritoneScaleDegree(int value) => FromValue(value);
    public static implicit operator int(TritoneScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<TritoneScaleDegree> All => ValueObjectUtils<TritoneScaleDegree>.Items;
    public static IReadOnlyCollection<TritoneScaleDegree> Items => ValueObjectUtils<TritoneScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static TritoneScaleDegree Tritone => new(1);
    public static TritoneScaleDegree Petrushka => new(2);
    public static TritoneScaleDegree TritoneMode3 => new(3);
    public static TritoneScaleDegree TritoneMode4 => new(4);
    public static TritoneScaleDegree TritoneMode5 => new(5);
    public static TritoneScaleDegree TritoneMode6 => new(6);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Tritone",
        2 => "Petrushka",
        3 => "Tritone mode 3",
        4 => "Tritone mode 4",
        5 => "Tritone mode 5",
        6 => "Tritone mode 6",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "Tri",
        2 => "Pet",
        3 => "Tri3",
        4 => "Tri4",
        5 => "Tri5",
        6 => "Tri6",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
