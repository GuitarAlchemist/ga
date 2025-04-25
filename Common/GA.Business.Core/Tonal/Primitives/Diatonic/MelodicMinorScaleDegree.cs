namespace GA.Business.Core.Tonal.Primitives.Diatonic;

/// <summary>
/// A melodic minor scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Minor_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct MelodicMinorScaleDegree : IRangeValueObject<MelodicMinorScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(MelodicMinorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MelodicMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public MelodicMinorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static MelodicMinorScaleDegree Min => FromValue(_minValue);
    public static MelodicMinorScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<MelodicMinorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<MelodicMinorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator MelodicMinorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(MelodicMinorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<MelodicMinorScaleDegree> All => ValueObjectUtils<MelodicMinorScaleDegree>.Items;
    public static IReadOnlyCollection<MelodicMinorScaleDegree> Items => ValueObjectUtils<MelodicMinorScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static MelodicMinorScaleDegree MelodicMinor => new(1);
    public static MelodicMinorScaleDegree DorianFlatSecond => new(2);
    public static MelodicMinorScaleDegree LydianAugmented => new(3);
    public static MelodicMinorScaleDegree LydianDominant => new(4);
    public static MelodicMinorScaleDegree MixolydianFlatSixth => new(5);
    public static MelodicMinorScaleDegree LocrianNaturalSecond => new(6);
    public static MelodicMinorScaleDegree Altered => new(7);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Melodic minor",
        2 => "Dorian \u266D2",
        3 => "Lydian \u266F5",
        4 => "Lydian dominant",
        5 => "Mixolydian \u266D6",
        6 => "Locrian \u266E2",
        7 => "Altered",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "i",
        2 => "ii",
        3 => "III+",
        4 => "IV7",
        5 => "V",
        6 => "vi°",
        7 => "vii°",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
