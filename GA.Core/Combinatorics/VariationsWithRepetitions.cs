namespace GA.Core.Combinatorics;

using Collections;

/// <summary>
/// Arrange collection items into all possible arrays (Collection items can be used multiples times) - Also called "k-tuple"
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
public class VariationsWithRepetitions<T> : IIndexer<BigInteger, Variation<T>>,
                                            IEnumerable<Variation<T>>
    where T : notnull
{
    #region IIndexer{BigInteger, Variation{T}} Members
    /// <summary>
    /// Gets a variation.
    /// </summary>
    /// <param name="index">The <see cref="BigInteger"/> key.</param>
    /// <returns>The <see cref="ImmutableArray{T}"/></returns>
    public Variation<T> this[BigInteger index] => GetVariation(index);

    #endregion

    #region IEnumerable{Variation{T}}

    public IEnumerator<Variation<T>> GetEnumerator()
    {
        var key = BigInteger.Zero;
        while (key.CompareTo(Count) != 0) yield return GetVariation(key++);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    /// <summary>
    /// The number of possible variations.
    /// </summary>
    public BigInteger Count { get; }

    private readonly BigInteger _base;
    private readonly int _length;
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
    /// <param name="elements">The initial <see cref="IReadOnlyCollection{T}"/>.</param>
    /// <param name="length"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public VariationsWithRepetitions(
        IReadOnlyCollection<T> elements,
        int length)
    {
        Elements = elements ?? throw new ArgumentNullException(nameof(elements));
        _base = new(elements.Count);
        Count = BigInteger.Pow(_base, length);
        _length = length;

        _indexByElement =
            elements.Select((o, i) => (o, i))
                .ToImmutableDictionary(
                    tuple => tuple.o,
                    tuple => tuple.i);
        _elementByIndex =
            elements.Select((o, i) => (o, i))
                .ToImmutableDictionary(
                    tuple => tuple.i,
                    tuple => tuple.o);

        _lazyElementsSet = new(() => Elements.ToImmutableHashSet());
    }

    /// <summary>
    /// The possible <see cref="IReadOnlyCollection{T}"/> elements to choose from (The "alphabet").
    /// </summary>
    public IReadOnlyCollection<T> Elements { get; }

    /// <summary>
    /// Gets the key for a variation.
    /// </summary>
    /// <param name="variation">The <see cref="ImmutableArray{T}"/></param>
    /// <returns></returns>
    public BigInteger GetKey(IEnumerable<T> variation)
    {
        var variationArray = variation.Take(_length).ToImmutableArray();
        if (!_lazyElementsSet.Value.IsSupersetOf(variationArray))
        {
            var invalidItems = variationArray.Except(_lazyElementsSet.Value).ToImmutableArray();
            var sInvalidItems = string.Join(", ", invalidItems.Take(5));
            if (invalidItems.Length >= 5) sInvalidItems += "...";
            throw new ArgumentException($"Invalid {nameof(variation)} - {invalidItems.Length} items are not in initial collection: {sInvalidItems}");
        }

        // Decompose the key is a series of items
        // key = index(item0) + index(item1) * _base ^ 1 + index(item2) * _base ^ 2 + etc...
        // key = Sum(index(itemX * _base ^ X); X: 0..count(items)
        var result = new BigInteger(0);
        var weight = new BigInteger(1);
        var values = variationArray.Select(item => _indexByElement[item]);
        foreach (var value in values)
        {
            result += value * weight;
            weight *= _base;
        }
        return result;
    }

    /// <summary>
    /// Gets a variation for its index.
    /// </summary>
    /// <param name="index">The variation index.</param> in lexicographical order (See https://en.wikipedia.org/wiki/Lexicographic_order)
    private Variation<T> GetVariation(BigInteger index)
    {
        var arrayBuilder = ImmutableArray.CreateBuilder<T>(_length);
        var dividend = index;
        for (var i = 0; i < _length; i++)
        {
            var elementIndex = (int)BigInteger.Remainder(dividend, _base);
            arrayBuilder.Add(_elementByIndex[elementIndex]);
            dividend = BigInteger.Divide(index, _base);
        }

        return new(index, arrayBuilder.ToImmutable());
    }

}