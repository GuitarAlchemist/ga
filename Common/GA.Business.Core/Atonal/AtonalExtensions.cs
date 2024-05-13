namespace GA.Business.Core.Atonal;

using Abstractions;
using Intervals;
using Notes;
using Primitives;

[PublicAPI]
public static class AtonalExtensions
{
    /// <summary>
    /// Gets the printable chromatic notes.
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{PitchClass}"/>.</param>
    /// <returns>The <see cref="Interval.Chromatic"/></returns>
    public static PrintableReadOnlyCollection<Note.Chromatic> ToChromaticNotes(IEnumerable<PitchClass> items)
        => items.Select(pc => new Note.Chromatic(pc)).ToImmutableList().AsPrintable();

    /// <summary>
    /// Gets the interval class vector
    /// </summary>
    /// <typeparam name="T">The items type (Must implement <see cref="IValueObject"/> and <see cref="IStaticPairIntervalClassNorm{T}"/>).</typeparam>
    /// <param name="items">The <see cref="IEnumerable{T}"/> collection of items</param>
    /// <remarks>
    /// The reasoning here is that if the item class type can measure a norm between 2 items Interval Class unit, then an Interval Class Vector can be computed
    /// </remarks>
    /// <returns>The <see cref="IntervalClassVector"/></returns>
    public static IntervalClassVector ToIntervalClassVector<T>(this IEnumerable<T> items)
        where T : IValueObject, IStaticPairIntervalClassNorm<T>
    {
        ArgumentNullException.ThrowIfNull(items);
        
        var normedCartesianProduct = items.ToNormedCartesianProduct<T, IntervalClass>();
        var countByNorm = normedCartesianProduct.ByNormCounts(pair => pair.Norm.Value > 0);
        return new(countByNorm);
    }
}
