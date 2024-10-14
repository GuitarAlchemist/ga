namespace GA.Business.Core.Intervals;

using Primitives;

/// <summary>
/// Scale mode interval base class
/// </summary>
/// <typeparam name="TIntervalSize">The interval size type (Must derive from <see cref="IIntervalSize"/>)</typeparam>
/// <param name="size">The <paramtyperef name="TIntervalSize"/></param>
/// <param name="quality">The interval <see cref="IntervalQuality"/></param>
/// <param name="refQuality">The reference interval <see cref="IntervalQuality"/></param>
public abstract class ScaleModeIntervalBase<TIntervalSize>(TIntervalSize size, IntervalQuality quality, IntervalQuality refQuality) 
    : FormulaInterval<TIntervalSize>(size, quality) where TIntervalSize : IIntervalSize
{
    /// <summary>
    /// Gets the reference <see cref="IntervalQuality"/>
    /// </summary>
    public IntervalQuality RefQuality { get; } = refQuality;

    /// <summary>
    /// True if interval quality is different the reference quality
    /// </summary>
    public bool IsColorTone => Quality != RefQuality;

    /// <inheritdoc />
    public override string ToString() => Print();

    private string Print()
    {
        var sb = new StringBuilder();
        var isAccidentalPrinted = false;
        var accidental = Quality.ToAccidental(Consonance);
        if (IsColorTone)
        {
            sb.Append(">");
            if (!accidental.HasValue)
            {
                var refAccidental = RefQuality.ToAccidental(Consonance);
                if (refAccidental.HasValue)
                {
                    sb.Append(Accidental.Natural.ToString());
                    isAccidentalPrinted = true;
                }
            }
        }

        if (!isAccidentalPrinted && accidental.HasValue)
        {
            sb.Append(accidental.Value.ToString());
        }
        sb.Append(Size);
        var result = sb.ToString().PadLeft(3);

        return result;
    }
}