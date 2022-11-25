namespace GA.Core.Combinatorics;

public readonly struct Variation<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _items;

    public Variation(
        BigInteger index, 
        ImmutableArray<T> items)
    {
        Index = index;
        _items = items;
    }

    /// <summary>
    /// The variation <see cref="BigInteger"/> key (Lexicographical order).
    /// </summary>
    public BigInteger Index { get; }
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _items).GetEnumerator();
    public int Count => _items.Count;
    public T this[int index] => _items[index];

    public override string ToString() => $"{Index}: {string.Join(" ", _items)}";
}