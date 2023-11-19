namespace GA.Core.Combinatorics;

/// <summary>
/// A variation (Item in a combination or arrangement of items)
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <seealso cref="VariationsWithRepetitions{T}"/>
/// <inheritdoc cref="IReadOnlyList{T>"/>
public readonly struct Variation<T>(
        BigInteger index,
        ImmutableArray<T> items,
        VariationFormat? variationFormat = null)
    : IReadOnlyList<T>
{
    #region IReadOnlyList<T> members

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _items).GetEnumerator();
    public int Count => _items.Count;
    public T this[int index] => _items[index];

    #endregion

    private readonly IReadOnlyList<T> _items = items;

    /// <summary>
    /// The current variation <see cref="BigInteger"/> index (Lexicographical order).
    /// </summary>
    public BigInteger Index { get; } = index;

    public override string ToString() => Print(this, variationFormat);

    public static string Print(
        Variation<T> variation,
        VariationFormat? variationFormat = null)
    {
        var sb = new StringBuilder();
        if (variationFormat != null) sb.Append(variation.Index.ToString(variationFormat.IndexFormat));
        else sb.Append(variation.Index);
        sb.Append(": ");
        var sItems = string.Join(" ", variation.Reverse());
        // if (variationFormat != null) sItems = sItems.PadLeft(variationFormat.Padding);
        sb.Append(sItems);
        return sb.ToString();
    }
}