namespace GA.Core.Collections;

/// <summary>
///     A read-only list of value types
/// </summary>
/// <typeparam name="T">The value type</typeparam>
public readonly struct ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
    where T : struct
{
    private readonly ImmutableList<T> _values;

    /// <summary>
    ///     Initializes a new instance of the ValueList class
    /// </summary>
    /// <param name="values">The values to include in the list</param>
    public ValueList(IEnumerable<T> values)
    {
        _values = [.. values];
    }

    /// <summary>
    ///     Gets the number of elements in the list
    /// </summary>
    public int Count => _values?.Count ?? 0;

    /// <summary>
    ///     Gets the element at the specified index
    /// </summary>
    /// <param name="index">The zero-based index of the element to get</param>
    /// <returns>The element at the specified index</returns>
    public T this[int index] => _values[index];

    /// <summary>
    ///     Returns an enumerator that iterates through the list
    /// </summary>
    /// <returns>An enumerator for the list</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return (_values ?? ImmutableList<T>.Empty).GetEnumerator();
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the list
    /// </summary>
    /// <returns>An enumerator for the list</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object
    /// </summary>
    /// <param name="other">The object to compare with the current object</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false</returns>
    public bool Equals(ValueList<T> other)
    {
        if (_values == null && other._values == null)
        {
            return true;
        }

        if (_values == null || other._values == null)
        {
            return false;
        }

        return _values.SequenceEqual(other._values);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object
    /// </summary>
    /// <param name="obj">The object to compare with the current object</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false</returns>
    public override bool Equals(object? obj)
    {
        return obj is ValueList<T> other && Equals(other);
    }

    /// <summary>
    ///     Serves as the default hash function
    /// </summary>
    /// <returns>A hash code for the current object</returns>
    public override int GetHashCode()
    {
        if (_values == null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var value in _values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    ///     Determines whether two ValueList instances are equal
    /// </summary>
    /// <param name="left">The first ValueList to compare</param>
    /// <param name="right">The second ValueList to compare</param>
    /// <returns>true if the ValueLists are equal; otherwise, false</returns>
    public static bool operator ==(ValueList<T> left, ValueList<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two ValueList instances are not equal
    /// </summary>
    /// <param name="left">The first ValueList to compare</param>
    /// <param name="right">The second ValueList to compare</param>
    /// <returns>true if the ValueLists are not equal; otherwise, false</returns>
    public static bool operator !=(ValueList<T> left, ValueList<T> right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    ///     Returns a string representation of the ValueList
    /// </summary>
    /// <returns>A string representation of the ValueList</returns>
    public override string ToString()
    {
        if (_values == null || _values.Count == 0)
        {
            return "[]";
        }

        return $"[{string.Join(", ", _values)}]";
    }
}
