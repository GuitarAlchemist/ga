namespace GA.Business.Core;

using System.Diagnostics;
using GA.Core.Extensions;

[PublicAPI]
public static class ValueObjectUtils<TValue>
    where TValue : struct, IValueObject<TValue>
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
                $"{typeof(TValue)} {valueExpression} ({value}) cannot be less than {minValueExpression} ({minValue}).");
        }

        if (value > maxValue)
        {
            Debugger.Break();

            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{typeof(TValue)} {valueExpression} ({value}) cannot be greater than {maxValueExpression} ({maxValue}).");
        }

        return value;
    }

    public static IReadOnlyCollection<TValue> GetCollection() => ValueObjectCollection<TValue>.Create();
    public static IReadOnlyCollection<int> GetValues() => GetCollection().Select(value => value.Value).ToImmutableList();
    public static IReadOnlyCollection<TValue> GetRange(int start, int count) => ValueObjectCollection<TValue>.Create(start, count);
}