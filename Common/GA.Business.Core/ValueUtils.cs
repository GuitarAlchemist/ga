using GA.Core.Extensions;

namespace GA.Business.Core;

using System.Diagnostics;
using System.Runtime.CompilerServices;

[PublicAPI]
public static class ValueUtils<TValue>
    where TValue : struct, IValue<TValue>
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

    public static IReadOnlyCollection<TValue> GetAll() => ReadOnlyValues<TValue>.Create();
    public static IReadOnlyCollection<TValue> GetRange(int start, int count) => ReadOnlyValues<TValue>.Create(start, count);
}