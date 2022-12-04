namespace GA.Core.Extensions;

using Collections;

[PublicAPI]
public static class NormedPairExtensions
{
    /// <summary>
    /// Index by norm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TNorm">The norm type.</typeparam>
    /// <param name="normedPairs">The collection of <see cref="NormedPair{T, TNorm}"/></param>
    /// <returns>The <see cref="ILookup{TKey,TElement}"/> where the key type is <see cref="TNorm"/> and the element type is <see cref="Pair{T}"/></returns>
    public static ILookup<TNorm, Pair<T>> ByNorm<T, TNorm>(this IEnumerable<NormedPair<T, TNorm>> normedPairs) 
        where T : IStaticNorm<T, TNorm>
        where TNorm : struct
    {
        if (normedPairs == null) throw new ArgumentNullException(nameof(normedPairs));

        return normedPairs.ToLookup(pair => pair.Norm, pair => new Pair<T>(pair.Item1, pair.Item2));
    }

    /// <summary>
    /// Get the counts for each <see cref="TNorm"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TNorm">The norm type.</typeparam>
    /// <param name="normedPairs">The collection of <see cref="NormedPair{T, TNorm}"/></param>
    /// <param name="predicate">The predicate of <see cref="NormedPair{T, TNorm"/> </param>
    /// <returns>The <see cref="ImmutableSortedDictionary{TKey,TValue}"/>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ImmutableSortedDictionary<TNorm, int> ByNormCounts<T, TNorm>(
        this IEnumerable<NormedPair<T, TNorm>> normedPairs,
        Func<NormedPair<T, TNorm>, bool>? predicate = null)
            where T : IStaticNorm<T, TNorm>
            where TNorm : struct
    {
        if (normedPairs == null) throw new ArgumentNullException(nameof(normedPairs));
        if (predicate != null) normedPairs = normedPairs.Where(predicate);
        return normedPairs.ByNorm().GetCounts();
    }
}