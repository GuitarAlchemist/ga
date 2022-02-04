using GA.Business.Core.Intervals.Primitives;

namespace GA.Business.Core.Intervals;

public sealed class ModeCompoundInterval : ModeIntervalBase<CompoundDiatonicNumber>
{
    public ModeCompoundInterval(
        CompoundDiatonicNumber degree, 
        Quality quality, 
        Quality refQuality) 
            : base(degree, quality, refQuality)
    {
    }
    
    public ModeInterval ToSimple()
    {
        return new(Degree.ToSimple(), Quality, RefQuality);
    }
}