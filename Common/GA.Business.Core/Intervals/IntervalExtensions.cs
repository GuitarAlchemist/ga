using GA.Business.Core.SetTheory;

namespace GA.Business.Core.Intervals;

using Primitives;
using Notes;
using GA.Business.Core.Notes.Primitives;
using GA.Core;

public static class IntervalExtensions
{
    public static Indexer<IntervalSize, IntervalQuality> ToQualityByNumber(this IEnumerable<Interval.Simple> intervals) =>
        new(
            intervals.DistinctBy(simple => simple.Size)
                     .ToDictionary(interval => interval.Size,
                                   interval => interval.Quality)
        );

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Interval.Simple> intervals)
    {
        var PitchClasses = new List<PitchClass>();
        foreach (var interval in intervals)
        {
            var value = interval.ToSemitones().Value;
            var PitchClass = new PitchClass {Value = value};
            PitchClasses.Add(PitchClass);
        }

        return new(PitchClasses);
    }
}
