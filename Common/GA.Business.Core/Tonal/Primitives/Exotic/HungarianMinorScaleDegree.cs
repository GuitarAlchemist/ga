namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
///     A Hungarian minor scale degree
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Hungarian_minor_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct HungarianMinorScaleDegree : IRangeValueObject<HungarianMinorScaleDegree>,
    IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 7;

    private readonly int _value;

    // Constructor
    public HungarianMinorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<HungarianMinorScaleDegree> All =>
        ValueObjectUtils<HungarianMinorScaleDegree>.Items;

    public static IReadOnlyCollection<HungarianMinorScaleDegree> Items =>
        ValueObjectUtils<HungarianMinorScaleDegree>.Items;

    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    // Static instances for convenience
    public static HungarianMinorScaleDegree HungarianMinor => new(1);
    public static HungarianMinorScaleDegree HungarianMinorMode2 => new(2);
    public static HungarianMinorScaleDegree HungarianMinorMode3 => new(3);
    public static HungarianMinorScaleDegree HungarianMinorMode4 => new(4);
    public static HungarianMinorScaleDegree HungarianMinorMode5 => new(5);
    public static HungarianMinorScaleDegree HungarianMinorMode6 => new(6);
    public static HungarianMinorScaleDegree HungarianMinorMode7 => new(7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HungarianMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new HungarianMinorScaleDegree { Value = value };
    }

    public static HungarianMinorScaleDegree Min => FromValue(_minValue);
    public static HungarianMinorScaleDegree Max => FromValue(_maxValue);

    public static implicit operator HungarianMinorScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(HungarianMinorScaleDegree degree)
    {
        return degree.Value;
    }

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    public string ToName()
    {
        return Value switch
        {
            1 => "Hungarian minor",
            2 => "Hungarian minor mode 2",
            3 => "Hungarian minor mode 3",
            4 => "Hungarian minor mode 4",
            5 => "Hungarian minor mode 5",
            6 => "Hungarian minor mode 6",
            7 => "Hungarian minor mode 7",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "HunMin",
            2 => "HunMin2",
            3 => "HunMin3",
            4 => "HunMin4",
            5 => "HunMin5",
            6 => "HunMin6",
            7 => "HunMin7",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<HungarianMinorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<HungarianMinorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(HungarianMinorScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(HungarianMinorScaleDegree left, HungarianMinorScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(HungarianMinorScaleDegree left, HungarianMinorScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(HungarianMinorScaleDegree left, HungarianMinorScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(HungarianMinorScaleDegree left, HungarianMinorScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
