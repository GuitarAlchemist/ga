namespace GA.Business.Core.Intervals;

using System.Text;

using Primitives;

public abstract class ModeIntervalBase<TIntervalSize>
    where TIntervalSize : IIntervalSize
{
    protected ModeIntervalBase(
        TIntervalSize size, 
        IntervalQuality quality, 
        IntervalQuality refQuality)
    {
        Size = size;
        Quality = quality;
        RefQuality = refQuality;
    }

    public TIntervalSize Size { get; }
    public IntervalQuality Quality { get; }
    public IntervalQuality RefQuality { get; }
    public bool IsColorTone => Quality != RefQuality;
    public IntervalSizeConsonance Consonance => Size.Consonance;

    public override string ToString()
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
        var result =sb.ToString().PadLeft(3);

        return result;
    }
}