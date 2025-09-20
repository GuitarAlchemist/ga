namespace GA.Business.Core.Tonal.Primitives.Pentatonic;

/// <summary>
/// A Hirajoshi scale degree (Japanese pentatonic scale)
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Hirajoshi_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct HirajoshiScaleDegree : IRangeValueObject<HirajoshiScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(HirajoshiScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(HirajoshiScaleDegree left, HirajoshiScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(HirajoshiScaleDegree left, HirajoshiScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(HirajoshiScaleDegree left, HirajoshiScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(HirajoshiScaleDegree left, HirajoshiScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 5;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HirajoshiScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public HirajoshiScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static HirajoshiScaleDegree Min => FromValue(_minValue);
    public static HirajoshiScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<HirajoshiScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<HirajoshiScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator HirajoshiScaleDegree(int value) => FromValue(value);
    public static implicit operator int(HirajoshiScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<HirajoshiScaleDegree> All => ValueObjectUtils<HirajoshiScaleDegree>.Items;
    public static IReadOnlyCollection<HirajoshiScaleDegree> Items => ValueObjectUtils<HirajoshiScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static HirajoshiScaleDegree Hirajoshi => new(1);
    public static HirajoshiScaleDegree HirajoshiKumoi => new(2);
    public static HirajoshiScaleDegree HirajoshiHonKumoi => new(3);
    public static HirajoshiScaleDegree HirajoshiIwato => new(4);
    public static HirajoshiScaleDegree HirajoshiAkebono => new(5);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Hirajoshi",
        2 => "Kumoi",
        3 => "Hon-kumoi",
        4 => "Iwato",
        5 => "Akebono",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "Hira",
        2 => "Kumoi",
        3 => "HonK",
        4 => "Iwato",
        5 => "Akeb",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
