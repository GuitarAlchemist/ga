namespace GA.Business.Core.Tonal.Primitives.Diatonic;

/// <summary>
/// A natural minor scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Minor_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct NaturalMinorScaleDegree : IRangeValueObject<NaturalMinorScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(NaturalMinorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NaturalMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public NaturalMinorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static NaturalMinorScaleDegree Min => FromValue(_minValue);
    public static NaturalMinorScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<NaturalMinorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<NaturalMinorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator NaturalMinorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(NaturalMinorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<NaturalMinorScaleDegree> All => ValueObjectUtils<NaturalMinorScaleDegree>.Items;
    public static IReadOnlyCollection<NaturalMinorScaleDegree> Items => ValueObjectUtils<NaturalMinorScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static NaturalMinorScaleDegree Aeolian => new(1);
    public static NaturalMinorScaleDegree Locrian => new(2);
    public static NaturalMinorScaleDegree Ionian => new(3);
    public static NaturalMinorScaleDegree Dorian => new(4);
    public static NaturalMinorScaleDegree Phrygian => new(5);
    public static NaturalMinorScaleDegree Lydian => new(6);
    public static NaturalMinorScaleDegree Mixolydian => new(7);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
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

    public string ToShortName() => Value switch
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
