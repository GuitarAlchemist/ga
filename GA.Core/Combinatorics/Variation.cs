namespace GA.Core.Combinatorics;

/// <summary>
/// A k-tuple variation. 
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <seealso cref="VariationsWithRepetitions{T}"/>
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

    public (T, T) Tuple() => (this[0], this[1]);
    public (T, T, T) Tuple3() => (this[0], this[1], this[2]);
    public (T, T, T, T) Tuple4() => (this[0], this[1], this[2], this[3]);
    public (T, T, T, T, T) Tuple5() => (this[0], this[1], this[2], this[3], this[4]);
    public (T, T, T, T, T, T) Tuple6() => (this[0], this[1], this[2], this[3], this[4], this[5]);
    public (T, T, T, T, T, T, T) Tuple7() => (this[0], this[1], this[2], this[3], this[4], this[5], this[6]);
    public (T, T, T, T, T, T, T, T) Tuple8() => (this[0], this[1], this[2], this[3], this[4], this[5], this[6], this[7]);
    public (T, T, T, T, T, T, T, T, T) Tuple9() => (this[0], this[1], this[2], this[3], this[4], this[5], this[6], this[7], this[8]);
    public (T ,T, T, T, T, T, T, T, T, T) Tuple10() => (this[0], this[1], this[2], this[3], this[4], this[5], this[6], this[7], this[8], this[9]);
    public (T ,T, T, T, T, T, T, T, T, T, T) Tuple11() => (this[0], this[1], this[2], this[3], this[4], this[5], this[6], this[7], this[8], this[9], this[10]);
    public (T ,T, T, T, T, T, T, T, T, T, T, T) Tuple12() => (this[0], this[1], this[2], this[3], this[4], this[5], this[6], this[7], this[8], this[9], this[10], this[11]);
    public (T ,T, T, T, T, T, T, T, T, T, T, T, T) Tuple13() => (this[0], this[1], this[2], this[3], this[4], this[5], this[6], this[7], this[8], this[9], this[10], this[11], this[11]);

    public override string ToString() => $"{Index,-10}: {string.Join(" ", _items)}";
}