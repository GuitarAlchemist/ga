namespace GA.Business.Core.Tonal.Primitives.Diatonic;

/// <summary>
///     A harmonic major scale degree
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Harmonic_major_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct HarmonicMajorScaleDegree : IRangeValueObject<HarmonicMajorScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 7;

    private readonly int _value;

    // Constructor
    public HarmonicMajorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<HarmonicMajorScaleDegree> All => ValueObjectUtils<HarmonicMajorScaleDegree>.Items;

    public static IReadOnlyCollection<HarmonicMajorScaleDegree> Items =>
        ValueObjectUtils<HarmonicMajorScaleDegree>.Items;

    public static IReadOnlyCollection<int> Values => [.. Items.Select(degree => degree.Value)];

    // Static instances for convenience
    public static HarmonicMajorScaleDegree HarmonicMajor => new(1);
    public static HarmonicMajorScaleDegree DorianFlatFifth => new(2);
    public static HarmonicMajorScaleDegree PhrygianFlatFourth => new(3);
    public static HarmonicMajorScaleDegree LydianFlatThird => new(4);
    public static HarmonicMajorScaleDegree MixolydianFlatSecond => new(5);
    public static HarmonicMajorScaleDegree LydianAugmentedSharpSecond => new(6);
    public static HarmonicMajorScaleDegree LocrianDoubleFlat7 => new(7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HarmonicMajorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new HarmonicMajorScaleDegree { Value = value };
    }

    public static HarmonicMajorScaleDegree Min => FromValue(_minValue);
    public static HarmonicMajorScaleDegree Max => FromValue(_maxValue);

    public static implicit operator HarmonicMajorScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(HarmonicMajorScaleDegree degree)
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
            1 => "Harmonic major",
            2 => "Dorian b5",
            3 => "Phrygian b4",
            4 => "Lydian b3",
            5 => "Mixolydian b2",
            6 => "Lydian augmented #2",
            7 => "Locrian bb7",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<HarmonicMajorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<HarmonicMajorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(HarmonicMajorScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(HarmonicMajorScaleDegree left, HarmonicMajorScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(HarmonicMajorScaleDegree left, HarmonicMajorScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(HarmonicMajorScaleDegree left, HarmonicMajorScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(HarmonicMajorScaleDegree left, HarmonicMajorScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
