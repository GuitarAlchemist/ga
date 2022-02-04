using GA.Business.Core.Intervals.Primitives;
using GA.Core;

namespace GA.Business.Core.Intervals;

public static class IntervalExtensions
{
    public static Indexer<DiatonicNumber, Quality> ToQualityByNumber(this IEnumerable<Interval.Simple> intervals) =>
        new(
            intervals.DistinctBy(simple => simple.Number)
                     .ToDictionary(interval => interval.Number,
                                   interval => interval.Quality)
        );
}
