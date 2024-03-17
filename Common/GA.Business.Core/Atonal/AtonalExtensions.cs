namespace GA.Business.Core.Atonal;

using GA.Core;
using Intervals;
using Notes;
using GA.Core.Collections;
using GA.Core.Extensions;
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
    /// Gets the interval class vector.
    /// </summary>
    /// <typeparam name="T">The items type (Must implement <see cref="IValueObject"/> and <see cref="IStaticNorm{T, IntervalClass}"/>).</typeparam>
    /// <param name="items"></param>
    /// <returns></returns>
    public static IntervalClassVector ToIntervalClassVector<T>(this IEnumerable<T> items)
        where T : IValueObject, IStaticNorm<T, IntervalClass>
    {
        ArgumentNullException.ThrowIfNull(items);
        
        var normedCartesianProduct = items.ToNormedCartesianProduct<T, IntervalClass>();
        var countByNorm = normedCartesianProduct.ByNormCounts(pair => pair.Norm.Value > 0);
        return new(countByNorm);
    }
}
