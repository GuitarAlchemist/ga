namespace GA.Domain.Core.Primitives;



/// <summary>
///     Scale or chord formula simple interval class
/// </summary>
/// <param name="size">The <see cref="SimpleIntervalSize" /></param>
/// <param name="quality">The <see cref="IntervalQuality" /></param>
public sealed class FormulaSimpleInterval(SimpleIntervalSize size, IntervalQuality quality)
    : FormulaInterval<SimpleIntervalSize>(size, quality);
