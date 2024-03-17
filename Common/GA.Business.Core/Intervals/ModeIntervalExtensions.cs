namespace GA.Business.Core.Intervals;

using GA.Core.Extensions;

public static class ModeIntervalExtensions
{
    public static IReadOnlyCollection<ModeCompoundInterval> ToCompound(this IReadOnlyCollection<ModeInterval> modeIntervals)
    {
        ArgumentNullException.ThrowIfNull(modeIntervals);

        return 
            modeIntervals.Select(interval => interval.ToCompound())
                         .ToImmutableList()
                         .AsPrintable();
    }
}