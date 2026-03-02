namespace GA.Domain.Core.Primitives.Extensions;

using GA.Core.Collections;
using Intervals;
using Theory.Atonal;

public static class IntervalExtensions
{
    public static Indexer<SimpleIntervalSize, IntervalQuality> ToQualityByNumber(this IEnumerable<Interval.Simple> intervals) =>
        new(intervals.DistinctBy(i => i.Size)
            .ToImmutableDictionary(i => i.Size, i => i.Quality));

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Interval.Chromatic> intervals) => new(intervals.Select(i => PitchClass.FromValue(i.Size.Value)));
}
