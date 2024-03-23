namespace GA.Business.Core.Intervals;

using Tonal.Modes;

public class ModeFormula : IReadOnlyCollection<ModeInterval>
{
    public ModeFormula(ScaleMode mode)
    {
        Mode = mode ?? throw new ArgumentNullException(nameof(mode));

        var modeIntervals = ModeIntervals(mode);
        var colorTones = ModeColorTones(modeIntervals);

        Intervals = modeIntervals.AsPrintable();
        ColorTones = colorTones.AsPrintable();
    }

    public ScaleMode Mode { get; }
    public IReadOnlyCollection<ModeInterval> Intervals { get; }
    public IReadOnlyCollection<ModeInterval> ColorTones { get; }

    private static ImmutableList<ModeInterval> ModeIntervals(ScaleMode mode)
    {
        var qualityByNumber = mode.Intervals.ToQualityByNumber();
        var refQualityByNumber = mode.RefMode.Intervals.ToQualityByNumber();

        ModeInterval CreateModeInterval(Interval.Simple interval)
        {
            var size = interval.Size;
            var quality = qualityByNumber[size];
            var refQuality = refQualityByNumber[size];

            return new(size, quality, refQuality);
        }

        var result =
            mode.Intervals
                .Select(CreateModeInterval)
                .ToImmutableList();
        return result;
    }

    private static ImmutableList<ModeInterval> ModeColorTones(ImmutableList<ModeInterval> modeIntervals)
    {
        var result = 
            modeIntervals
                .Where(interval => interval.IsColorTone)
                .ToImmutableList();

        return result;
    }
    
    public IEnumerator<ModeInterval> GetEnumerator() => Intervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) Intervals).GetEnumerator();
    public int Count => Intervals.Count;

    public override string ToString() => Intervals.ToString() ?? string.Empty;
}