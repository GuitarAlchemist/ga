namespace GA.Business.Core.Tonal.Primitives.Symmetric;

/// <summary>
/// A whole-tone scale degree.
/// </summary>
/// <remarks>
/// The whole-tone scale has only 6 notes, with each note separated by a whole tone.
/// </remarks>
[PublicAPI]
public readonly record struct WholeToneScaleDegree : IRangeValueObject<WholeToneScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(WholeToneScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(WholeToneScaleDegree left, WholeToneScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(WholeToneScaleDegree left, WholeToneScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(WholeToneScaleDegree left, WholeToneScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(WholeToneScaleDegree left, WholeToneScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WholeToneScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public WholeToneScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static WholeToneScaleDegree Min => FromValue(_minValue);
    public static WholeToneScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<WholeToneScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<WholeToneScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator WholeToneScaleDegree(int value) => FromValue(value);
    public static implicit operator int(WholeToneScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<WholeToneScaleDegree> All => ValueObjectUtils<WholeToneScaleDegree>.Items;
    public static IReadOnlyCollection<WholeToneScaleDegree> Items => ValueObjectUtils<WholeToneScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static WholeToneScaleDegree WholeTone => new(1);
    public static WholeToneScaleDegree WholeTone2 => new(2);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Whole-tone",
        2 => "Whole-tone 2",
        3 => "Whole-tone 3",
        4 => "Whole-tone 4",
        5 => "Whole-tone 5",
        6 => "Whole-tone 6",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "WT1",
        2 => "WT2",
        3 => "WT3",
        4 => "WT4",
        5 => "WT5",
        6 => "WT6",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
