namespace GA.Business.Core.Intervals;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Atonal;
using GA.Core.Collections;
using GA.Core.Extensions;
using ChromaticInterval = Interval.Chromatic;

public class ChromaticIntervalsFromRoot(IEnumerable<ChromaticInterval> intervals)
    : IReadOnlyCollection<ChromaticInterval>
{
    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlyCollection{T}" />
    /// </summary>
    private PrintableReadOnlyCollection<ChromaticInterval> Intervals { get; } = intervals.ToImmutableList().AsPrintable();

    /// <summary>
    ///     Gets the <see cref="PitchClassSet" />
    /// </summary>
    public PitchClassSet PitchClassSet => Intervals.ToPitchClassSet();

    /// <inheritdoc />
    public override string ToString() => Intervals.PrintOut;

    #region IReadonlyCollection<Interval.Chromatic> Members

    public IEnumerator<ChromaticInterval> GetEnumerator() => Intervals.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Intervals).GetEnumerator();
    public int Count => Intervals.Count;

    #endregion
}
