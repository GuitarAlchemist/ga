namespace GA.Core.Collections;

using Extensions;

[PublicAPI]
public static class ValueObjectUtils<TSelf>
    where TSelf : IRangeValueObject<TSelf>
{
    public static IReadOnlyCollection<TSelf> Items => ValueObjectCollection<TSelf>.Create();

    public static ImmutableArray<int> Values => ValueObjectCache<TSelf>.AllValues;

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

        var count = maxValue - minValue;

        if (normalize)
        {
            value = minValue + (value - minValue).Mod(count) + 1;
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

        var count = maxValue - minValue;
        if (normalize)
        {
            value = minValue + (value - minValue).Mod(count) + 1;
        }

        if (value < minValue)
        {
            return false;
        }

        return value <= maxValue;
    }

    public static IReadOnlyCollection<TSelf> GetItems(int start, int count)
    {
        return ValueObjectCollection<TSelf>.Create(start, count);
    }

    public static IReadOnlyCollection<TSelf> GetItemsWithHead(TSelf head, int start, int count)
    {
        return ValueObjectCollection<TSelf>.CreateWithHead(head, start, count);
    }
}
