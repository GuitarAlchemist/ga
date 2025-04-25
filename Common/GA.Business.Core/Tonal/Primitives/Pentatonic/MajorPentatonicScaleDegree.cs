namespace GA.Business.Core.Tonal.Primitives.Pentatonic;

/// <summary>
/// A major pentatonic scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Pentatonic_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct MajorPentatonicScaleDegree : IRangeValueObject<MajorPentatonicScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(MajorPentatonicScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 5;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MajorPentatonicScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public MajorPentatonicScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static MajorPentatonicScaleDegree Min => FromValue(_minValue);
    public static MajorPentatonicScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<MajorPentatonicScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<MajorPentatonicScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator MajorPentatonicScaleDegree(int value) => FromValue(value);
    public static implicit operator int(MajorPentatonicScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<MajorPentatonicScaleDegree> All => ValueObjectUtils<MajorPentatonicScaleDegree>.Items;
    public static IReadOnlyCollection<MajorPentatonicScaleDegree> Items => ValueObjectUtils<MajorPentatonicScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static MajorPentatonicScaleDegree MajorPentatonic => new(1);
    public static MajorPentatonicScaleDegree Egyptian => new(2);
    public static MajorPentatonicScaleDegree BluesMinor => new(3);
    public static MajorPentatonicScaleDegree BluesMajor => new(4);
    public static MajorPentatonicScaleDegree MinorPentatonic => new(5);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Major pentatonic",
        2 => "Egyptian",
        3 => "Blues minor",
        4 => "Blues major",
        5 => "Minor pentatonic",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "Maj5",
        2 => "Egy",
        3 => "Bm5",
        4 => "BM5",
        5 => "Min5",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
