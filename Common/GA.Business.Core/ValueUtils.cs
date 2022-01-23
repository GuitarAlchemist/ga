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
        [CallerArgumentExpression("value")] string? valueExpression = null,
        [CallerArgumentExpression("minValue")] string? minValueExpression = null,
        [CallerArgumentExpression("maxValue")] string? maxValueExpression = null)
    {
        if (value < minValue)
        {
            Debugger.Break();

            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{valueExpression} ({value}) cannot be less than {minValueExpression} ({minValue}).");
        }

        if (value > maxValue)
        {
            Debugger.Break();

            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{valueExpression} ({value}) cannot be greater than {maxValueExpression} ({maxValue}).");
        }

        return value;
    }

    public static IReadOnlyCollection<TValue> GetAll() => ReadOnlyValues<TValue>.Create();
    public static IReadOnlyCollection<TValue> GetRange(int start, int count) => ReadOnlyValues<TValue>.Create(start, count);
}