namespace GA.Business.Core.Intervals;

using Primitives;
using Notes;
using GA.Business.Core.Notes.Primitives;
using GA.Core;



public static class IntervalExtensions
{
    public static Indexer<DiatonicNumber, Quality> ToQualityByNumber(this IEnumerable<Interval.Simple> intervals) =>
        new(
            intervals.DistinctBy(simple => simple.Number)
                     .ToDictionary(interval => interval.Number,
                                   interval => interval.Quality)
        );

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
