namespace GA.Core.Combinatorics;

/// <summary>
///     Ordered arrangements of elements where repetition is allowed - Also called "k-tuple"
/// </summary>
/// <typeparam name="T"></typeparam>
/// <remarks>
///     Inspired from https://weibeld.net/math/combinatorics.html#variations-with-repetitions-k-tuples
///     Arrangement of k elements from a set of n elements into a sequence of length k, where each element of the set may
///     be chosen multiple times.
///     Known as k-tuples in the English literature.
///     k &lt; n or k = n or k &gt; n
///     In many cases, the elements of the set can be thought of as classes from which multiple objects can be
///     instantiated.
///     Can also be seen as forming words of length k over an alphabet of size n
///     Example:
///     Set: {a,b,c}
///     n=3
///     k=2
///     9 words: (a,a),(a,b),(a,c),(b,a),(b,b),(b,c),(c,a),(c,b),(c,c)
/// </remarks>
[PublicAPI]
public class VariationsWithRepetitions<T> : IEnumerable<Variation<T>>,
    IIndexer<BigInteger, Variation<T>>
    where T : notnull
{
    private readonly ImmutableDictionary<int, T> _elementByIndex;

    private readonly ImmutableDictionary<T, int> _indexByElement;
    private readonly Lazy<ImmutableHashSet<T>> _lazyElementsSet;

    /// <summary>
    ///     Creates a <see cref="VariationsWithRepetitions{T}" /> instance.
    /// </summary>
    /// <remarks>
    ///     - <see cref="elements" /> : the "alphabet"
    ///     - <see cref="length" />   : the length of generated "words"
    /// </remarks>
    /// <param name="elements">The initial <see cref="IEnumerable{T}" />.</param>
    /// <param name="length">The number of items in each variation.</param>
    /// <param name="predicate">Initial elements predicate (Optional).</param>
    /// <exception cref="ArgumentNullException"></exception>
    public VariationsWithRepetitions(
        IEnumerable<T> elements,
        int length,
        Func<T, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(elements);

        if (predicate != null)
        {
            elements = [.. elements.Where(predicate)];
        }

        Elements = [.. elements];
        Base = new(Elements.Count);
        Count = BigInteger.Pow(Base, length);
        // Handle Count == 0 (e.g., empty alphabet with positive length) where Log10 is undefined
        IndexFormat = Count > 0 ? $"D{(int)Math.Floor(BigInteger.Log10(Count) + 1)}" : "D1";
        Length = length;

        _indexByElement =
            Elements.Select((o, i) => (o, i))
                .ToImmutableDictionary(
                    tuple => tuple.o,
                    tuple => tuple.i);
        _elementByIndex =
            Elements.Select((o, i) => (o, i))
                .ToImmutableDictionary(
                    tuple => tuple.i,
                    tuple => tuple.o);

        _lazyElementsSet = new(() => [.. Elements]);
    }

    /// <summary>
    ///     The number of possible variations.
    /// </summary>
    public BigInteger Count { get; }

    public string IndexFormat { get; }

    /// <summary>
    ///     The possible <see cref="IReadOnlyCollection{T}" /> elements to choose from (The "alphabet").
    /// </summary>
    public IReadOnlyCollection<T> Elements { get; }

    /// <summary>
    ///     Gets the number of elements in a variation.
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///     Gets the <see cref="BigInteger" /> number base for computing variation indices.
    /// </summary>
    public BigInteger Base { get; }

    #region IIndexer<BigInteger, Variation<T>>

    /// <summary>
    ///     Gets a variation given its index.
    /// </summary>
    /// <param name="index">The <see cref="BigInteger" /> Lexicographical-order index</param>
    /// <returns>The <see cref="ImmutableArray{T}" /></returns>
    public Variation<T> this[BigInteger index] => CreateVariation(index);

    #endregion

    /// <summary>
    ///     Gets the index of a variation.
    /// </summary>
    /// <param name="variation">The <see cref="ImmutableArray{T}" /></param>
    /// <returns>The <see cref="BigInteger" /> index.</returns>
    public BigInteger GetIndex(IEnumerable<T> variation)
    {
        var variationArray = variation.ToImmutableArray();
        if (variationArray.Length != Length)
        {
            throw new ArgumentException(
                $"Invalid {nameof(variation)} length: expected {Length}, got {variationArray.Length}");
        }

        if (Length == 0)
        {
            return BigInteger.Zero;
        }

        if (Base == 0)
        {
            throw new InvalidOperationException(
                "Cannot compute index when the alphabet is empty and length > 0 (no variations exist).");
        }

        if (!_lazyElementsSet.Value.IsSupersetOf(variationArray))
        {
            var invalidItems = variationArray.Except(_lazyElementsSet.Value).ToImmutableArray();
            var sInvalidItems = string.Join(", ", invalidItems.Take(5));
            if (invalidItems.Length >= 5)
            {
                sInvalidItems += "...";
            }

            throw new ArgumentException(
                $"Invalid {nameof(variation)} - {invalidItems.Length} items are not in initial collection: {sInvalidItems}");
        }

        // Compose the variation index with most-significant digit at position 0
        // index = idx(item0) * Base^(Length-1) + idx(item1) * Base^(Length-2) + ... + idx(item{L-1}) * Base^0
        var result = BigInteger.Zero;
        var weight = BigInteger.One;
        for (int i = 1; i < Length; i++) weight *= Base; // weight = Base^(Length-1)

        for (var pos = 0; pos < Length; pos++)
        {
            var value = _indexByElement[variationArray[pos]];
            result += new BigInteger(value) * weight;
            if (pos < Length - 1) weight /= Base;
        }

        return result;
    }

    public override string ToString()
    {
        var len = Elements.Max(element => element.ToString()!.Length);

        var sb = new StringBuilder();
        sb.Append(string.Join(" ", Elements.Select(element => element.ToString()!.PadLeft(len))));
        sb.Append($" => {Count} variations");
        return sb.ToString();
    }

    /// <summary>
    ///     Create a variation from an index.
    /// </summary>
    /// <param name="index">The variation index.</param>
    /// <remarks>
    ///     Variations are produced in lexicographical order, with position 0 as the most significant digit
    ///     of the base-<see cref="Base"/> number system. That is, for alphabet [a,b,c] and length 2, indices map to:
    ///     0 → [a,a], 1 → [a,b], 2 → [a,c], 3 → [b,a], etc.
    /// </remarks>
    private Variation<T> CreateVariation(BigInteger index)
    {
        if (Length == 0)
        {
            return new(index, ImmutableArray<T>.Empty);
        }

        if (Base == 0)
        {
            throw new InvalidOperationException(
                "Cannot create a variation when the alphabet is empty and length > 0 (no variations exist).");
        }

        var arrayBuilder = ImmutableArray.CreateBuilder<T>(Length);
        // Pre-fill to correct size
        for (var i = 0; i < Length; i++) arrayBuilder.Add(default!);

        var dividend = index;
        // Fill from least significant digit into the last position, so index 0 is most significant position
        for (var pos = Length - 1; pos >= 0; pos--)
        {
            var elementIndex = (int)BigInteger.Remainder(dividend, Base);
            arrayBuilder[pos] = _elementByIndex[elementIndex];
            dividend = BigInteger.Divide(dividend, Base);
        }

        return new(index, arrayBuilder.ToImmutable());
    }

    #region IEnumerable{T} Members

    public IEnumerator<Variation<T>> GetEnumerator()
    {
        var index = BigInteger.Zero;
        while (index.CompareTo(Count) != 0)
        {
            yield return CreateVariation(index++);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}
