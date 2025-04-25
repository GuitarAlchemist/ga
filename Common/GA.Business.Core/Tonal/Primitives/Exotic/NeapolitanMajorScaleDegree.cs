namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
/// A Neapolitan major scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Neapolitan_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct NeapolitanMajorScaleDegree : IRangeValueObject<NeapolitanMajorScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(NeapolitanMajorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(NeapolitanMajorScaleDegree left, NeapolitanMajorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(NeapolitanMajorScaleDegree left, NeapolitanMajorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(NeapolitanMajorScaleDegree left, NeapolitanMajorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(NeapolitanMajorScaleDegree left, NeapolitanMajorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NeapolitanMajorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public NeapolitanMajorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static NeapolitanMajorScaleDegree Min => FromValue(_minValue);
    public static NeapolitanMajorScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<NeapolitanMajorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<NeapolitanMajorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator NeapolitanMajorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(NeapolitanMajorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<NeapolitanMajorScaleDegree> All => ValueObjectUtils<NeapolitanMajorScaleDegree>.Items;
    public static IReadOnlyCollection<NeapolitanMajorScaleDegree> Items => ValueObjectUtils<NeapolitanMajorScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static NeapolitanMajorScaleDegree NeapolitanMajor => new(1);
    public static NeapolitanMajorScaleDegree LeadingWholeTone => new(2);
    public static NeapolitanMajorScaleDegree LydianAugmentedDominant => new(3);
    public static NeapolitanMajorScaleDegree LydianDominantFlat6 => new(4);
    public static NeapolitanMajorScaleDegree MajorLocrian => new(5);
    public static NeapolitanMajorScaleDegree SemiLocrianFlat4 => new(6);
    public static NeapolitanMajorScaleDegree SuperLocrianDoubleFlat7 => new(7);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Neapolitan major",
        2 => "Leading whole tone",
        3 => "Lydian augmented dominant",
        4 => "Lydian dominant b6",
        5 => "Major Locrian",
        6 => "Semi-Locrian b4",
        7 => "Super Locrian bb7",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "NeapMaj",
        2 => "LeadWT",
        3 => "Lyd+Dom",
        4 => "LydDomb6",
        5 => "MajLoc",
        6 => "SemiLocb4",
        7 => "SuperLocbb7",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
