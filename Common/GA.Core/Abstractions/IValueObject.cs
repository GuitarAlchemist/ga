namespace GA.Core.Abstractions;

/// <summary>
/// Interface for an object identified by its value.
/// </summary>
/// <remarks>
/// Derives from <see cref="IComparable"/>
/// </remarks>
public interface IValueObject : IComparable
{
    /// <summary>
    /// Gets the <see cref="int"/> value
    /// </summary>
    int Value { get; }

    int IComparable.CompareTo(object? obj)
    {
        if (obj is null) return 1;
        return obj is IValueObject other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must implement {nameof(IValueObject)}");
    }
}

/// <summary>
/// Value object interface (Strongly typed)
/// </summary>
/// <typeparam name="TSelf">This object type</typeparam>
/// Derives from <see cref="IValueObject"/>, <see cref="IComparable{TSelf}"/> and IEquatable{TSelf}
public interface IValueObject<TSelf> : IValueObject, IComparable<TSelf>, IEquatable<TSelf>
    where TSelf : IValueObject<TSelf>
{
    int IComparable<TSelf>.CompareTo(TSelf? other) => Value.CompareTo(other?.Value);

    /// <summary>
    /// Creates an <paramtyperef name="TSelf"/> object instance from its value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    static abstract TSelf FromValue(int value);

    static abstract implicit operator TSelf(int value);
    static abstract implicit operator int(TSelf fret);
}