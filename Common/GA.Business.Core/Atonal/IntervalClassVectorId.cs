namespace GA.Business.Core.Atonal;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Primitives;

/// <summary>
///     Uniquely identifies an Interval Class Vector as a base-12 integer
/// </summary>
/// <remarks>
///     e.g. &lt;2, 5, 4, 3, 6, 1&gt; Interval Class Vector (From major scale or its modes) => 254361
/// </remarks>
/// <param name="Value">The base-12 <see cref="int" /> value</param>
public readonly record struct IntervalClassVectorId(int Value) : IComparable<IntervalClassVectorId>
{
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
    public override string ToString()
    {
        return $"<{string.Join(" ", Vector.Values)}>";
    }

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
        // to match GetVector() decomposition and the documented encoding (e.g., 254361).
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

    public static implicit operator int(IntervalClassVectorId id)
    {
        return id.Value;
    }

    public static implicit operator IntervalClassVectorId(int value)
    {
        return new(value);
    }

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

    public bool Equals(IntervalClassVectorId other)
    {
        return Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value;
    }

    #endregion

    #region Relational Members Members

    public int CompareTo(IntervalClassVectorId other)
    {
        return Value.CompareTo(other.Value);
    }

    public static bool operator <(IntervalClassVectorId left, IntervalClassVectorId right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(IntervalClassVectorId left, IntervalClassVectorId right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(IntervalClassVectorId left, IntervalClassVectorId right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(IntervalClassVectorId left, IntervalClassVectorId right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
