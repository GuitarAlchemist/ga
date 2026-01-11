namespace GA.Business.Core.Intervals;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Atonal;
using GA.Core.Collections;
using Primitives;

public static class IntervalExtensions
{
    public static Indexer<SimpleIntervalSize, IntervalQuality>
        ToQualityByNumber(this IEnumerable<Interval.Simple> intervals)
    {
        return new(intervals.DistinctBy(i => i.Size)
            .ToImmutableDictionary(i => i.Size, i => i.Quality));
    }

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Interval.Simple> intervals)
    {
        var pitchClasses =
            intervals
                .Select(interval => interval.Semitones.Value)
                .Select(value => new PitchClass { Value = value })
                .ToList();

        return new(pitchClasses);
    }
}
