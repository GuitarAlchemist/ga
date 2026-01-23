namespace GA.Domain.Core.Primitives;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GA.Core.Collections;
using Theory.Atonal;

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
        return new PitchClassSet(intervals.Select(i => PitchClass.FromValue(i.Semitones.Value)));
    }

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Interval.Chromatic> intervals)
    {
        return new PitchClassSet(intervals.Select(i => PitchClass.FromValue(i.Size.Value)));
    }
}
