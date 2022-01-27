namespace GA.Business.Core.Intervals;

using System.Text;
using Primitives;

public sealed class ModeInterval
{
    public DiatonicNumber Degree { get; init; }
    public Quality Quality { get; init; }
    public Quality RefQuality { get; init; }
    public bool IsColorTone => Quality != RefQuality;

    public override string ToString()
    {
        var sb = new StringBuilder();
        var isAccidentalPrinted = false;
        var accidental = Quality.ToAccidental(Degree.IsPerfect);
        if (IsColorTone)
        {
            sb.Append(">");
            if (!accidental.HasValue)
            {
                var refAccidental = RefQuality.ToAccidental(Degree.IsPerfect);
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