namespace GA.Business.Core.Intervals;

using System.Collections;
using System.Collections.Immutable;

using Tonal.Modes;
using GA.Core;

public class ModeFormula : IReadOnlyCollection<ModeInterval>
{
    private readonly IReadOnlyCollection<ModeInterval> _modeIntervals;

    public ModeFormula(ScaleMode mode)
    {
        Mode = mode ?? throw new ArgumentNullException(nameof(mode));

        _modeIntervals = GetModeIntervals(mode);
    }

    public ScaleMode Mode { get; }

    private static IReadOnlyCollection<ModeInterval> GetModeIntervals(ScaleMode mode)
    {
        var qualityByNumber = mode.Intervals.ToQualityByNumber();
        var refQualityByNumber = mode.RefMode.Intervals.ToQualityByNumber();

        ModeInterval CreateModeInterval(Interval.Simple interval)
        {
            var number = interval.Number;
            var quality = qualityByNumber[number];
            var refQuality = refQualityByNumber[number];

            return new(number, quality, refQuality);
        }

        var result =
            mode.Intervals
                .Select(CreateModeInterval)
                .ToImmutableList()
                .AsPrintable();
        return result;
    }

    public IEnumerator<ModeInterval> GetEnumerator() => _modeIntervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _modeIntervals).GetEnumerator();
    public int Count => _modeIntervals.Count;

    public override string ToString() => _modeIntervals.ToString() ?? string.Empty;
}