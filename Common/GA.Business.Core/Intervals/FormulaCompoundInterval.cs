namespace GA.Business.Core.Intervals;

using Primitives;

public sealed class FormulaCompoundInterval(CompoundIntervalSize size, IntervalQuality quality)
    : FormulaInterval<CompoundIntervalSize>(size, quality);
