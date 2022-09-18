using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core;

using System.Diagnostics;
using System.Runtime.Versioning;

using GA.Core.Extensions;

/// <summary>
/// Interface for an object identified by its value.
/// </summary>
public interface IValueObject
{
    int Value { get; init; }
}

/// <summary>
/// Value object interface (Strongly typed)
/// </summary>
/// <typeparam name="TSelf"></typeparam>
[RequiresPreviewFeatures]
public interface IValueObject<TSelf> : IValueObject, IComparable<TSelf>, IComparable
    where TSelf : struct, IValueObject<TSelf>
{
    int IComparable<TSelf>.CompareTo(TSelf other) => Value.CompareTo(other.Value);
    int IComparable.CompareTo(object? obj)
    { 
        if (ReferenceEquals(null, obj)) return 1;
        return obj is TSelf other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(TSelf)}");
    }
    
    // ReSharper disable once UnusedMemberInSuper.Global - Flag method
    public static abstract TSelf FromValue(int value);
    static abstract implicit operator TSelf(int value);
    static abstract implicit operator int(TSelf fret);

    /// <summary>
    /// Gets a <see cref="TSelf"/> instance with the minimum value.<see cref="TSelf"/>.
    /// </summary>
    static abstract TSelf Min { get; }

    /// <summary>
    /// Gets a <see cref="TSelf"/> instance with the maximum value.<see cref="TSelf"/>.
    /// </summary>
    static abstract TSelf Max { get; }

    /// <summary>
    /// Gets a collection of <see cref="TSelf"/> instance for the value range.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    // ReSharper disable once InconsistentNaming
    public static IReadOnlyCollection<TSelf> GetRange(int start, int count) => ValueObjectUtils<TSelf>.GetItems(start, count);

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