namespace GA.Business.Core.Tonal.Primitives.Diatonic;

/// <summary>
/// A major scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Major_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct MajorScaleDegree : IRangeValueObject<MajorScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(MajorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MajorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public MajorScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static MajorScaleDegree Min => FromValue(_minValue);
    public static MajorScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<MajorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<MajorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator MajorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(MajorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<MajorScaleDegree> All => ValueObjectUtils<MajorScaleDegree>.Items;
    public static IReadOnlyCollection<MajorScaleDegree> Items => ValueObjectUtils<MajorScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static MajorScaleDegree Ionian => new(1);
    public static MajorScaleDegree Dorian => new(2);
    public static MajorScaleDegree Phrygian => new(3);
    public static MajorScaleDegree Lydian => new(4);
    public static MajorScaleDegree Mixolydian => new(5);
    public static MajorScaleDegree Aeolian => new(6);
    public static MajorScaleDegree Locrian => new(7);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Ionian",
        2 => "Dorian",
        3 => "Phrygian",
        4 => "Lydian",
        5 => "Mixolydian",
        6 => "Aeolian",
        7 => "Locrian",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
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
