namespace GA.Business.Core.Intervals;

using Primitives;

public abstract class ModeIntervalBase<TIntervalSize>(TIntervalSize size,
    IntervalQuality quality,
    IntervalQuality refQuality)
    where TIntervalSize : IIntervalSize
{
    public TIntervalSize Size { get; } = size;
    public IntervalQuality Quality { get; } = quality;
    public IntervalQuality RefQuality { get; } = refQuality;
    public bool IsColorTone => Quality != RefQuality;
    public IntervalSizeConsonance Consonance => Size.Consonance;

    public Semitones ToSemitones()
    {
        var result = Size.ToSemitones();
        var accidental = Quality.ToAccidental(Consonance);
        if (accidental.HasValue) result += accidental.Value.ToSemitones();
        return result;
    }

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