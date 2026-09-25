namespace GA.Core.ValueObjects;

using System.Collections.Immutable;
using GA.Core.Extensions;

[PublicAPI]
public static class ValueObjectUtils<TSelf>
    where TSelf : IRangeValueObject<TSelf>
{
    // The collection is immutable, so one instance serves every read (it used to be 32 bytes per read)
    public static IReadOnlyCollection<TSelf> Items => ItemsHolder.All;

    public static ImmutableArray<int> Values => ValueObjectCache<TSelf>.AllValues;

    /// <summary>
    ///     <see cref="Values" /> as an interface, boxed once. Returning the <see cref="ImmutableArray{T}" />
    ///     through <see cref="IReadOnlyList{T}" /> boxed it on every read: 24 bytes each.
    /// </summary>
    public static IReadOnlyList<int> ValuesList => ValueObjectCache<TSelf>.AllValuesList;

    public static ReadOnlySpan<TSelf> ItemsSpan => ValueObjectCache<TSelf>.ItemsSpan;

    public static ReadOnlySpan<int> ValuesSpan => ValueObjectCache<TSelf>.ValuesSpan;

    public static FrozenSet<TSelf> ItemsFrozenSet => ValueObjectCache<TSelf>.ItemsSet;

    public static FrozenSet<int> ValuesFrozenSet => ValueObjectCache<TSelf>.ValuesSet;

    /// <summary>
    ///     Ensure the value is in range.
    /// </summary>
    public static int EnsureValueRange(
        int value,
        int minValue,
        int maxValue,
        bool normalize = false,
        [CallerArgumentExpression(nameof(value))]
        string? valueExpression = null,
        [CallerArgumentExpression(nameof(minValue))]
        string? minValueExpression = null,
        [CallerArgumentExpression(nameof(maxValue))]
        string? maxValueExpression = null)
    {
        if (value >= minValue && value <= maxValue)
        {
            return value;
        }

        var count = maxValue - minValue + 1;

        if (normalize)
        {
            value = minValue + (value - minValue).Mod(count);
        }

        if (value < minValue)
        {
            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{typeof(TSelf)} {valueExpression} ({value}) cannot be less than {minValueExpression} ({minValue}).");
        }

        if (value > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{typeof(TSelf)} {valueExpression} ({value}) cannot be greater than {maxValueExpression} ({maxValue}).");
        }

        return value;
    }

    /// <summary>
    ///     Checks if the value is in range.
    /// </summary>
    public static bool IsValueInRange(
        int value,
        int minValue,
        int maxValue,
        bool normalize = false)
    {
        if (value >= minValue && value <= maxValue)
        {
            return true;
        }

        // Same normalization as EnsureValueRange: wrap into [minValue, maxValue]
        var count = maxValue - minValue + 1;
        if (normalize && count > 0)
        {
            value = minValue + (value - minValue).Mod(count);
        }

        if (value < minValue)
        {
            return false;
        }

        return value <= maxValue;
    }

    public static IReadOnlyCollection<TSelf> GetItems(int start, int count) => ValueObjectCollection<TSelf>.Create(start, count);

    public static IReadOnlyCollection<TSelf> GetItemsWithHead(TSelf head, int start, int count) => ValueObjectCollection<TSelf>.CreateWithHead(head, start, count);

    // Built on the first read of Items, as before, not when another static member is first used
    private static class ItemsHolder
    {
        internal static readonly ValueObjectCollection<TSelf> All = ValueObjectCollection<TSelf>.Create();
    }
}

