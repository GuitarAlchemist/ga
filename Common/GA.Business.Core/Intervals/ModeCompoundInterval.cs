using GA.Business.Core.Intervals.Primitives;

namespace GA.Business.Core.Intervals;

public sealed class ModeCompoundInterval : ModeIntervalBase<CompoundIntervalSize>
{
    public ModeCompoundInterval(
        CompoundIntervalSize degree, 
        IntervalQuality quality, 
        IntervalQuality refQuality) 
            : base(degree, quality, refQuality)
    {
    }
    
    public ModeInterval ToSimple()
    {
        return new(Size.ToSimple(), Quality, RefQuality);
    }
}