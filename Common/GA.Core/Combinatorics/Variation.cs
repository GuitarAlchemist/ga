namespace GA.Core.Combinatorics;

/// <summary>
///     A variation (Item in a combination or arrangement of items)
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <seealso cref="VariationsWithRepetitions{T}" />
/// <inheritdoc cref="IReadOnlyList{T>" />
public readonly struct Variation<T>(
    BigInteger index,
    IReadOnlyList<T> items,
    VariationFormat? variationFormat = null) : IReadOnlyList<T>
{
    #region IReadOnlyList<T> members

    public IEnumerator<T> GetEnumerator()
    {
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)items).GetEnumerator();
    }

    public int Count => items.Count;
    public T this[int aIndex] => items[aIndex];

    #endregion

    /// <summary>
    ///     The current variation <see cref="BigInteger" /> index (Lexicographical order)
    /// </summary>
    public BigInteger Index { get; } = index;

    /// <inheritdoc />
    public override string ToString()
    {
        return Print(this, variationFormat);
    }

    private static string Print(
        Variation<T> variation,
        VariationFormat? variationFormat = null)
    {
        var sb = new StringBuilder();
        if (variationFormat != null)
        {
            sb.Append(variation.Index.ToString(variationFormat.IndexFormat));
        }
        else
        {
            sb.Append(variation.Index);
        }

        sb.Append(": ");
        var sItems = string.Join(" ", variation.Reverse());
        // if (variationFormat != null) sItems = sItems.PadLeft(variationFormat.Padding);
        sb.Append(sItems);
        return sb.ToString();
    }
}
