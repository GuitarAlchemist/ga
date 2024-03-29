using GA.Core.Collections.Abstractions;

namespace GA.Core.Combinatorics;

using Collections;

/// <summary>
/// Ordered arrangements of elements where repetition is allowed - Also called "k-tuple"
/// </summary>
/// <typeparam name="T"></typeparam>
/// <remarks>
/// Inspired from https://weibeld.net/math/combinatorics.html#variations-with-repetitions-k-tuples
/// 
/// Arrangement of k elements from a set of n elements into a sequence of length k, where each element of the set may be chosen multiple times.
/// 
/// Known as k-tuples in the English literature.
/// k &lt; n or k = n or k &gt; n
/// In many cases, the elements of the set can be thought of as classes from which multiple objects can be instantiated.
/// Can also be seen as forming words of length k over an alphabet of size n
///
/// Example:
///
/// Set: {a,b,c}
/// n=3
/// k=2
/// 9 words: (a,a),(a,b),(a,c),(b,a),(b,b),(b,c),(c,a),(c,b),(c,c)
/// 
/// </remarks>
[PublicAPI]
public class VariationsWithRepetitions<T> : IEnumerable<Variation<T>>,
                                            IIndexer<BigInteger, Variation<T>>
    where T : notnull
{
    #region IIndexer<BigInteger, Variation<T>>

    /// <summary>
    /// Gets a variation given its index.
    /// </summary>
    /// <param name="index">The <see cref="BigInteger"/> Lexicographical-order index</param>
    /// <returns>The <see cref="ImmutableArray{T}"/></returns>
    public Variation<T> this[BigInteger index] => CreateVariation(index);

    #endregion

    #region IEnumerable{T} Members

    public IEnumerator<Variation<T>> GetEnumerator()
    {
        var index = BigInteger.Zero;
        while (index.CompareTo(Count) != 0) yield return CreateVariation(index++);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    /// <summary>
    /// The number of possible variations.
    /// </summary>
    public BigInteger Count { get; }

    private readonly ImmutableDictionary<T, int> _indexByElement;
    private readonly ImmutableDictionary<int, T> _elementByIndex;
    private readonly Lazy<ImmutableHashSet<T>> _lazyElementsSet;

    /// <summary>
    /// Creates a <see cref="VariationsWithRepetitions{T}"/> instance.
    /// </summary>
    /// <remarks>
    /// - <see cref="elements"/> : the "alphabet"
    /// - <see cref="length"/>   : the length of generated "words"
    /// </remarks>
    /// <param name="elements">The initial <see cref="IEnumerable{T}"/>.</param>
    /// <param name="length">The number of items in each variation.</param>
    /// <param name="predicate">Initial elements predicate (Optional).</param>
    /// <exception cref="ArgumentNullException"></exception>
    public VariationsWithRepetitions(
        IEnumerable<T> elements,
        int length,
        Func<T, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(elements);
        
        if (predicate != null) elements = elements.Where(predicate).ToImmutableList();
        Elements = elements.ToImmutableList();
        Base = new(Elements.Count);
        Count = BigInteger.Pow(Base, length);
        IndexFormat = $"D{(int)Math.Floor(BigInteger.Log10(Count) + 1)}";
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

    public string IndexFormat { get; }

    /// <summary>
    /// The possible <see cref="IReadOnlyCollection{T}"/> elements to choose from (The "alphabet").
    /// </summary>
    public IReadOnlyCollection<T> Elements { get; }

    /// <summary>
    /// Gets the number of elements in a variation.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the <see cref="BigInteger"/> number base for computing variation indices.
    /// </summary>
    public BigInteger Base { get; }

    /// <summary>
    /// Gets the index of a variation.
    /// </summary>
    /// <param name="variation">The <see cref="ImmutableArray{T}"/></param>
    /// <returns>The <see cref="BigInteger"/> index.</returns>
    public BigInteger GetIndex(IEnumerable<T> variation)
    {
        var variationArray = variation.Take(Length).ToImmutableArray();
        if (!_lazyElementsSet.Value.IsSupersetOf(variationArray))
        {
            var invalidItems = variationArray.Except(_lazyElementsSet.Value).ToImmutableArray();
            var sInvalidItems = string.Join(", ", invalidItems.Take(5));
            if (invalidItems.Length >= 5) sInvalidItems += "...";
            throw new ArgumentException($"Invalid {nameof(variation)} - {invalidItems.Length} items are not in initial collection: {sInvalidItems}");
        }

        // Decompose the variation index is a series of items
        // index = indexof(item0) + indexof(item1) * _base ^ 1 + indexof(item2) * _base ^ 2 + etc...
        var result = new BigInteger(0);
        var weight = new BigInteger(1);
        var values = variationArray.Select(item => _indexByElement[item]);
        foreach (var value in values)
        {
            result += new BigInteger(value) * weight;
            weight *= Base;
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
    /// Create a variation from an index.
    /// </summary>
    /// <param name="index">The variation index.</param> in lexicographical order (See https://en.wikipedia.org/wiki/Lexicographic_order)
    private Variation<T> CreateVariation(BigInteger index)
    {
        var arrayBuilder = ImmutableArray.CreateBuilder<T>(Length);
        var dividend = index;
        for (var i = 0; i < Length; i++)
        {
            var elementIndex = (int)BigInteger.Remainder(dividend, Base);
            arrayBuilder.Add(_elementByIndex[elementIndex]);
            dividend = BigInteger.Divide(dividend, Base);
        }
        return new(index, arrayBuilder.ToImmutable());
    }
}