namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
/// A double harmonic scale degree (Byzantine scale)
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Double_harmonic_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct DoubleHarmonicScaleDegree : IRangeValueObject<DoubleHarmonicScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(DoubleHarmonicScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleHarmonicScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public DoubleHarmonicScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static DoubleHarmonicScaleDegree Min => FromValue(_minValue);
    public static DoubleHarmonicScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<DoubleHarmonicScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<DoubleHarmonicScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator DoubleHarmonicScaleDegree(int value) => FromValue(value);
    public static implicit operator int(DoubleHarmonicScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<DoubleHarmonicScaleDegree> All => ValueObjectUtils<DoubleHarmonicScaleDegree>.Items;
    public static IReadOnlyCollection<DoubleHarmonicScaleDegree> Items => ValueObjectUtils<DoubleHarmonicScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static DoubleHarmonicScaleDegree DoubleHarmonic => new(1);
    public static DoubleHarmonicScaleDegree LydianSharpSecondSharpSixth => new(2);
    public static DoubleHarmonicScaleDegree UltraPhrygian => new(3);
    public static DoubleHarmonicScaleDegree HungarianMinor => new(4);
    public static DoubleHarmonicScaleDegree Oriental => new(5);
    public static DoubleHarmonicScaleDegree IonianAugmentedSharpSecond => new(6);
    public static DoubleHarmonicScaleDegree LocrianDoubleFlat3DoubleFlat7 => new(7);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Double harmonic",
        2 => "Lydian #2 #6",
        3 => "Ultra Phrygian",
        4 => "Hungarian minor",
        5 => "Oriental",
        6 => "Ionian augmented #2",
        7 => "Locrian bb3 bb7",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "DH",
        2 => "Lyd#2#6",
        3 => "UPhr",
        4 => "HunMin",
        5 => "Orient",
        6 => "Ion+#2",
        7 => "Locbb3bb7",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
