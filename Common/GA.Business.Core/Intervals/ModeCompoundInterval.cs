namespace GA.Business.Core.Intervals;

using Primitives;



public sealed class ModeCompoundInterval(
        CompoundIntervalSize degree,
        IntervalQuality quality,
        IntervalQuality refQuality)
    : ModeIntervalBase<CompoundIntervalSize>(degree, quality, refQuality)
{
    public ModeInterval ToSimple()
    {
        return new(Size.ToSimple(), Quality, RefQuality);
    }
}