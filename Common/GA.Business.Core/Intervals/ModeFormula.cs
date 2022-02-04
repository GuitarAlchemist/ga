namespace GA.Business.Core.Intervals;

using System.Collections;
using Primitives;
using Tonal;
using GA.Core;

public class ModeFormula : IReadOnlyCollection<ModeInterval>
{
    private readonly IReadOnlyCollection<ModeInterval> _modeIntervals;

    public ModeFormula(Mode mode)
    {
        Mode = mode ?? throw new ArgumentNullException(nameof(mode));

        _modeIntervals = GetModeIntervals(mode);
    }

    public Mode Mode { get; }

    private static IReadOnlyCollection<ModeInterval> GetModeIntervals(Mode mode)
    {
        var qualityByNumber = mode.Intervals.ToQualityByNumber();
        var refQualityByNumber = mode.RefMode.Intervals.ToQualityByNumber();

        var modeIntervals = new List<ModeInterval>();
        foreach (var interval in mode.Intervals)
        {
            modeIntervals.Add(CreateModeInterval(interval));
        }
        modeIntervals.Add(new(DiatonicNumber.Octave, Quality.Perfect, Quality.Perfect));
        var result = modeIntervals.AsReadOnly().AsPrintable();
        return result;

        ModeInterval CreateModeInterval(Interval.Simple interval)
        {
            var number = interval.Number;
            var quality = qualityByNumber[number];
            var refQuality = refQualityByNumber[number];

            return new(number, quality, refQuality);
        }
    }

    public IEnumerator<ModeInterval> GetEnumerator() => _modeIntervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _modeIntervals).GetEnumerator();
    public int Count => _modeIntervals.Count;

    public override string ToString() => _modeIntervals.ToString() ?? string.Empty;
}