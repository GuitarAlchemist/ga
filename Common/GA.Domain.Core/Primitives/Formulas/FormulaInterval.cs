namespace GA.Domain.Core.Primitives.Formulas;

using Intervals;

/// <summary>
///     Scale or chord formula interval abstract class (Strongly-typed)
/// </summary>
/// <typeparam name="TIntervalSize">The interval size type (Must implement <see cref="IIntervalSize" />)</typeparam>
/// <param name="size">The <paramtyperef name="TIntervalSize" /></param>
/// <param name="quality">The <see cref="IntervalQuality" /></param>
public abstract class FormulaInterval<TIntervalSize>(TIntervalSize size, IntervalQuality quality)
    : FormulaIntervalBase(size, quality)
    where TIntervalSize : IIntervalSize
{
    /// <summary>
    ///     Gets the <typeparamref name="TIntervalSize" />
    /// </summary>
    public new TIntervalSize Size { get; } = size;
}
