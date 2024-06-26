﻿namespace GA.Core.Extensions;

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
    /// <returns>The <see cref="ILookup{TKey,TElement}"/> where the key type is <see cref="TNorm"/> and the element type is <see cref="OrderedPair{T}"/></returns>
    public static ILookup<TNorm, OrderedPair<T>> ByNorm<T, TNorm>(this IEnumerable<NormedPair<T, TNorm>> normedPairs) 
        where T : IStaticPairNorm<T, TNorm>
        where TNorm : struct, IValueObject<TNorm>
    {
        ArgumentNullException.ThrowIfNull(normedPairs);

        return normedPairs.ToLookup(pair => pair.Norm, pair => new OrderedPair<T>(pair.Item1, pair.Item2));
    }

    /// <summary>
    /// Get the counts for each possible <see cref="TNorm"/>.
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
            where T : IStaticPairNorm<T, TNorm>, IValueObject
            where TNorm : struct, IStaticReadonlyCollection<TNorm>, IValueObject<TNorm>
    {
        ArgumentNullException.ThrowIfNull(normedPairs);
        
        if (predicate != null) normedPairs = normedPairs.Where(predicate);
        normedPairs = normedPairs.Where(pair => pair.Item1.Value <= pair.Item2.Value); // De-duplicate pairs
        var countByNorm = normedPairs.ByNorm().GetCounts();

        var dictBuilder = ImmutableSortedDictionary.CreateBuilder<TNorm, int>();
        foreach (var norm in TNorm.Items)
        {
            if (countByNorm.TryGetValue(norm, out var count)) dictBuilder[norm] = count;
            else dictBuilder[norm] = 0;
        }
        return dictBuilder.ToImmutable();
    }
}