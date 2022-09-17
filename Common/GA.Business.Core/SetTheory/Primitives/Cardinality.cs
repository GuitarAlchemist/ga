namespace GA.Business.Core.SetTheory.Primitives;

/// <inheritdoc cref="IEquatable{Fret}" />
/// <inheritdoc cref="IComparable{Fret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An non-muted instrument fret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct Cardinality : IValueObject<Cardinality>
{
    #region Relational members

    public int CompareTo(Cardinality other) => _value.CompareTo(other._value);
    public static bool operator <(Cardinality left, Cardinality right) => left.CompareTo(right) < 0;
    public static bool operator >(Cardinality left, Cardinality right) => left.CompareTo(right) > 0;
    public static bool operator <=(Cardinality left, Cardinality right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Cardinality left, Cardinality right) => left.CompareTo(right) >= 0;

    #endregion

    public static IReadOnlyCollection<Cardinality> Items => Fretboard.Primitives.ValueObjectCollection<Cardinality>.Items;
    public static IReadOnlyCollection<int> Values => Fretboard.Primitives.ValueObjectCollection<Cardinality>.Values;

    private const int _minValue = 0;
    private const int _maxValue = 12;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Cardinality FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Cardinality Min => FromValue(_minValue);
    public static Cardinality Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IValueObject<Cardinality>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IValueObject<Cardinality>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator Cardinality(int value) => new() { Value = value };
    public static implicit operator int(Cardinality fret) => fret.Value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<Cardinality>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => Value.ToString();
}