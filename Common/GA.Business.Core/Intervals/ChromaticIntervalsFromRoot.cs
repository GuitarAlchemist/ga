namespace GA.Business.Core.Intervals;

using Atonal;
using GA.Core.Extensions;
using ChromaticInterval = Interval.Chromatic;

public class ChromaticIntervalsFromRoot(IEnumerable<ChromaticInterval> intervals)
    : IReadOnlyCollection<ChromaticInterval>
{
    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlyCollection{ChromaticInterval}" />
    /// </summary>
    public PrintableReadOnlyCollection<ChromaticInterval> Intervals { get; } =
        intervals.ToImmutableList().AsPrintable();

    /// <summary>
    ///     Gets the <see cref="PitchClassSet" />
    /// </summary>
    public PitchClassSet PitchClassSet => Intervals.ToPitchClassSet();

    /// <inheritdoc />
    public override string ToString()
    {
        return Intervals.PrintOut;
    }

    #region IReadonlyCollection<Interval.Chromatic> Members

    public IEnumerator<ChromaticInterval> GetEnumerator()
    {
        return Intervals.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Intervals).GetEnumerator();
    }

    public int Count => Intervals.Count;

    #endregion
}
