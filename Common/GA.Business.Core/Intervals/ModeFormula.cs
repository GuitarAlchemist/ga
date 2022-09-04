namespace GA.Business.Core.Intervals;

using Tonal.Modes;
using GA.Core;

public class ModeFormula : IReadOnlyCollection<ModeInterval>
{
    public ModeFormula(ScaleMode mode)
    {
        Mode = mode ?? throw new ArgumentNullException(nameof(mode));
        Intervals = GetModeIntervals(mode);
        ColorTones = Intervals.Where(interval => interval.IsColorTone).ToImmutableList();
    }

    public ScaleMode Mode { get; }
    public IReadOnlyCollection<ModeInterval> Intervals { get; }
    public IReadOnlyCollection<ModeInterval> ColorTones { get; }

    private static IReadOnlyCollection<ModeInterval> GetModeIntervals(ScaleMode mode)
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
                .ToImmutableList()
                .AsPrintable();
        return result;
    }

    public IEnumerator<ModeInterval> GetEnumerator() => Intervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) Intervals).GetEnumerator();
    public int Count => Intervals.Count;

    public override string ToString() => Intervals.ToString() ?? string.Empty;
}