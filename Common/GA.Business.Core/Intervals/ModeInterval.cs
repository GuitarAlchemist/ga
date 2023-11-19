namespace GA.Business.Core.Intervals;

using Primitives;



public sealed class ModeInterval(IntervalSize size,
        IntervalQuality quality,
        IntervalQuality refQuality)
    : ModeIntervalBase<IntervalSize>(size, quality, refQuality)
{
    public ModeCompoundInterval ToCompound() => new(Size.ToCompound(), Quality, RefQuality);

}