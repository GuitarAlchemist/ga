namespace GA.Core.Extensions;

using Combinatorics;

[PublicAPI]
public static class CombinatoricsExtensions
{
    extension<T>(IEnumerable<T> items) where T : notnull
    {
        /// <summary>
        ///     Get the TxT cartesian product.
        /// </summary>
        /// <param name="predicate">The <see cref="Func{T, Boolean}" /> (Optional).</param>
        /// <returns>The <see cref="CartesianProduct{T,TPair}" />.</returns>
        public CartesianProduct<T> ToCartesianProduct(Func<T, bool>? predicate = null) => new(items, predicate);
    }

    extension<T, TNorm>(IEnumerable<T> items)
        where T : IStaticPairNorm<T, TNorm>, IValueObject
        where TNorm : struct, IValueObject<TNorm>, IStaticReadonlyCollection<TNorm>
    {
        /// <summary>
        ///     Get the <typeparamref name="TNorm" />-normed cartesian product.
        /// </summary>
        /// <param name="predicate">The <see cref="Func{T, Boolean}" /> (Optional).</param>
        /// <returns>The <see cref="CartesianProduct{T,TPair}" />.</returns>
        public NormedCartesianProduct<T, TNorm> ToNormedCartesianProduct(Func<T, bool>? predicate = null) =>
            new(items, predicate);
    }
}
