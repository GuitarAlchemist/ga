namespace GA.Business.Core.Intervals.Primitives;

/// <summary>
///     See https://en.wikibooks.org/wiki/Music_Theory/Consonance_and_Dissonance
/// </summary>
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
