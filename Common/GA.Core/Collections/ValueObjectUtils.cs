namespace GA.Core.Collections;

using Extensions;

[PublicAPI]
public static class ValueObjectUtils<TSelf>
    where TSelf : IRangeValueObject<TSelf>
{
    /// <summary>
    /// Checks whether the value is in range
    /// </summary>
    /// <param name="value">The <see cref="int"/> value</param>
    /// <param name="minValue">The min <see cref="int"/> value</param>
    /// <param name="maxValue">The max <see cref="int"/> value</param>
    /// <param name="normalize">A <see cref="bool"/> flag indicating whether the value should be normalized</param>
    /// <param name="valueExpression">a <see cref="Nullable{String}"/></param>
    /// <param name="minValueExpression">a <see cref="Nullable{String}"/></param>
    /// <param name="maxValueExpression">a <see cref="Nullable{String}"/></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of range</exception>
    public static int CheckRange(
        int value, 
        int minValue,
        int maxValue,
        bool normalize = false,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null,
        [CallerArgumentExpression(nameof(minValue))] string? minValueExpression = null,
        [CallerArgumentExpression(nameof(maxValue))] string? maxValueExpression = null)
    {
        if (value >= minValue && value <= maxValue) return value;

        // Attempt to normalize the value
        var count = maxValue - minValue;

        if (normalize) value = minValue + (value - minValue).Mod(count) + 1;

        if (value < minValue)
        {
            Debugger.Break();

            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{typeof(TSelf)} {valueExpression} ({value}) cannot be less than {minValueExpression} ({minValue}).");
        }

        // ReSharper disable once InvertIf
        if (value > maxValue)
        {
            Debugger.Break();

            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{typeof(TSelf)} {valueExpression} ({value}) cannot be greater than {maxValueExpression} ({maxValue}).");
        }

        return value;
    }

    public static IReadOnlyCollection<TSelf> Items => ValueObjectCollection<TSelf>.Create();
    // ReSharper disable once InconsistentNaming
    public static IReadOnlyCollection<TSelf> GetItems(int start, int count) => ValueObjectCollection<TSelf>.Create(start, count);
    public static IReadOnlyCollection<TSelf> GetItemsWithHead(TSelf head, int start, int count) => ValueObjectCollection<TSelf>.CreateWithHead(head, start, count);
    public static ImmutableList<int> Values => Items.Select(value => value.Value).ToImmutableList();
}