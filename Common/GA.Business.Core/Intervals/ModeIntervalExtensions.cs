namespace GA.Business.Core.Intervals;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
