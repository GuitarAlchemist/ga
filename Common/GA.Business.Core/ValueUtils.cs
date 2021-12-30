using System.Runtime.CompilerServices;

namespace GA.Business.Core;

public static class ValueUtils<TValue>
    where TValue : struct, IValue<TValue>
{
    public static int CheckRange(
        int value, 
        int minValue,
        int maxValue,
        [CallerArgumentExpression("value")] string? valueExpression = null,
        [CallerArgumentExpression("minValue")] string? minValueExpression = null,
        [CallerArgumentExpression("maxValue")] string? maxValueExpression = null)
    {
        if (value < minValue)
        {
            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{valueExpression} ({value}) cannot be less than {minValueExpression} ({minValue}).");
        }

        if (value > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{valueExpression} ({value}) cannot be greater than {maxValueExpression} ({maxValue}).");
        }

        return value;
    }

    public static IReadOnlyCollection<TValue> All() => ReadOnlyCollectionWrapper<TValue>.Create();
    public static IReadOnlyCollection<TValue> Collection(int start, int count) => ReadOnlyCollectionWrapper<TValue>.Create(start, count);
}