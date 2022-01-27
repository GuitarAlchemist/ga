namespace GA.Business.Core.Intervals;

using Primitives;
using Tonal;
using GA.Core;

public class ModeQualities
{
    private readonly ModeQualityIndexer _qualityByNumber;
    private readonly ModeQualityIndexer _refQualityByNumber;
    private readonly IReadOnlyCollection<ModeInterval> _modeIntervals;

    public ModeQualities(Mode mode)
    {
        Mode = mode ?? throw new ArgumentNullException(nameof(mode));
        _qualityByNumber = new(mode);
        _refQualityByNumber = RefQualities();

        _modeIntervals = CreateModeIntervals(mode);
    }

    private IReadOnlyCollection<ModeInterval> CreateModeIntervals(Mode mode)
    {
        var modeIntervals = new List<ModeInterval>();
        foreach (var interval in mode.Intervals)
        {
            modeIntervals.Add(CreateModeInterval(interval));
        }
        var result = modeIntervals.AsReadOnly();
        return result;

        ModeInterval CreateModeInterval(Interval.Simple interval)
        {
            var number = interval.Size;
            return new()
            {
                Degree = number,
                Quality = _qualityByNumber[number],
                RefQuality = _refQualityByNumber[number]
            };
        }
    }

    public Mode Mode { get; }

    public override string ToString() => string.Join(" ", _modeIntervals);

    private ModeQualityIndexer RefQualities()
    {
        var refMode =
            _qualityByNumber[DiatonicNumber.Third] == Quality.Minor
                ? Mode.MajorScale.Aeolian
                : Mode.MajorScale.Ionian;
        return new(refMode);
    }

    public sealed class ModeQualityIndexer : Indexer<DiatonicNumber, Quality>
    {
        public ModeQualityIndexer(Mode mode) : base(ToDictionary(mode)) { }

        private static Dictionary<DiatonicNumber, Quality> ToDictionary(Mode mode) =>
            mode.Intervals.ToDictionary(
                interval => interval.Size,
                interval => interval.Quality);
    }
}
