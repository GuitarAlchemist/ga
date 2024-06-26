﻿namespace GA.Core.Combinatorics;

/// <summary>
/// TxT variations (With custom pair type).
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <typeparam name="TPair">The item pair type.</typeparam>
[PublicAPI]
public abstract class CartesianProductBase<T, TPair>(
        IEnumerable<T> items,
        Func<OrderedPair<T>, TPair>? selector = null,
        Func<T, bool>? predicate = null) : IEnumerable<TPair> // where T : IItemCollection<T>
    where T : notnull
    where TPair : OrderedPair<T>
{
    #region IEnumerable<TPair> Members

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<TPair> GetEnumerator()
    {
        var pairs = Variations.GetPairs();
        return selector == null ? 
            pairs.Cast<TPair>().GetEnumerator() : 
            pairs.Select(selector).GetEnumerator();
    }

    #endregion

    /// <summary>
    /// Gets the <see cref="VariationsWithRepetitions{T}"/>
    /// </summary>
    public VariationsWithRepetitions<T> Variations { get; } = new(items, 2, predicate);

    /// <summary>
    /// Gets the <see cref="BigInteger"/> pair count.
    /// </summary>
    public BigInteger Count => Variations.Count;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(" ", Variations.Elements));
        var t = typeof(T).Name;
        sb.Append($" => {t} x {t}: {Count} variations");
        return sb.ToString();
    }
}