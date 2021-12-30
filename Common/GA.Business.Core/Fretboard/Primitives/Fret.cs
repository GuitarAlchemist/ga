namespace GA.Business.Core.Fretboard.Primitives;

using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{Fret}" />
/// <inheritdoc cref="IComparable{Fret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An non-muted instrument fret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct Fret : IValue<Fret>, IAll<Fret>
{
    #region Relational members

    public int CompareTo(Fret other) => _value.CompareTo(other._value);
    public static bool operator <(Fret left, Fret right) => left.CompareTo(right) < 0;
    public static bool operator >(Fret left, Fret right) => left.CompareTo(right) > 0;
    public static bool operator <=(Fret left, Fret right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Fret left, Fret right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 36;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Fret Create(int value) => new() { Value = value };

    public const int DefaultCount = 22;
    public static Fret Min => Create(_minValue);
    public static Fret Max => Create(_maxValue);
    public static Fret Open => Create(0);
    public static IReadOnlyCollection<Fret> All => ValueUtils<Fret>.All();

    public static int CheckRange(int value) => ValueUtils<Fret>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Fret>.CheckRange(value, minValue, maxValue);
    public static IReadOnlyCollection<Fret> GetCollection(int start, int count) => ValueUtils<Fret>.Collection(start, count);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public void CheckMaxValue(int maxValue) => ValueUtils<Fret>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => Value.ToString();
}