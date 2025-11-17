namespace GA.Core.Combinatorics;

/// <summary>
///     Computes all possible combinations
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class Combinations<T> : IEnumerable<Variation<T>>,
    IIndexer<BigInteger, Variation<T>> where T : notnull
{
    private readonly VariationsWithRepetitions<bool> _boolVariations;

    private readonly Lazy<ImmutableDictionary<T, BigInteger>> _lazyWeightByItem;
    private readonly VariationFormat _variationFormat;

    public Combinations(IReadOnlyCollection<T> elements)
    {
        Elements = elements ?? throw new ArgumentNullException(nameof(elements));
        _lazyWeightByItem = new(GetWeightByItem);
        _boolVariations = new([false, true], elements.Count);
        _variationFormat = new($"D{(int)Math.Floor(BigInteger.Log10(_boolVariations.Count) + 1)}", elements.Count * 2);
    }

    /// <summary>
    ///     The initial <see cref="IReadOnlyCollection{T}" /> elements to combine.
    /// </summary>
    public IReadOnlyCollection<T> Elements { get; }

    #region IIndexer<BigInteger, Variation<T>>

    /// <summary>
    ///     Gets a variation given its index.
    /// </summary>
    /// <param name="index">The <see cref="BigInteger" /> Lexicographical-order index</param>
    /// <returns>The <see cref="ImmutableArray{T}" /></returns>
    public Variation<T> this[BigInteger index] => CreateVariation(index);

    #endregion

    public override string ToString()
    {
        return $"{typeof(T).Name}: {Elements}; {_boolVariations}";
    }

    /// <summary>
    ///     Gets the index of a variation.
    /// </summary>
    /// <param name="variation">The <see cref="IEnumerable{T}" /></param>
    /// <returns>The <see cref="BigInteger" /> index (Lexicographical order).</returns>
    public BigInteger GetIndex(IEnumerable<T> variation)
    {
        return variation.Aggregate(BigInteger.Zero, (index, item) => index + _lazyWeightByItem.Value[item]);
    }

    private ImmutableDictionary<T, BigInteger> GetWeightByItem()
    {
        // MSB-first weighting: first element has highest weight (2^(n-1))
        var count = Elements.Count;
        var weight = BigInteger.One << (count == 0 ? 0 : count - 1);
        var weightByItemBuilder = ImmutableDictionary.CreateBuilder<T, BigInteger>();
        foreach (var item in Elements)
        {
            weightByItemBuilder.Add(item, weight);
            weight >>= 1;
        }

        return weightByItemBuilder.ToImmutable();
    }

    private Variation<T> CreateVariation(BigInteger index)
    {
        var arrayBuilder = ImmutableArray.CreateBuilder<T>();
        using var itemsEnumerator = Elements.GetEnumerator();
        foreach (var b in _boolVariations[index])
        {
            itemsEnumerator.MoveNext();
            if (b)
            {
                arrayBuilder.Add(itemsEnumerator.Current);
            }
        }

        return new(index, arrayBuilder.ToImmutable(), _variationFormat);
    }

    #region IReadOnlyCollection<Variation<T>> Members

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

    public BigInteger Count => _boolVariations.Count;

    #endregion
}
