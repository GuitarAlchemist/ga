namespace GA.Business.Core.Tonal.Primitives;

/// <summary>
///     A generic scale degree that can be used for any scale.
/// </summary>
/// <remarks>
///     This class is used when a specific scale degree class is not available.
/// </remarks>
[PublicAPI]
public readonly record struct GenericScaleDegree : IValueObject<GenericScaleDegree>
{
    /// <summary>
    ///     Creates a new instance of the <see cref="GenericScaleDegree" /> class from a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A new instance of the <see cref="GenericScaleDegree" /> class.</returns>
    public static GenericScaleDegree FromValue(int value)
    {
        return new GenericScaleDegree { Value = value };
    }

    /// <summary>
    ///     Implicitly converts an integer to a <see cref="GenericScaleDegree" />.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A new instance of the <see cref="GenericScaleDegree" /> class.</returns>
    public static implicit operator GenericScaleDegree(int value)
    {
        return FromValue(value);
    }

    /// <summary>
    ///     Implicitly converts a <see cref="GenericScaleDegree" /> to an integer.
    /// </summary>
    /// <param name="degree">The degree.</param>
    /// <returns>The value of the degree.</returns>
    public static implicit operator int(GenericScaleDegree degree)
    {
        return degree.Value;
    }

    /// <summary>
    ///     Gets or initializes the value of the degree.
    /// </summary>
    public int Value { get; init; }

    #region Relational members

    public int CompareTo(GenericScaleDegree other)
    {
        return Value.CompareTo(other.Value);
    }

    public static bool operator <(GenericScaleDegree left, GenericScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(GenericScaleDegree left, GenericScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(GenericScaleDegree left, GenericScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(GenericScaleDegree left, GenericScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
