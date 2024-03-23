namespace GA.Business.Core.Intervals;

using Primitives;
using Atonal;

public static class IntervalExtensions
{
    public static Indexer<IntervalSize, IntervalQuality>
        ToQualityByNumber(this IEnumerable<Interval.Simple> intervals) =>
        new(intervals.DistinctBy(i => i.Size)
            .ToImmutableDictionary(i => i.Size, i => i.Quality));

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Interval.Simple> intervals)
    {
        var pitchClasses =
            intervals
                .Select(interval => interval.ToSemitones().Value)
                .Select(value => new PitchClass { Value = value })
                .ToList();

        return new(pitchClasses);
    }
}
