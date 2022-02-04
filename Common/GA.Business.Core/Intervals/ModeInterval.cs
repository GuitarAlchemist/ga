using GA.Business.Core.Intervals.Primitives;

namespace GA.Business.Core.Intervals;

public sealed class ModeInterval : ModeIntervalBase<DiatonicNumber>
{
    public ModeInterval(
        DiatonicNumber degree, 
        Quality quality, 
        Quality refQuality) 
            : base(degree, quality, refQuality)
    {
    }

    public ModeCompoundInterval ToCompound()
    {
        return new(
            Degree.ToCompound(), 
            Quality, 
            RefQuality);
    }
}