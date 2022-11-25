namespace GA.Business.Core.Intervals;

using Primitives;
using Atonal;
using GA.Core.Collections;

public static class IntervalExtensions
{
    public static Indexer<IntervalSize, IntervalQuality> ToQualityByNumber(this IEnumerable<Interval.Simple> intervals) =>
        new(intervals.DistinctBy(i => i.Size)
                     .ToImmutableDictionary(i => i.Size, i => i.Quality));

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Interval.Simple> intervals)
    {
        var pitchClasses = new List<PitchClass>();
        foreach (var interval in intervals)
        {
            var value = interval.ToSemitones().Value;
            var pitchClass = new PitchClass {Value = value};
            pitchClasses.Add(pitchClass);
        }

        return new(pitchClasses);
    }
}
