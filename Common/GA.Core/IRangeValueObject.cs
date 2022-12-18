namespace GA.Core;

using Extensions;

/// <summary>
/// Interface for a value object where the min and max values are known (Strongly typed)
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface IRangeValueObject<TSelf> : IValueObject<TSelf>
    where TSelf : IRangeValueObject<TSelf>
{
    /// <summary>
    /// Gets a <see cref="TSelf"/> instance with the minimum value.<see cref="TSelf"/>.
    /// </summary>
    static abstract TSelf Min { get; }

    /// <summary>
    /// Gets a <see cref="TSelf"/> instance with the maximum value.<see cref="TSelf"/>.
    /// </summary>
    static abstract TSelf Max { get; }

    /// <summary>
    /// Ensures value in range.
    /// </summary>
    /// <param name="value">The value that represents the object.</param>
    /// <param name="minValue">The minimum valid value</param>
    /// <param name="maxValue">The maximum valid value.</param>
    /// <param name="normalize">Flag to normalize the value inside min/max range, modulo the range size</param>
    /// <param name="valueExpression"></param>
    /// <param name="minValueExpression"></param>
    /// <param name="maxValueExpression"></param>
    /// <returns>The object value (Optionally normalized).</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static int EnsureValueInRange(
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
}
