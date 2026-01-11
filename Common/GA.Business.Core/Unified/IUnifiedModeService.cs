namespace GA.Business.Core.Unified;

using System.Collections.Generic;
using Atonal;

/// <summary>
///     Read-only service that unifies tonal modes and atonal modal families under a single abstraction.
///     Provides analysis methods for mode comparison, voice leading, brightness ranking, and Z-relations.
/// </summary>
public interface IUnifiedModeService
{
    /// <summary>
    ///     Build a unified mode instance from a pitch-class set and a chosen root.
    ///     The root is used only for contextualization; the class identity remains root-agnostic.
    /// </summary>
    UnifiedModeInstance FromPitchClassSet(PitchClassSet set, PitchClass root);

    /// <summary>
    ///     Build a unified mode instance from a tonal scale mode.
    /// </summary>
    UnifiedModeInstance FromScaleMode(Tonal.Modes.ScaleMode mode, PitchClass root);

    /// <summary>
    ///     Enumerate all rotations (modal members) for a class, contextualized at the given root.
    /// </summary>
    IEnumerable<UnifiedModeInstance> EnumerateRotations(UnifiedModeClass modeClass, PitchClass root);

    /// <summary>
    ///     Produce a unified description combining set-theoretic facts and any available family info.
    /// </summary>
    UnifiedModeDescription Describe(UnifiedModeInstance instance);

    /// <summary>
    ///     Calculates the number of common tones (invariant pitch classes) between two mode instances.
    /// </summary>
    int GetCommonToneCount(UnifiedModeInstance a, UnifiedModeInstance b);

    /// <summary>
    ///     Calculates a voice leading distance metric (minimal semitone displacement sum).
    ///     Returns double.PositiveInfinity for sets of different cardinalities.
    /// </summary>
    /// <remarks>
    ///     Uses a simple sorted-pair metric. For optimal bijection, consider the Hungarian algorithm.
    /// </remarks>
    double GetVoiceLeadingDistance(UnifiedModeInstance a, UnifiedModeInstance b);

    /// <summary>
    ///     Ranks all rotations of a modal family by brightness (sum of pitch class values).
    ///     Higher brightness = "brighter" mode (e.g., Lydian is brighter than Locrian).
    /// </summary>
    /// <returns>Ordered enumerable from brightest to darkest mode.</returns>
    IEnumerable<(UnifiedModeInstance Instance, int Brightness)> RankByBrightness(UnifiedModeClass modeClass, PitchClass root);

    /// <summary>
    ///     Finds Z-related set classes (sets with same interval class vector but different prime forms).
    ///     Z-relations are musicologically significant pairs with identical intervallic content.
    /// </summary>
    IEnumerable<(PitchClassSet Set1, PitchClassSet Set2)> GetZRelatedPairs();
}

