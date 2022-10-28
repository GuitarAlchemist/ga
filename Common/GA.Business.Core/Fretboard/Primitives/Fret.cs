namespace GA.Business.Core.Fretboard.Primitives;

/// <inheritdoc cref="IEquatable{Fret}" />
/// <inheritdoc cref="IComparable{Fret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An non-muted instrument fret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct Fret : IValueObject<Fret>, 
                                     IValueObjectCollection<Fret>
{
    #region Relational members

    public int CompareTo(Fret other) => _value.CompareTo(other._value);
    public static bool operator <(Fret left, Fret right) => left.CompareTo(right) < 0;
    public static bool operator >(Fret left, Fret right) => left.CompareTo(right) > 0;
    public static bool operator <=(Fret left, Fret right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Fret left, Fret right) => left.CompareTo(right) >= 0;

    #endregion

    public static IReadOnlyCollection<Fret> Items => ValueObjectUtils<Fret>.Items;
    public static IReadOnlyCollection<int> Values => ValueObjectUtils<Fret>.Values;

    private const int _minValue = 0;
    private const int _maxValue = 36;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fret FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Fret Min => FromValue(_minValue);
    public static Fret Max => FromValue(_maxValue);
    public static Fret Open => FromValue(0);

    public static int CheckRange(int value) => IValueObject<Fret>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IValueObject<Fret>.EnsureValueInRange(value, minValue, maxValue);
    public static IReadOnlyCollection<Fret> Range(int start, int count) => ValueObjectUtils<Fret>.GetItems(start, count);

    public static implicit operator Fret(int value) => new() { Value = value };
    public static implicit operator int(Fret fret) => fret.Value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<Fret>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => Value.ToString();
}