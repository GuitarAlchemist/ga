namespace GA.Core.Abstractions;

using Extensions;

/// <summary>
///     Interface for a value object with <see cref="TSelf" /> min/max static values
/// </summary>
/// <typeparam name="TSelf">This object type</typeparam>
/// <remarks>
///     Derives from <see cref="IValueObject{TSelf}" />
/// </remarks>
public interface IRangeValueObject<TSelf> : IValueObject<TSelf>
    where TSelf : IRangeValueObject<TSelf>
{
    /// <summary>
    ///     Gets the <typeparamref name="TSelf" /> min value
    /// </summary>
    static abstract TSelf Min { get; }

    /// <summary>
    ///     Gets the <typeparamref name="TSelf" /> max value
    /// </summary>
    static abstract TSelf Max { get; }

    /// <summary>
    ///     Ensures value in range
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

        // Attempt to normalize the value
        var count = maxValue - minValue;

        if (normalize)
        {
            value =
                minValue
                +
                (value - minValue).Mod(count) + 1;
        }

        if (value < minValue)
        {
            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{typeof(TSelf)} {valueExpression} ({value}) cannot be less than {minValueExpression} ({minValue}).");
        }

        // ReSharper disable once InvertIf
        if (value > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                valueExpression,
                $"{typeof(TSelf)} {valueExpression} ({value}) cannot be greater than {maxValueExpression} ({maxValue}).");
        }

        return value;
    }
}
