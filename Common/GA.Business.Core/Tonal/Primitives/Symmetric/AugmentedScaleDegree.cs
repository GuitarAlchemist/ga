namespace GA.Business.Core.Tonal.Primitives.Symmetric;

/// <summary>
/// An augmented scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Hexatonic_scale#Augmented_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct AugmentedScaleDegree : IRangeValueObject<AugmentedScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(AugmentedScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(AugmentedScaleDegree left, AugmentedScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(AugmentedScaleDegree left, AugmentedScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(AugmentedScaleDegree left, AugmentedScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(AugmentedScaleDegree left, AugmentedScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AugmentedScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public AugmentedScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static AugmentedScaleDegree Min => FromValue(_minValue);
    public static AugmentedScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<AugmentedScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<AugmentedScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator AugmentedScaleDegree(int value) => FromValue(value);
    public static implicit operator int(AugmentedScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<AugmentedScaleDegree> All => ValueObjectUtils<AugmentedScaleDegree>.Items;
    public static IReadOnlyCollection<AugmentedScaleDegree> Items => ValueObjectUtils<AugmentedScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static AugmentedScaleDegree Augmented => new(1);
    public static AugmentedScaleDegree AugmentedInversed => new(2);
    public static AugmentedScaleDegree AugmentedDominant => new(3);
    public static AugmentedScaleDegree AugmentedLydian => new(4);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Augmented",
        2 => "Augmented inversed",
        3 => "Augmented dominant",
        4 => "Augmented Lydian",
        5 => "Augmented mode 5",
        6 => "Augmented mode 6",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "Aug1",
        2 => "Aug2",
        3 => "Aug3",
        4 => "Aug4",
        5 => "Aug5",
        6 => "Aug6",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
