namespace GA.Business.Core.Atonal;

using Primitives;

/// <summary>
/// Uniquely identifies an Interval Class Vector as a base-12 integer
/// </summary>
/// <remarks>
/// e.g. &lt;2, 5, 4, 3, 6, 1&gt; Interval Class Vector (From major scale or its modes) => 254361
/// </remarks>
/// <param name="Value">The base-12 <see cref="Int32"/> value</param>
public readonly record struct IntervalClassVectorId(int Value) : IComparable<IntervalClassVectorId>
{
    #region Static Helpers

    public static implicit operator int(IntervalClassVectorId id)  => id.Value;
    public static implicit operator IntervalClassVectorId( int value)  => new (value);

    /// <summary>
    /// Creates an ID from an Interval Class Vector
    /// </summary>
    /// <param name="countByIntervalClass">The <see cref="IReadOnlyDictionary{IntervalClass, Int32}"/></param>
    /// <returns>The <see cref="IntervalClassVectorId"/></returns>
    public static IntervalClassVectorId CreateFrom(IReadOnlyDictionary<IntervalClass, int> countByIntervalClass)
    {
        var value = ToValue(countByIntervalClass);
        return new IntervalClassVectorId(value);
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

    /// <summary>
    /// Gets the <see cref="ImmutableSortedDictionary{IntervalClass, Int32}"/> interval class vector for the ID
    /// </summary>
    public ImmutableSortedDictionary<IntervalClass, int> Vector => GetVector(Value);

    /// <inheritdoc />
    /// <remarks>
    /// e.g.
    /// <code>
    /// &lt;2, 5, 4, 3, 6, 1&gt;
    /// </code>
    /// </remarks>
    public override string ToString() => $"<{string.Join(" ", Vector.Values)}>";

    /// <summary>
    /// Converts a value to an interval vector representation
    /// </summary>
    /// <param name="value">The base 12 value <see cref="int"/></param>
    /// <returns>The <see cref="Vector"/></returns>
    private static ImmutableSortedDictionary<IntervalClass, int> GetVector(int value)
    {
        // Decompose base 12 value
        var dictBuilder = ImmutableSortedDictionary.CreateBuilder<IntervalClass, int>();
        var dividend = value;
        var intervalClasses = 
            IntervalClass
                .Range(1, 6)
                .Reverse(); // Start by least significant weight

        foreach (var intervalClass in intervalClasses)
        {
            var intervalClassCount = dividend % 12; // Remainder
            dictBuilder.Add(intervalClass, intervalClassCount);
            dividend /= 12;
        }
        return dictBuilder.ToImmutable();
    }

    /// <summary>
    /// Converts the <see cref="Vector"/> to a base 12 value
    /// </summary>
    /// <param name="countByIntervalClass">The <see cref="IReadOnlyDictionary{TKey,TValue}"/> where the key is a <see cref="IntervalClass"/> and the value is a <see cref="int"/> number of occurrences of the interval class</param>
    /// <returns>The base 12 value <see cref="int"/></returns>
    private static int ToValue(IReadOnlyDictionary<IntervalClass, int> countByIntervalClass)
    {
        // Ensure all interval classes are present as keys
        foreach (var intervalClass in IntervalClass.Items)
        {
            if (!countByIntervalClass.ContainsKey(intervalClass)) throw new ArgumentException($"Missing interval class '{intervalClass}' in {nameof(countByIntervalClass)}.");
        }

        // Compute that value in base 12
        var weight = 1; // Start by least significant weight
        var value = 0;
        foreach (var ic in countByIntervalClass.Keys.OrderBy(ic => ic.Value))
        {
            var count = countByIntervalClass[ic];
            value += count * weight;
            weight *= 12;
        }

        return value;
    }
}