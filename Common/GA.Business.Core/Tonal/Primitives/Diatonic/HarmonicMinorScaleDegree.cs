namespace GA.Business.Core.Tonal.Primitives.Diatonic;

/// <summary>
///     A harmonic minor scale degree
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Minor_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct HarmonicMinorScaleDegree : IRangeValueObject<HarmonicMinorScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 7;

    private readonly int _value;

    // Constructor
    public HarmonicMinorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<HarmonicMinorScaleDegree> All => ValueObjectUtils<HarmonicMinorScaleDegree>.Items;

    public static IReadOnlyCollection<HarmonicMinorScaleDegree> Items =>
        ValueObjectUtils<HarmonicMinorScaleDegree>.Items;

    public static IReadOnlyCollection<int> Values => [.. Items.Select(degree => degree.Value)];

    // Static instances for convenience
    public static HarmonicMinorScaleDegree HarmonicMinor => new(1);
    public static HarmonicMinorScaleDegree LocrianNaturalSixth => new(2);
    public static HarmonicMinorScaleDegree IonianAugmented => new(3);
    public static HarmonicMinorScaleDegree DorianSharpFourth => new(4);
    public static HarmonicMinorScaleDegree PhrygianDominant => new(5);
    public static HarmonicMinorScaleDegree LydianSharpSecond => new(6);
    public static HarmonicMinorScaleDegree Alteredd7 => new(7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HarmonicMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new HarmonicMinorScaleDegree { Value = value };
    }

    public static HarmonicMinorScaleDegree Min => FromValue(_minValue);
    public static HarmonicMinorScaleDegree Max => FromValue(_maxValue);

    public static implicit operator HarmonicMinorScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(HarmonicMinorScaleDegree degree)
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
            1 => "Harmonic minor",
            2 => "Locrian \u266E6",
            3 => "Ionian augmented",
            4 => "Dorian \u266F4",
            5 => "Phrygian dominant",
            6 => "Lydian \u266F2",
            7 => "Altered bb7",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "i",
            2 => "ii°",
            3 => "III+",
            4 => "iv#4",
            5 => "V",
            6 => "VI",
            7 => "vii°",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<HarmonicMinorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<HarmonicMinorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(HarmonicMinorScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
