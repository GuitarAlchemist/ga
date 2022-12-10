namespace GA.Core.Collections;

using Extensions;

[PublicAPI]
public static class ValueObjectUtils<TSelf>
    where TSelf : IValueObject<TSelf>, new()
{
    public static int CheckRange(
        int value, 
        int minValue,
        int maxValue,
        bool normalize = false,
        [CallerArgumentExpression("value")] string? valueExpression = null,
        [CallerArgumentExpression("minValue")] string? minValueExpression = null,
        [CallerArgumentExpression("maxValue")] string? maxValueExpression = null)
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