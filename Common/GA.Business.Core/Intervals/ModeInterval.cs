using GA.Business.Core.Intervals.Primitives;

namespace GA.Business.Core.Intervals;

public sealed class ModeInterval : ModeIntervalBase<IntervalSize>
{
    public ModeInterval(
        IntervalSize size,
        IntervalQuality quality, 
        IntervalQuality refQuality) 
            : base(size, quality, refQuality)
    {
    }

    public ModeCompoundInterval ToCompound()
    {
        return new(
            Size.ToCompound(), 
            Quality, 
            RefQuality);
    }
}