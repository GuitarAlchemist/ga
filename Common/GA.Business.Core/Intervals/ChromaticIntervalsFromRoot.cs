namespace GA.Business.Core.Intervals;

using Atonal;
using Extensions;
using ChromaticInterval = Interval.Chromatic;

public class ChromaticIntervalsFromRoot(IEnumerable<ChromaticInterval> intervals)
    : IReadOnlyCollection<ChromaticInterval>
{
    #region IReadonlyCollection<Interval.Chromatic> Members

    public IEnumerator<Interval.Chromatic> GetEnumerator() => Intervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Intervals).GetEnumerator();
    public int Count => Intervals.Count;

    #endregion

    /// <summary>
    /// Gets the <see cref="PrintableReadOnlyCollection{ChromaticInterval}"/>
    /// </summary>
    public PrintableReadOnlyCollection<ChromaticInterval> Intervals { get; } = intervals.ToImmutableList().AsPrintable();

    /// <summary>
    /// Gets the <see cref="PitchClassSet"/>
    /// </summary>
    public PitchClassSet PitchClassSet => Intervals.ToPitchClassSet();

    /// <inheritdoc />
    public override string ToString() => Intervals.PrintOut;
}