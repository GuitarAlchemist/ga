namespace GA.Business.Core.Intervals;

using System.Text;

using Primitives;

public abstract class ModeIntervalBase<TDegree>
    where TDegree : IDiatonicNumber
{
    protected ModeIntervalBase(
        TDegree degree, 
        Quality quality, 
        Quality refQuality)
    {
        Degree = degree;
        Quality = quality;
        RefQuality = refQuality;
    }

    public TDegree Degree { get; }
    public Quality Quality { get; }
    public Quality RefQuality { get; }
    public bool IsColorTone => Quality != RefQuality;
    public bool IsPerfect => Degree.IsPerfect;

    public override string ToString()
    {
        var sb = new StringBuilder();
        var isAccidentalPrinted = false;
        var isPerfect = IsPerfect;
        var accidental = Quality.ToAccidental(isPerfect);
        if (IsColorTone)
        {
            sb.Append(">");
            if (!accidental.HasValue)
            {
                var refAccidental = RefQuality.ToAccidental(isPerfect);
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
        sb.Append(Degree);
        var result =sb.ToString().PadLeft(3);

        return result;
    }
}