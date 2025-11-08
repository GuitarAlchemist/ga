namespace GA.Business.Core.Tonal.Primitives.Symmetric;

/// <summary>
///     A whole-tone scale degree.
/// </summary>
/// <remarks>
///     The whole-tone scale has only 6 notes, with each note separated by a whole tone.
/// </remarks>
[PublicAPI]
public readonly record struct WholeToneScaleDegree : IRangeValueObject<WholeToneScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 6;

    private readonly int _value;

    // Constructor
    public WholeToneScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<WholeToneScaleDegree> All => ValueObjectUtils<WholeToneScaleDegree>.Items;
    public static IReadOnlyCollection<WholeToneScaleDegree> Items => ValueObjectUtils<WholeToneScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    // Static instances for convenience
    public static WholeToneScaleDegree WholeTone => new(1);
    public static WholeToneScaleDegree WholeTone2 => new(2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WholeToneScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new WholeToneScaleDegree { Value = value };
    }

    public static WholeToneScaleDegree Min => FromValue(_minValue);
    public static WholeToneScaleDegree Max => FromValue(_maxValue);

    public static implicit operator WholeToneScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(WholeToneScaleDegree degree)
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
            1 => "Whole-tone",
            2 => "Whole-tone 2",
            3 => "Whole-tone 3",
            4 => "Whole-tone 4",
            5 => "Whole-tone 5",
            6 => "Whole-tone 6",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
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

    public static int CheckRange(int value)
    {
        return IRangeValueObject<WholeToneScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<WholeToneScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(WholeToneScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(WholeToneScaleDegree left, WholeToneScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(WholeToneScaleDegree left, WholeToneScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(WholeToneScaleDegree left, WholeToneScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(WholeToneScaleDegree left, WholeToneScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
