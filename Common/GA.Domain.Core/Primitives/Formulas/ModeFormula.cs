namespace GA.Domain.Core.Primitives.Formulas;

using Extensions;
using GA.Core.Collections;
using Intervals;
using Theory.Tonal.Modes;

/// <summary>
///     Mode formula in the context of tonal music theory
/// </summary>
/// <param name="mode">The <see cref="ScaleMode" /></param>
[PublicAPI]
public class ModeFormula(ScaleMode mode) : IReadOnlyCollection<ScaleModeSimpleInterval>
{
    public ScaleMode Mode { get; } = mode ?? throw new ArgumentNullException(nameof(mode));
    public PrintableReadOnlyCollection<ScaleModeSimpleInterval> Intervals { get; } = CreateModeIntervals(mode);

    public PrintableReadOnlyCollection<ScaleModeSimpleInterval> CharacteristicIntervals { get; } =
        CreateCharacteristicIntervals(CreateModeIntervals(mode));

    public IEnumerator<ScaleModeSimpleInterval> GetEnumerator() => Intervals.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Intervals).GetEnumerator();

    public int Count => Intervals.Count;

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
    ///     An enumerable collection of `ScaleModeSimpleInterval` representing the mode intervals.
    /// </param>
    /// <returns>
    ///     A `PrintableReadOnlyCollection` of `ScaleModeSimpleInterval` containing intervals where the `IsColorTone` property
    ///     is true.
    /// </returns>
    private static PrintableReadOnlyCollection<ScaleModeSimpleInterval> CreateCharacteristicIntervals(
        IEnumerable<ScaleModeSimpleInterval> modeIntervals) => modeIntervals
        .Where(interval => interval.IsCharacteristic)
        .ToImmutableList()
        .AsPrintable();

    /// <inheritdoc />
    public override string ToString() => Intervals.ToString();
}
