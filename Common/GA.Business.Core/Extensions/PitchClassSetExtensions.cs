namespace GA.Business.Core.Extensions;

using Atonal;
using Atonal.Abstractions;
using Atonal.Primitives;

public static class PitchClassSetExtensions
{
    /// <summary>
    ///     Creates a pitch class set from notes or pitches
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{IPitchClass}" /></param>
    /// <returns>The <see cref="PitchClassSet" /></returns>
    public static PitchClassSet ToPitchClassSet(this IEnumerable<IPitchClass> items)
    {
        return new PitchClassSet(items.Select(item => item.PitchClass));
    }

    /// <summary>
    ///     Indicates whether the collection of objects represent a modal pitch class set
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{IPitchClass}" /></param>
    /// <returns>True if modal, false otherwise</returns>
    public static bool IsModal(this IEnumerable<IPitchClass> items)
    {
        return items.ToPitchClassSet().IsModal;
    }

    /// <summary>
    ///     Creates a lookup of pitch class sets, by cardinality
    /// </summary>
    /// <param name="pitchClassSets"></param>
    /// <returns></returns>
    public static ILookup<Cardinality, PitchClassSet> ByCardinality(this IEnumerable<PitchClassSet> pitchClassSets)
    {
        return pitchClassSets.ToLookup(pitchClassSet => pitchClassSet.Cardinality);
    }
}
