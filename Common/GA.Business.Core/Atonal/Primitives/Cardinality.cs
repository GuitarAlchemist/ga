namespace GA.Business.Core.Atonal.Primitives;

/// <summary>
/// Cardinality is the count of unique pitch classes in a pitch class set
/// </summary>
/// <remarks>
/// Implements <see cref="IStaticValueObjectList{Cardinality}" />, <see cref="IName" />
/// </remarks>
[PublicAPI]
public readonly record struct Cardinality : IStaticReadonlyCollectionFromValues<Cardinality>,
                                            IName
{
    #region IStaticValueObjectList<Cardinality> Members

    public static Cardinality Min => FromValue(_minValue);
    public static Cardinality Max => FromValue(_maxValue);
    
    public static IReadOnlyCollection<Cardinality> Items => IStaticReadonlyCollectionFromValues<Cardinality>.Items;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Cardinality FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static implicit operator Cardinality(int value) => new() { Value = value };
    public static implicit operator int(Cardinality fret) => fret.Value;
    
    public int Value { get => _value; init => _value = CheckRange(value); }
    
    private readonly int _value;
    
    private const int _minValue = 0;
    private const int _maxValue = 12;
    private static int CheckRange(int value) => IRangeValueObject<Cardinality>.EnsureValueInRange(value, _minValue, _maxValue);
    
    #endregion

    #region Relational members

    public int CompareTo(Cardinality other) =>_value.CompareTo(other._value);
    
    public static bool operator <(Cardinality left, Cardinality right) => left.CompareTo(right) < 0;
    public static bool operator >(Cardinality left, Cardinality right) => left.CompareTo(right) > 0;
    public static bool operator <=(Cardinality left, Cardinality right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Cardinality left, Cardinality right) => left.CompareTo(right) >= 0;

    #endregion
    
    /// <inheritdoc cref="IName.Name"/>
    public string Name => _cardinalityNames[_value];

    /// <inheritdoc />
    public override string ToString() => string.IsNullOrEmpty(Name) ? Value.ToString() : $"{Value} ({Name})";

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