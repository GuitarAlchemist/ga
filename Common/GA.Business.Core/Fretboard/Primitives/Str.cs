using System.Runtime.CompilerServices;

namespace GA.Business.Core.Fretboard.Primitives;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An instrument string (Between <see cref="Min"/> and <see cref="Max" />)
/// </summary>
/// <remarks>
/// String 1 is the string with the highest pitch.
/// </remarks>
[PublicAPI]
public readonly record struct Str : IValue<Str>
{
    #region Relational members

    public int CompareTo(Str other) => _value.CompareTo(other._value);
    public static bool operator <(Str left, Str right) => left.CompareTo(right) < 0;
    public static bool operator >(Str left, Str right) => left.CompareTo(right) > 0;
    public static bool operator <=(Str left, Str right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Str left, Str right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 26;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Str Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };
    public static Str operator ++(Str str) => Create(str._value + 1);

    /// <summary>
    /// The first string (Highest pitch)
    /// </summary>
    public static Str Min => Create(_minValue);
    /// <summary>
    /// The last string (Lowest pitch)
    /// </summary>
    public static Str Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<Str>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Str>.CheckRange(value, minValue, maxValue);
    public static IReadOnlyCollection<Str> GetCollection(int count) => ValueUtils<Str>.Collection(_minValue, count);

    public static implicit operator Str(int value) => new() { Value = value };
    public static implicit operator int(Str str) => str._value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public void CheckMaxValue(int maxValue) => ValueUtils<Str>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => Value.ToString();
}

