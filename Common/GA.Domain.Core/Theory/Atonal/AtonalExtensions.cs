namespace GA.Domain.Core.Theory.Atonal;

using Abstractions;
using GA.Core.Abstractions;
using GA.Core.Collections;
using GA.Core.Extensions;
using Primitives.Intervals;
using Primitives.Notes;

[PublicAPI]
public static class AtonalExtensions
{
    /// <summary>
    ///     Gets the printable chromatic notes.
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{PitchClass}" />.</param>
    /// <returns>The <see cref="Interval.Chromatic" /></returns>
    public static PrintableReadOnlyCollection<Note.Chromatic> ToChromaticNotes(IEnumerable<PitchClass> items) => items.Select(pc => new Note.Chromatic(pc)).ToImmutableList().AsPrintable();

    /// <summary>
    ///     Gets the interval class vector
    /// </summary>
    /// <remarks>
    ///     The reasoning here is that if the item class type can measure a norm between 2 items Interval Class unit, then an
    ///     Interval Class Vector can be computed
    /// </remarks>
    /// <returns>The <see cref="IntervalClassVector" /></returns>
    public static IntervalClassVector ToIntervalClassVector<T>(this IEnumerable<T> items) where T : IValueObject, IStaticPairIntervalClassNorm<T>
    {
        ArgumentNullException.ThrowIfNull(items);

        // PitchClassSet.IntervalClassVector lands here with its ImmutableSortedSet<PitchClass>, on every read.
        // A set of pitch classes is one of 4096 values, so the id is computed once per set, by the general
        // path below, and read back afterwards.
        if (items is ImmutableSortedSet<PitchClass> pitchClasses && ReferenceEquals(pitchClasses.KeyComparer, Comparer<PitchClass>.Default))
        {
            return new(PitchClassSetIntervalClassVectorId(pitchClasses));
        }

        return ComputeIntervalClassVector(items);
    }

    private static IntervalClassVector ComputeIntervalClassVector<T>(IEnumerable<T> items) where T : IValueObject, IStaticPairIntervalClassNorm<T>
    {
        var normedCartesianProduct = items.ToNormedCartesianProduct<T, IntervalClass>();
        var countByNorm = normedCartesianProduct.ByNormCounts(pair => pair.Norm.Value > 0);
        return new(countByNorm);
    }

    // Interval-class vector id + 1 by pitch-class set mask, so that 0 means "not computed yet"
    // (a set with fewer than two pitch classes has the id 0). Two threads racing on a slot write the same value.
    private static readonly int[] _intervalClassVectorIdPlusOneBySetMask = new int[4096];

    private static IntervalClassVectorId PitchClassSetIntervalClassVectorId(ImmutableSortedSet<PitchClass> pitchClasses)
    {
        var mask = 0;
        foreach (var pitchClass in pitchClasses) mask |= 1 << pitchClass.Value;

        var stored = _intervalClassVectorIdPlusOneBySetMask[mask];
        if (stored == 0)
        {
            stored = ComputeIntervalClassVector(pitchClasses).Id.Value + 1;
            _intervalClassVectorIdPlusOneBySetMask[mask] = stored;
        }

        return new(stored - 1);
    }
}
