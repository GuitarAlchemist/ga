namespace GA.Core.Combinatorics;

using Collections;
using System.Linq;

/// <summary>
/// Computes all possible combinations
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class Combinations<T> : IEnumerable<Variation<T>>,
                               IIndexer<BigInteger, Variation<T>>
    where T: struct, IStaticValueObjectList<T>
{
    #region IIndexer<BigInteger, Variation<T>>

    /// <summary>
    /// Gets a variation given its index.
    /// </summary>
    /// <param name="index">The <see cref="BigInteger"/> Lexicographical-order index</param>
    /// <returns>The <see cref="ImmutableArray{T}"/></returns>
    public Variation<T> this[BigInteger index] => CreateVariation(index);

    #endregion

    #region IReadOnlyCollection<Variation<T>> Members

    public IEnumerator<Variation<T>> GetEnumerator()
    {
        var index = BigInteger.Zero;
        while (index.CompareTo(Count) != 0) yield return CreateVariation(index++);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public BigInteger Count => _boolVariations.Count;

    #endregion

    private static Lazy<ImmutableDictionary<T, BigInteger>> _lazyWeightByItem;
    private readonly VariationsWithRepetitions<bool> _boolVariations;
    private readonly VariationFormat _variationFormat;

    static Combinations()
    {
        _lazyWeightByItem = new(GetWeightByItem);
    }

    public Combinations()
    {
        _boolVariations = new(new[] {false, true}, T.Items.Count);
        _variationFormat = new($"D{(int)Math.Floor(BigInteger.Log10(_boolVariations.Count) + 1)}", T.Items.Count * 2);
    }

    /// <summary>
    /// The initial <see cref="IReadOnlyCollection{T}"/> elements to combine.
    /// </summary>
    public IReadOnlyCollection<T> Elements => T.Items;

    public override string ToString() => $"{typeof(T).Name}: {Elements}; {_boolVariations}";

    /// <summary>
    /// Gets the index of a variation.
    /// </summary>
    /// <param name="variation">The <see cref="IEnumerable{T}"/></param>
    /// <returns>The <see cref="BigInteger"/> index (Lexicographical order).</returns>
    public BigInteger GetIndex(IEnumerable<T> variation) => variation.Aggregate(BigInteger.Zero, (index, item) => index + _lazyWeightByItem.Value[item]);

    private static ImmutableDictionary<T, BigInteger> GetWeightByItem()
    {
        var weight = BigInteger.One;
        var weightByItemBuilder = ImmutableDictionary.CreateBuilder<T, BigInteger>();
        foreach (var item in T.Items)
        {
            weightByItemBuilder.Add(item, weight);
            weight <<= 1;
        }

        return weightByItemBuilder.ToImmutable();
    }

    private Variation<T> CreateVariation(BigInteger index)
    {
        var arrayBuilder = ImmutableArray.CreateBuilder<T>();
        using var itemsEnumerator = T.Items.GetEnumerator();
        foreach (var b in _boolVariations[index])
        {
            itemsEnumerator.MoveNext();
            if (b) arrayBuilder.Add(itemsEnumerator.Current);
        }

        return new(index, arrayBuilder.ToImmutable(), _variationFormat);
    }
}