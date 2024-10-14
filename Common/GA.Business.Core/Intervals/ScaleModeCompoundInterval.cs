namespace GA.Business.Core.Intervals;

using Primitives;

public sealed class ScaleModeCompoundInterval(CompoundIntervalSize degree, IntervalQuality quality, IntervalQuality refQuality) 
    : ScaleModeIntervalBase<CompoundIntervalSize>(degree, quality, refQuality)
{
    /// <summary>
    /// Creates a simple interval from the compound interval
    /// </summary>
    /// <returns>The <see cref="ScaleModeCompoundInterval"/></returns>
    public ScaleModeSimpleInterval ToSimple() => new(Size.ToSimple(), Quality, RefQuality);
}