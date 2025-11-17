namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
///     A blues scale degree
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Blues_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct BluesScaleDegree : IRangeValueObject<BluesScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 6;

    private readonly int _value;

    // Constructor
    public BluesScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<BluesScaleDegree> All => ValueObjectUtils<BluesScaleDegree>.Items;
    public static IReadOnlyCollection<BluesScaleDegree> Items => ValueObjectUtils<BluesScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => [.. Items.Select(degree => degree.Value)];

    // Static instances for convenience
    public static BluesScaleDegree Blues => new(1);
    public static BluesScaleDegree MinorBlues => new(2);
    public static BluesScaleDegree BluesPhrygian => new(3);
    public static BluesScaleDegree BluesDorian => new(4);
    public static BluesScaleDegree BluesMixolydian => new(5);
    public static BluesScaleDegree BluesAeolian => new(6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BluesScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new BluesScaleDegree { Value = value };
    }

    public static BluesScaleDegree Min => FromValue(_minValue);
    public static BluesScaleDegree Max => FromValue(_maxValue);

    public static implicit operator BluesScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(BluesScaleDegree degree)
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
            1 => "Blues",
            2 => "Minor blues",
            3 => "Blues Phrygian",
            4 => "Blues Dorian",
            5 => "Blues Mixolydian",
            6 => "Blues Aeolian",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "Blues",
            2 => "mBlues",
            3 => "BPhr",
            4 => "BDor",
            5 => "BMix",
            6 => "BAeo",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<BluesScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<BluesScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(BluesScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(BluesScaleDegree left, BluesScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(BluesScaleDegree left, BluesScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(BluesScaleDegree left, BluesScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(BluesScaleDegree left, BluesScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
