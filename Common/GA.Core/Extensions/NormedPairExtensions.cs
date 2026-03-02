namespace GA.Core.Extensions;

using Collections;
using Combinatorics;

[PublicAPI]
public static class NormedPairExtensions
{
    extension<T, TNorm>(IEnumerable<NormedPair<T, TNorm>> normedPairs)
        where T : IStaticPairNorm<T, TNorm>
        where TNorm : struct, IValueObject<TNorm>
    {
        /// <summary>
        ///     Index by norm.
        /// </summary>
        /// <returns>
        ///     The <see cref="ILookup{TKey,TElement}" /> where the key type is <see cref="TNorm" /> and the element type is
        ///     <see cref="OrderedPair{T}" />
        /// </returns>
        public ILookup<TNorm, OrderedPair<T>> ByNorm()
        {
            ArgumentNullException.ThrowIfNull(normedPairs);

            return normedPairs.ToLookup(pair => pair.Norm, pair => new OrderedPair<T>(pair.Item1, pair.Item2));
        }
    }

    extension<T, TNorm>(IEnumerable<NormedPair<T, TNorm>> normedPairs)
        where T : IStaticPairNorm<T, TNorm>, IValueObject
        where TNorm : struct, IStaticReadonlyCollection<TNorm>, IValueObject<TNorm>
    {
        /// <summary>
        ///     Get the counts for each possible <see cref="TNorm" />.
        /// </summary>
        /// <param name="predicate">The predicate of <see cref="NormedPair{T, TNorm" /> </param>
        /// <returns>The <see cref="ImmutableSortedDictionary{TKey,TValue}" />.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ImmutableSortedDictionary<TNorm, int> ByNormCounts(
            Func<NormedPair<T, TNorm>, bool>? predicate = null)
        {
            ArgumentNullException.ThrowIfNull(normedPairs);

            if (predicate != null)
            {
                normedPairs = normedPairs.Where(predicate);
            }

            normedPairs = normedPairs.Where(pair => pair.Item1.Value <= pair.Item2.Value); // De-duplicate pairs
            var countByNorm = normedPairs.ByNorm().GetCounts();

            var dictBuilder = ImmutableSortedDictionary.CreateBuilder<TNorm, int>();
            foreach (var norm in TNorm.Items)
            {
                if (countByNorm.TryGetValue(norm, out var count))
                {
                    dictBuilder[norm] = count;
                }
                else
                {
                    dictBuilder[norm] = 0;
                }
            }

            return dictBuilder.ToImmutable();
        }
    }
}
