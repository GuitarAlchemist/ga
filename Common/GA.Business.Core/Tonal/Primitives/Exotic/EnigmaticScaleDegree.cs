namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
///     An enigmatic scale degree
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Enigmatic_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct EnigmaticScaleDegree : IRangeValueObject<EnigmaticScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 7;

    private readonly int _value;

    // Constructor
    public EnigmaticScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<EnigmaticScaleDegree> All => ValueObjectUtils<EnigmaticScaleDegree>.Items;
    public static IReadOnlyCollection<EnigmaticScaleDegree> Items => ValueObjectUtils<EnigmaticScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => [.. Items.Select(degree => degree.Value)];

    // Static instances for convenience
    public static EnigmaticScaleDegree Enigmatic => new(1);
    public static EnigmaticScaleDegree EnigmaticDorian => new(2);
    public static EnigmaticScaleDegree EnigmaticPhrygian => new(3);
    public static EnigmaticScaleDegree EnigmaticLydian => new(4);
    public static EnigmaticScaleDegree EnigmaticMixolydian => new(5);
    public static EnigmaticScaleDegree EnigmaticAeolian => new(6);
    public static EnigmaticScaleDegree EnigmaticLocrian => new(7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EnigmaticScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new EnigmaticScaleDegree { Value = value };
    }

    public static EnigmaticScaleDegree Min => FromValue(_minValue);
    public static EnigmaticScaleDegree Max => FromValue(_maxValue);

    public static implicit operator EnigmaticScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(EnigmaticScaleDegree degree)
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
            1 => "Enigmatic",
            2 => "Enigmatic Dorian",
            3 => "Enigmatic Phrygian",
            4 => "Enigmatic Lydian",
            5 => "Enigmatic Mixolydian",
            6 => "Enigmatic Aeolian",
            7 => "Enigmatic Locrian",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "Enig",
            2 => "EnigDor",
            3 => "EnigPhr",
            4 => "EnigLyd",
            5 => "EnigMix",
            6 => "EnigAeo",
            7 => "EnigLoc",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<EnigmaticScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<EnigmaticScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(EnigmaticScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(EnigmaticScaleDegree left, EnigmaticScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(EnigmaticScaleDegree left, EnigmaticScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(EnigmaticScaleDegree left, EnigmaticScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(EnigmaticScaleDegree left, EnigmaticScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
