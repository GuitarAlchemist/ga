﻿namespace GA.Business.Core.Extensions;

using Atonal;
using Atonal.Primitives;
using Atonal.Abstractions;

public static class PitchClassSetExtensions
{
    /// <summary>
    /// Creates a pitch class set from notes or pitches
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{IPitchClass}"/></param>
    /// <returns>The <see cref="PitchClassSet"/></returns>
    public static PitchClassSet ToPitchClassSet(this IEnumerable<IPitchClass> items) => new(items.Select(item => item.PitchClass));
    
    /// <summary>
    /// Indicates whether the collection of objects represent a modal pitch class set
    /// </summary>
    /// <param name="items">The <see cref="IEnumerable{IPitchClass}"/></param>
    /// <returns>True if modal, false otherwise</returns>
    public static bool IsModal(this IEnumerable<IPitchClass> items) => items.ToPitchClassSet().IsModal;    

    /// <summary>
    /// Creates a lookup of pitch class sets, by cardinality
    /// </summary>
    /// <param name="pitchClassSets"></param>
    /// <returns></returns>
    public static ILookup<Cardinality, PitchClassSet> ByCardinality(this IEnumerable<PitchClassSet> pitchClassSets) => pitchClassSets.ToLookup(pitchClassSet => pitchClassSet.Cardinality);
}