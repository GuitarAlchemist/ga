namespace GA.Business.Core.Intervals;

using System.Collections.Immutable;
using GA.Core;

public static class ModeIntervalExtensions
{
    public static IReadOnlyCollection<ModeCompoundInterval> ToCompound(this IReadOnlyCollection<ModeInterval> modeIntervals)
    {
        if (modeIntervals == null) throw new ArgumentNullException(nameof(modeIntervals));

        return 
            modeIntervals.Select(interval => interval.ToCompound())
                         .ToImmutableList()
                         .AsPrintable();
    }
}