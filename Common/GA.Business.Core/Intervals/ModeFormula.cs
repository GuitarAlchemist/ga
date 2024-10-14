namespace GA.Business.Core.Intervals;

using Tonal.Modes;

public class ModeFormula(ScaleMode mode) : IReadOnlyCollection<ScaleModeSimpleInterval>
{
    public ScaleMode Mode { get; } = mode ?? throw new ArgumentNullException(nameof(mode));
    public PrintableReadOnlyCollection<ScaleModeSimpleInterval> Intervals { get; } = CreateModeIntervals(mode);
    public PrintableReadOnlyCollection<ScaleModeSimpleInterval> ColorTones { get; } = CreateModeColorTones(CreateModeIntervals(mode));

    private static PrintableReadOnlyCollection<ScaleModeSimpleInterval> CreateModeIntervals(ScaleMode mode)
    {
        var qualityByNumber = mode.SimpleIntervals.ToQualityByNumber();
        var refQualityByNumber = mode.RefMode.SimpleIntervals.ToQualityByNumber();

        return mode.SimpleIntervals
            .Select(CreateModeInterval)
            .ToImmutableList()
            .AsPrintable();

        ScaleModeSimpleInterval CreateModeInterval(Interval.Simple interval)
        {
            var size = interval.Size;
            var quality = qualityByNumber[size];
            var refQuality = refQualityByNumber[size];
            return new(size, quality, refQuality);
        }
    }

    private static PrintableReadOnlyCollection<ScaleModeSimpleInterval> CreateModeColorTones(IEnumerable<ScaleModeSimpleInterval> modeIntervals) =>
        modeIntervals
            .Where(interval => interval.IsColorTone)
            .ToImmutableList()
            .AsPrintable();
    
    public IEnumerator<ScaleModeSimpleInterval> GetEnumerator() => Intervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) Intervals).GetEnumerator();
    public int Count => Intervals.Count;

    public override string ToString() => Intervals.ToString();
}