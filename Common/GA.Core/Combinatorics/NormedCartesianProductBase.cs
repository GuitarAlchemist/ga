using GA.Core.Collections.Abstractions;

namespace GA.Core.Combinatorics;

using Collections;
using Extensions;

/// <summary>
/// (T x T) variations, with ||(T,T)||
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <typeparam name="TNorm">The norm type.</typeparam>
[PublicAPI]
public class NormedCartesianProductBase<T, TNorm>(
    IEnumerable<T> items,
    Func<T, bool>? predicate = null) : CartesianProductBase<T, NormedPair<T, TNorm>>(items, pair => new(pair), predicate)
    where T : IStaticNorm<T, TNorm>, IValueObject
    where TNorm : struct, IValueObject<TNorm>, IStaticReadonlyCollection<TNorm>
{
    /// <summary>
    /// Get the counts for each <see cref="TNorm"/>.
    /// </summary>
    /// <typeparam name="TItemsCollection">The <see cref="TItemsCollection"/>.</typeparam>
    /// <param name="predicate">A <see cref="Func{T, Boolean}"/> (Optional).</param>
    /// <returns>The <see cref="ImmutableDictionary{TNorm, Int32}"/>.</returns>
    public static ImmutableSortedDictionary<TNorm, int> NormCounts<TItemsCollection>(Func<T, bool>? predicate = null)
        where TItemsCollection : IStaticReadonlyCollection<T>
        => NormCounts(TItemsCollection.Items, predicate);

    /// <summary>
    /// Get the counts for each <see cref="TNorm"/>.
    /// </summary>
    /// <param name="items"></param>
    /// <param name="predicate">A <see cref="Func{T, Boolean}"/> (Optional).</param>
    /// <returns>The <see cref="ImmutableDictionary{TNorm, Int32}"/>.</returns>
    public static ImmutableSortedDictionary<TNorm, int> NormCounts(IEnumerable<T> items, Func<T, bool>? predicate  = null) 
        => new NormedCartesianProductBase<T, TNorm>(items, predicate).ByNormCounts();

    public override string ToString()
    {
        return $"{base.ToString()}; Norms: {GetNormsDescription()}";

        StringBuilder GetNormsDescription()
        {
            var sb = new StringBuilder();
            var groupings = this.ToLookup(pair => pair.Norm).OrderBy(grouping => grouping.Key);
            foreach (var grouping in groupings)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append($"{grouping.Key} x {grouping.Count()}");
            }
            return sb;
        }
    }
}