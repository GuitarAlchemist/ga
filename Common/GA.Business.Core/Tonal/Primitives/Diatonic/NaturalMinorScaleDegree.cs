namespace GA.Business.Core.Tonal.Primitives.Diatonic;

/// <summary>
///     A natural minor scale degree
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Minor_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct NaturalMinorScaleDegree : IRangeValueObject<NaturalMinorScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 7;

    private readonly int _value;

    // Constructor
    public NaturalMinorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<NaturalMinorScaleDegree> All => ValueObjectUtils<NaturalMinorScaleDegree>.Items;
    public static IReadOnlyCollection<NaturalMinorScaleDegree> Items => ValueObjectUtils<NaturalMinorScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => [.. Items.Select(degree => degree.Value)];

    // Static instances for convenience
    public static NaturalMinorScaleDegree Aeolian => new(1);
    public static NaturalMinorScaleDegree Locrian => new(2);
    public static NaturalMinorScaleDegree Ionian => new(3);
    public static NaturalMinorScaleDegree Dorian => new(4);
    public static NaturalMinorScaleDegree Phrygian => new(5);
    public static NaturalMinorScaleDegree Lydian => new(6);
    public static NaturalMinorScaleDegree Mixolydian => new(7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NaturalMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new NaturalMinorScaleDegree { Value = value };
    }

    public static NaturalMinorScaleDegree Min => FromValue(_minValue);
    public static NaturalMinorScaleDegree Max => FromValue(_maxValue);

    public static implicit operator NaturalMinorScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(NaturalMinorScaleDegree degree)
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
            1 => "Aeolian",
            2 => "Locrian",
            3 => "Ionian",
            4 => "Dorian",
            5 => "Phrygian",
            6 => "Lydian",
            7 => "Mixolydian",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "i",
            2 => "ii°",
            3 => "III",
            4 => "iv",
            5 => "v",
            6 => "VI",
            7 => "VII",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<NaturalMinorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<NaturalMinorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(NaturalMinorScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
