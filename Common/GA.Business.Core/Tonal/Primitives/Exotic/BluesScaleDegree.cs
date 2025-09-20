namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
/// A blues scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Blues_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct BluesScaleDegree : IRangeValueObject<BluesScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(BluesScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(BluesScaleDegree left, BluesScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(BluesScaleDegree left, BluesScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(BluesScaleDegree left, BluesScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(BluesScaleDegree left, BluesScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BluesScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public BluesScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static BluesScaleDegree Min => FromValue(_minValue);
    public static BluesScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<BluesScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<BluesScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator BluesScaleDegree(int value) => FromValue(value);
    public static implicit operator int(BluesScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<BluesScaleDegree> All => ValueObjectUtils<BluesScaleDegree>.Items;
    public static IReadOnlyCollection<BluesScaleDegree> Items => ValueObjectUtils<BluesScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static BluesScaleDegree Blues => new(1);
    public static BluesScaleDegree MinorBlues => new(2);
    public static BluesScaleDegree BluesPhrygian => new(3);
    public static BluesScaleDegree BluesDorian => new(4);
    public static BluesScaleDegree BluesMixolydian => new(5);
    public static BluesScaleDegree BluesAeolian => new(6);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Blues",
        2 => "Minor blues",
        3 => "Blues Phrygian",
        4 => "Blues Dorian",
        5 => "Blues Mixolydian",
        6 => "Blues Aeolian",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
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
