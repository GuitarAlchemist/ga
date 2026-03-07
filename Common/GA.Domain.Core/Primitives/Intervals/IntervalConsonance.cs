namespace GA.Domain.Core.Primitives.Intervals;

/// <summary>
///     Consonance or dissonance classification of a musical interval (<see href="https://en.wikipedia.org/wiki/Consonance_and_dissonance" />).
/// </summary>
/// <remarks>
///     <see href="https://en.wikibooks.org/wiki/Music_Theory/Consonance_and_Dissonance" />
/// </remarks>
public enum IntervalConsonance
{
    /// <summary>
    ///     The perfect consonances are the more stable consonances and are used at points of articulation, e.g. beginnings and
    ///     endings of phrases, sections, pieces
    /// </summary>
    Perfect,

    /// <summary>
    ///     The imperfect consonances are less stable and are used in places where music needs to continue
    /// </summary>
    Imperfect
}
