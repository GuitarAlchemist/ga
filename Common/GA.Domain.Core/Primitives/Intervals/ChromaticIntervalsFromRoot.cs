namespace GA.Domain.Core.Primitives.Intervals;

using GA.Core.Collections;
using GA.Core.Extensions;
using Extensions;
using Theory.Atonal;
using ChromaticInterval = Interval.Chromatic;

/// <summary>
/// Represents a collection of chromatic intervals from the root note
/// </summary>
/// <param name="intervals">The <see cref="ChromaticInterval" /> collection</param>
/// <remarks>
/// Example: <code>[0, 2, 4, 5, 7, 9, 11]</code> represents the intervals from the root note in a major scale.
/// </remarks>
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
