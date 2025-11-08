namespace GA.Business.Core.Intervals;

using Primitives;

/// <summary>
///     Scale mode simple interval class
/// </summary>
/// <param name="size">The <see cref="SimpleIntervalSize" /></param>
/// <param name="quality">The <see cref="IntervalQuality" /></param>
public sealed class ScaleModeSimpleInterval(
    SimpleIntervalSize size,
    IntervalQuality quality,
    IntervalQuality refQuality)
    : ScaleModeIntervalBase<SimpleIntervalSize>(size, quality, refQuality)
{
    /// <summary>
    ///     Creates a compound interval from the simple interval
    /// </summary>
    /// <returns>The <see cref="ScaleModeCompoundInterval" /></returns>
    public ScaleModeCompoundInterval ToCompound()
    {
        return new ScaleModeCompoundInterval(Size.ToCompound(), Quality, RefQuality);
    }
}
