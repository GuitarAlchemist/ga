namespace GA.Domain.Core.Primitives;



public sealed class FormulaCompoundInterval(CompoundIntervalSize size, IntervalQuality quality)
    : FormulaInterval<CompoundIntervalSize>(size, quality);
