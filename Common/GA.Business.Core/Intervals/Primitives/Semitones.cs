using System.Runtime.CompilerServices;

namespace GA.Business.Core.Intervals.Primitives;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// A chromatic interval expressed in semitones
/// </summary>
/// <remarks>
/// See https://en.wikipedia.org/wiki/Semitone
/// </remarks>
[PublicAPI]
public readonly record struct Semitones : IValue<Semitones>
{
    #region Relational members

    public int CompareTo(Semitones other) => _value.CompareTo(other._value);
    public static bool operator <(Semitones left, Semitones right) => left.CompareTo(right) < 0;
    public static bool operator >(Semitones left, Semitones right) => left.CompareTo(right) > 0;
    public static bool operator <=(Semitones left, Semitones right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Semitones left, Semitones right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -12 * 12; // -12 octaves, 12 semitones each
    private const int _maxValue = 12 * 12; // +12 octaves, 12 semitones each
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Semitones Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };
    public static Semitones operator ++(Semitones str) => Create(str._value + 1);

    public static Semitones Min => Create(_minValue);
    public static Semitones Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<Semitones>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Semitones>.CheckRange(value, minValue, maxValue);

    public static implicit operator Semitones(int value) => new() { Value = value };
    public static implicit operator int(Semitones str) => str._value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();
}

