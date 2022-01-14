using System.Runtime.CompilerServices;

namespace GA.Business.Core.Intervals.Primitives;

/// <inheritdoc cref="IEquatable{String}" />
/// <inheritdoc cref="IComparable{String}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An interval quality
/// </summary>
/// <remarks>
/// See https://en.wikipedia.org/wiki/Semitone
/// </remarks>
[PublicAPI]
public readonly record struct Quality : IValue<Quality>
{
    #region Relational members

    public int CompareTo(Quality other) => _value.CompareTo(other._value);
    public static bool operator <(Quality left, Quality right) => left.CompareTo(right) < 0;
    public static bool operator >(Quality left, Quality right) => left.CompareTo(right) > 0;
    public static bool operator <=(Quality left, Quality right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Quality left, Quality right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -2;
    private const int _maxValue = 2;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Quality Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Quality Min => Create(_minValue);
    public static Quality Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<Quality>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<Quality>.CheckRange(value, minValue, maxValue);

    public static implicit operator Quality(int value) => new() { Value = value };
    public static implicit operator int(Quality quality) => quality._value;
    public static Quality operator !(Quality quality) => quality.ToInverse();

    public static Quality Diminished => Create(-2);
    public static Quality Minor => Create(-1);
    public static Quality Perfect => Create(0);
    public static Quality Major => Create(1);
    public static Quality Augmented => Create(2);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    /// <summary>
    /// Create a new Quality instance with the inverse quality value..
    /// </summary>
    /// <returns>
    /// The inverse <see cref="Quality"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quality ToInverse() => Create(-Value);

    public override string ToString() => Value.ToString();
}

