namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
///     A Tritone scale degree (Petrushka scale)
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Tritone_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct TritoneScaleDegree : IRangeValueObject<TritoneScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 6;

    private readonly int _value;

    // Constructor
    public TritoneScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<TritoneScaleDegree> All => ValueObjectUtils<TritoneScaleDegree>.Items;
    public static IReadOnlyCollection<TritoneScaleDegree> Items => ValueObjectUtils<TritoneScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    // Static instances for convenience
    public static TritoneScaleDegree Tritone => new(1);
    public static TritoneScaleDegree Petrushka => new(2);
    public static TritoneScaleDegree TritoneMode3 => new(3);
    public static TritoneScaleDegree TritoneMode4 => new(4);
    public static TritoneScaleDegree TritoneMode5 => new(5);
    public static TritoneScaleDegree TritoneMode6 => new(6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TritoneScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new TritoneScaleDegree { Value = value };
    }

    public static TritoneScaleDegree Min => FromValue(_minValue);
    public static TritoneScaleDegree Max => FromValue(_maxValue);

    public static implicit operator TritoneScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(TritoneScaleDegree degree)
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
            1 => "Tritone",
            2 => "Petrushka",
            3 => "Tritone mode 3",
            4 => "Tritone mode 4",
            5 => "Tritone mode 5",
            6 => "Tritone mode 6",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
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

    public static int CheckRange(int value)
    {
        return IRangeValueObject<TritoneScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<TritoneScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(TritoneScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(TritoneScaleDegree left, TritoneScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(TritoneScaleDegree left, TritoneScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(TritoneScaleDegree left, TritoneScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(TritoneScaleDegree left, TritoneScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
