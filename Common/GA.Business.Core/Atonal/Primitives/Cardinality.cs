namespace GA.Business.Core.Atonal.Primitives;

/// <summary>
/// A non-muted instrument fret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
/// <remarks>
/// Implements <see cref="IStaticValueObjectList{Cardinality}" />, <see cref="IName" />
/// </remarks>
[PublicAPI]
public readonly record struct Cardinality : IStaticValueObjectList<Cardinality>,
                                            IName
{
    #region IStaticValueObjectList<Cardinality> Members

    public static IReadOnlyCollection<Cardinality> Items => ValueObjectUtils<Cardinality>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<Cardinality>.Values;

    #endregion

    #region IValueObject<Cardinality> Members

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(Cardinality other) => _value.CompareTo(other._value);
    public static bool operator <(Cardinality left, Cardinality right) => left.CompareTo(right) < 0;
    public static bool operator >(Cardinality left, Cardinality right) => left.CompareTo(right) > 0;
    public static bool operator <=(Cardinality left, Cardinality right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Cardinality left, Cardinality right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 12;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Cardinality FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Cardinality Min => FromValue(_minValue);
    public static Cardinality Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<Cardinality>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<Cardinality>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator Cardinality(int value) => new() { Value = value };
    public static implicit operator int(Cardinality fret) => fret.Value;

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<Cardinality>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => string.IsNullOrEmpty(Name) ? Value.ToString() : $"{Value} ({Name})";
    public string Name => _cardinalityNames[_value];

    private static readonly ImmutableDictionary<int, string> _cardinalityNames = new Dictionary<int, string>
    {
        [0] = string.Empty,
        [1] = "Monotonic",
        [2] = "Ditonic",
        [3] = "Tritonic",
        [4] = "Tetratonic",
        [5] = "Pentatonic",
        [6] = "Hexatonic",
        [7] = "Heptatonic",
        [8] = "Octatonic",
        [9] = "Enneatonic",
        [10] = "Decatonic",
        [11] = "Hendecatonic",
        [12] = "Dodecatonic",
    }.ToImmutableDictionary();
}