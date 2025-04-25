namespace GA.Business.Core.Intervals;

using Tonal.Modes;

public class ModeFormula(ScaleMode mode) : IReadOnlyCollection<ScaleModeSimpleInterval>
{
    public ScaleMode Mode { get; } = mode ?? throw new ArgumentNullException(nameof(mode));
    public PrintableReadOnlyCollection<ScaleModeSimpleInterval> Intervals { get; } = CreateModeIntervals(mode);
    public PrintableReadOnlyCollection<ScaleModeSimpleInterval> CharacteristicIntervals { get; } = CreateCharacteristicIntervals(CreateModeIntervals(mode));

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

    /// Creates a collection of characteristic intervals from the provided mode intervals.
    /// Characteristic intervals are intervals where the `IsColorTone` property is true.
    /// <param name="modeIntervals">
    /// An enumerable collection of `ScaleModeSimpleInterval` representing the mode intervals.
    /// </param>
    /// <returns>
    /// A `PrintableReadOnlyCollection` of `ScaleModeSimpleInterval` containing intervals where the `IsColorTone` property is true.
    /// </returns>
    private static PrintableReadOnlyCollection<ScaleModeSimpleInterval> CreateCharacteristicIntervals(IEnumerable<ScaleModeSimpleInterval> modeIntervals) =>
        modeIntervals
            .Where(interval => interval.IsCharacteristic)
            .ToImmutableList()
            .AsPrintable();
    
    public IEnumerator<ScaleModeSimpleInterval> GetEnumerator() => Intervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) Intervals).GetEnumerator();
    public int Count => Intervals.Count;

    public override string ToString() => Intervals.ToString();
}