namespace GA.Core.Combinatorics;

/// <summary>
/// A k-tuple variation. 
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <seealso cref="VariationsWithRepetitions{T}"/>
/// <inheritdoc cref="IReadOnlyList{T>"/>
public readonly struct Variation<T> : IReadOnlyList<T>
{
    #region IReadOnlyList<T> members

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _items).GetEnumerator();
    public int Count => _items.Count;
    public T this[int index] => _items[index];

    #endregion

    private readonly IReadOnlyList<T> _items;

    public Variation(
        BigInteger index, 
        ImmutableArray<T> items)
    {
        Index = index;
        _items = items;
    }

    /// <summary>
    /// The variation <see cref="BigInteger"/> index (Lexicographical order).
    /// </summary>
    public BigInteger Index { get; }

    public override string ToString() => $"{Index,-10}: {string.Join(" ", _items.Reverse())}";
}