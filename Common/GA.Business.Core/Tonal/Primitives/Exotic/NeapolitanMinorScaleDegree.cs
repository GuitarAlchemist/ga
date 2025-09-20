namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
/// A Neapolitan minor scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Neapolitan_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct NeapolitanMinorScaleDegree : IRangeValueObject<NeapolitanMinorScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(NeapolitanMinorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(NeapolitanMinorScaleDegree left, NeapolitanMinorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(NeapolitanMinorScaleDegree left, NeapolitanMinorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(NeapolitanMinorScaleDegree left, NeapolitanMinorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(NeapolitanMinorScaleDegree left, NeapolitanMinorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NeapolitanMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public NeapolitanMinorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static NeapolitanMinorScaleDegree Min => FromValue(_minValue);
    public static NeapolitanMinorScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<NeapolitanMinorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<NeapolitanMinorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator NeapolitanMinorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(NeapolitanMinorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<NeapolitanMinorScaleDegree> All => ValueObjectUtils<NeapolitanMinorScaleDegree>.Items;
    public static IReadOnlyCollection<NeapolitanMinorScaleDegree> Items => ValueObjectUtils<NeapolitanMinorScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static NeapolitanMinorScaleDegree NeapolitanMinor => new(1);
    public static NeapolitanMinorScaleDegree LydianSharp2 => new(2);
    public static NeapolitanMinorScaleDegree MixolydianAugmented => new(3);
    public static NeapolitanMinorScaleDegree HungarianGypsy => new(4);
    public static NeapolitanMinorScaleDegree LocrianDominant => new(5);
    public static NeapolitanMinorScaleDegree IonianSharp2Sharp5 => new(6);
    public static NeapolitanMinorScaleDegree UltraLocrianbb3 => new(7);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Neapolitan minor",
        2 => "Lydian #2",
        3 => "Mixolydian augmented",
        4 => "Hungarian Gypsy",
        5 => "Locrian dominant",
        6 => "Ionian #2 #5",
        7 => "Ultra Locrian bb3",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "NeapMin",
        2 => "Lyd#2",
        3 => "Mix+",
        4 => "HunGypsy",
        5 => "LocDom",
        6 => "Ion#2#5",
        7 => "UltraLocbb3",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
