namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
/// A Prometheus scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Mystic_chord"/>
/// </remarks>
[PublicAPI]
public readonly record struct PrometheusScaleDegree : IRangeValueObject<PrometheusScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(PrometheusScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(PrometheusScaleDegree left, PrometheusScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(PrometheusScaleDegree left, PrometheusScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(PrometheusScaleDegree left, PrometheusScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PrometheusScaleDegree left, PrometheusScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrometheusScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public PrometheusScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static PrometheusScaleDegree Min => FromValue(_minValue);
    public static PrometheusScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<PrometheusScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<PrometheusScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator PrometheusScaleDegree(int value) => FromValue(value);
    public static implicit operator int(PrometheusScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<PrometheusScaleDegree> All => ValueObjectUtils<PrometheusScaleDegree>.Items;
    public static IReadOnlyCollection<PrometheusScaleDegree> Items => ValueObjectUtils<PrometheusScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static PrometheusScaleDegree Prometheus => new(1);
    public static PrometheusScaleDegree PrometheusNeapolitan => new(2);
    public static PrometheusScaleDegree PrometheusPhrygian => new(3);
    public static PrometheusScaleDegree PrometheusLydian => new(4);
    public static PrometheusScaleDegree PrometheusMixolydian => new(5);
    public static PrometheusScaleDegree PrometheusLocrian => new(6);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Prometheus",
        2 => "Prometheus Neapolitan",
        3 => "Prometheus Phrygian",
        4 => "Prometheus Lydian",
        5 => "Prometheus Mixolydian",
        6 => "Prometheus Locrian",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "Prom",
        2 => "PromNeap",
        3 => "PromPhr",
        4 => "PromLyd",
        5 => "PromMix",
        6 => "PromLoc",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
