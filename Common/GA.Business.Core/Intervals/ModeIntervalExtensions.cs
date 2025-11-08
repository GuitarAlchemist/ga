namespace GA.Business.Core.Intervals;

using GA.Core.Extensions;

public static class ModeIntervalExtensions
{
    public static IReadOnlyCollection<ScaleModeCompoundInterval> ToCompound(
        this IReadOnlyCollection<ScaleModeSimpleInterval> modeIntervals)
    {
        ArgumentNullException.ThrowIfNull(modeIntervals);

        return
            modeIntervals.Select(interval => interval.ToCompound())
                .ToImmutableList()
                .AsPrintable();
    }
}
