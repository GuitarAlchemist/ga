namespace GA.Business.Core.Atonal.Grothendieck;

using System.Collections.Generic;
using JetBrains.Annotations;

/// <summary>
///     Service for Grothendieck group operations on pitch-class sets
/// </summary>
[PublicAPI]
public interface IGrothendieckService
{
    /// <summary>
    ///     Compute the interval-class vector for a pitch-class set
    /// </summary>
    /// <param name="pitchClasses">Pitch classes (0-11)</param>
    /// <returns>Interval-class vector</returns>
    IntervalClassVector ComputeIcv(IEnumerable<int> pitchClasses);

    /// <summary>
    ///     Compute the Grothendieck delta between two ICVs
    /// </summary>
    /// <param name="source">Source ICV</param>
    /// <param name="target">Target ICV</param>
    /// <returns>Signed delta</returns>
    GrothendieckDelta ComputeDelta(IntervalClassVector source, IntervalClassVector target);

    /// <summary>
    ///     Compute harmonic cost (L1 norm of delta)
    /// </summary>
    /// <param name="delta">Grothendieck delta</param>
    /// <returns>Harmonic cost (0 = no change, higher = more change)</returns>
    double ComputeHarmonicCost(GrothendieckDelta delta);

    /// <summary>
    ///     Find pitch-class sets within a given harmonic distance
    /// </summary>
    /// <param name="source">Source pitch-class set</param>
    /// <param name="maxDistance">Maximum L1 norm distance</param>
    /// <returns>Nearby pitch-class sets with their deltas</returns>
    IEnumerable<(PitchClassSet Set, GrothendieckDelta Delta, double Cost)> FindNearby(
        PitchClassSet source,
        int maxDistance);

    /// <summary>
    ///     Find the shortest harmonic path between two pitch-class sets
    /// </summary>
    /// <param name="source">Source pitch-class set</param>
    /// <param name="target">Target pitch-class set</param>
    /// <param name="maxSteps">Maximum number of intermediate steps</param>
    /// <returns>Path of pitch-class sets</returns>
    IEnumerable<PitchClassSet> FindShortestPath(
        PitchClassSet source,
        PitchClassSet target,
        int maxSteps = 5);
}
