namespace GA.Business.Core.Tonal.Primitives.Pentatonic;

/// <summary>
///     An In Sen scale degree (Japanese pentatonic scale)
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/In_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct InSenScaleDegree : IRangeValueObject<InSenScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 5;

    private readonly int _value;

    // Constructor
    public InSenScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<InSenScaleDegree> All => ValueObjectUtils<InSenScaleDegree>.Items;
    public static IReadOnlyCollection<InSenScaleDegree> Items => ValueObjectUtils<InSenScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    // Static instances for convenience
    public static InSenScaleDegree InSen => new(1);
    public static InSenScaleDegree InSenMode2 => new(2);
    public static InSenScaleDegree InSenMode3 => new(3);
    public static InSenScaleDegree InSenMode4 => new(4);
    public static InSenScaleDegree InSenMode5 => new(5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InSenScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new InSenScaleDegree { Value = value };
    }

    public static InSenScaleDegree Min => FromValue(_minValue);
    public static InSenScaleDegree Max => FromValue(_maxValue);

    public static implicit operator InSenScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(InSenScaleDegree degree)
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
            1 => "In Sen",
            2 => "In Sen mode 2",
            3 => "In Sen mode 3",
            4 => "In Sen mode 4",
            5 => "In Sen mode 5",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "InSen",
            2 => "InSen2",
            3 => "InSen3",
            4 => "InSen4",
            5 => "InSen5",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<InSenScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<InSenScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(InSenScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(InSenScaleDegree left, InSenScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(InSenScaleDegree left, InSenScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(InSenScaleDegree left, InSenScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(InSenScaleDegree left, InSenScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
