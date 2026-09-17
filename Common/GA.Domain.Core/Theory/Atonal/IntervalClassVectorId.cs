namespace GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Uniquely identifies an Interval Class Vector as a base-12 integer
/// </summary>
/// <remarks>
///     e.g. the &lt;2 5 4 3 6 1&gt; interval-class vector (major scale or its modes) packs
///     base-12 to Value = 608761 (2·12⁵ + 5·12⁴ + 4·12³ + 3·12² + 6·12 + 1). Base-12 (not
///     base-10) is used so each count digit holds 0–11 — base-10 corrupts any count ≥ 10,
///     which occurs for sets of cardinality ≥ 11.
///     A count of exactly 12 overflows a base-12 digit; only the full chromatic aggregate
///     &lt;12 12 12 12 12 6&gt; has one. Its value (3257430) is still unique, since every other
///     vector packs below 12⁶, so it is kept (ids are used as keys, labels and in ix payloads) and
///     decoded as a special case instead of as digits (which read &lt;1 1 1 1 0 6&gt;).
/// </remarks>
/// <param name="Value">The base-12 <see cref="int" /> value</param>
public readonly record struct IntervalClassVectorId(int Value) : IComparable<IntervalClassVectorId>
{
    /// <summary>
    ///     Packed value of the chromatic aggregate's &lt;12 12 12 12 12 6&gt;, whose counts of 12 overflow
    ///     their base-12 digits (12·12⁵ + 12·12⁴ + 12·12³ + 12·12² + 12·12 + 6).
    /// </summary>
    private const int _chromaticAggregateValue = 3257430;

    /// <summary>
    ///     Gets the <see cref="ImmutableSortedDictionary" /> interval class vector for the ID
    /// </summary>
    public ImmutableSortedDictionary<IntervalClass, int> Vector => GetVector(Value);

    /// <inheritdoc />
    /// <remarks>
    ///     e.g.
    ///     <code>
    /// &lt;2, 5, 4, 3, 6, 1&gt;
    /// </code>
    /// </remarks>
    public override string ToString() => $"<{string.Join(" ", Vector.Values)}>";

    /// <summary>
    ///     Converts a value to an interval vector representation
    /// </summary>
    /// <param name="value">The <see cref="int" /> value</param>
    /// <param name="valueBase">The <see cref="int" /> base to express the value</param>
    /// <returns>The <see cref="Vector" /></returns>
    private static ImmutableSortedDictionary<IntervalClass, int> GetVector(int value, int valueBase = 12)
    {
        // Decompose base 12 value
        var dictBuilder = ImmutableSortedDictionary.CreateBuilder<IntervalClass, int>();
        if (value == _chromaticAggregateValue)
        {
            for (var icValue = 1; icValue <= 6; icValue++)
            {
                dictBuilder.Add(IntervalClass.FromValue(icValue), icValue == 6 ? 6 : 12);
            }

            return dictBuilder.ToImmutable();
        }

        var dividend = value;

        // Iterate deterministically from IC6 down to IC1. This avoids LINQ Reverse() over a custom collection
        // and keeps the same least-significant-first decomposition as ToValue's weight accumulation.
        for (var icValue = 6; icValue >= 1; icValue--)
        {
            var intervalClass = IntervalClass.FromValue(icValue);
            var intervalClassCount = dividend % valueBase; // Remainder
            dictBuilder.Add(intervalClass, intervalClassCount);
            dividend /= valueBase;
        }

        return dictBuilder.ToImmutable();
    }

    /// <summary>
    ///     Converts the <see cref="Vector" /> to a value
    /// </summary>
    /// <param name="countByIntervalClass">
    ///     The <see cref="IReadOnlyDictionary{TKey,TValue}" /> where the key is a
    ///     <see cref="IntervalClass" /> and the value is a <see cref="int" /> number of occurrences of the interval class
    /// </param>
    /// <param name="valueBase">The value base <see cref="int" /></param>
    /// <returns>The base 12 value <see cref="int" /></returns>
    private static int ToValue(IReadOnlyDictionary<IntervalClass, int> countByIntervalClass, int valueBase = 12)
    {
        // Normalize: ensure all interval classes [1..6] are present with default value 0
        // Accumulate weights starting from IC6 (least significant digit) up to IC1 (most significant)
        // to match GetVector() decomposition and the documented encoding (major scale
        // <2 5 4 3 6 1> => 608761 in base 12).
        var value = 0;
        var weight = 1;

        for (var icValue = 6; icValue >= 1; icValue--)
        {
            var ic = IntervalClass.FromValue(icValue);
            var count = countByIntervalClass.GetValueOrDefault(ic, 0);
            value += count * weight;
            weight *= valueBase;
        }

        return value;
    }

    #region Static Helpers

    public static implicit operator int(IntervalClassVectorId id) => id.Value;

    public static implicit operator IntervalClassVectorId(int value) => new(value);

    /// <summary>
    ///     Creates an ID from an Interval Class Vector
    /// </summary>
    /// <param name="countByIntervalClass">The <see cref="IReadOnlyDictionary{IntervalClass, Int32}" /></param>
    /// <returns>The <see cref="IntervalClassVectorId" /></returns>
    public static IntervalClassVectorId CreateFrom(IReadOnlyDictionary<IntervalClass, int> countByIntervalClass)
    {
        var value = ToValue(countByIntervalClass);
        return new(value);
    }

    #endregion

    #region Equality Members

    public bool Equals(IntervalClassVectorId other) => Value == other.Value;

    public override int GetHashCode() => Value;

    #endregion

    #region Relational Members Members

    public int CompareTo(IntervalClassVectorId other) => Value.CompareTo(other.Value);

    public static bool operator <(IntervalClassVectorId left, IntervalClassVectorId right) => left.CompareTo(right) < 0;

    public static bool operator >(IntervalClassVectorId left, IntervalClassVectorId right) => left.CompareTo(right) > 0;

    public static bool operator <=(IntervalClassVectorId left, IntervalClassVectorId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(IntervalClassVectorId left, IntervalClassVectorId right) => left.CompareTo(right) >= 0;

    #endregion
}
