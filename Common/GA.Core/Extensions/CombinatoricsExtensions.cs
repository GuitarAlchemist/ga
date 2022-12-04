namespace GA.Core.Extensions;

using Combinatorics;

[PublicAPI]
public static class CombinatoricsExtensions
{
    /// <summary>
    /// Get the TxT cartesian product.
    /// </summary>
    /// <typeparam name="T">The item type,</typeparam>
    /// <param name="items">The <see cref="IEnumerable{T}"/>,</param>
    /// <param name="predicate">The <see cref="Func{T, Boolean}"/> (Optional).</param>
    /// <returns>The <see cref="CartesianProduct{T,TPair}"/>.</returns>
    public static CartesianProduct<T> ToCartesianProduct<T>(
        this IEnumerable<T> items,
        Func<T, bool>? predicate = null) where T : notnull
            => new(items, predicate);

    /// <summary>
    /// Get the <typeparamref name="TNorm"/>-normed cartesian product.
    /// </summary>
    /// <typeparam name="T">The item type,</typeparam>
    /// <typeparam name="TNorm">The norm type.</typeparam>
    /// <param name="items">The <see cref="IEnumerable{T}"/>,</param>
    /// <param name="predicate">The <see cref="Func{T, Boolean}"/> (Optional).</param>
    /// <returns>The <see cref="CartesianProduct{T,TPair}"/>.</returns>
    public static NormedCartesianProduct<T, TNorm> ToNormedCartesianProduct<T, TNorm>(
        this IEnumerable<T> items, 
        Func<T, bool>? predicate = null) where T : IStaticNorm<T, TNorm>
                                         where TNorm : struct
            => new(items, predicate);
}