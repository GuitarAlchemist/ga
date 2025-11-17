namespace GA.Business.Core.Unified;

using Atonal;

/// <summary>
///     Read-only service that unifies tonal modes and atonal modal families under a single abstraction.
///     This layer is additive and does not modify existing types.
/// </summary>
public interface IUnifiedModeService
{
    /// <summary>
    ///     Build a unified mode instance from a pitch-class set and a chosen root.
    ///     The root is used only for contextualization; the class identity remains root-agnostic.
    /// </summary>
    UnifiedModeInstance FromPitchClassSet(PitchClassSet set, PitchClass root);

    /// <summary>
    ///     Enumerate all rotations (modal members) for a class, contextualized at the given root.
    /// </summary>
    IEnumerable<UnifiedModeInstance> EnumerateRotations(UnifiedModeClass modeClass, PitchClass root);

    /// <summary>
    ///     Produce a unified description combining set-theoretic facts and any available family info.
    /// </summary>
    UnifiedModeDescription Describe(UnifiedModeInstance instance);

    // Future tonal adapter (stub): ScaleMode → Unified
    UnifiedModeInstance FromScaleMode(Tonal.Modes.ScaleMode mode, PitchClass root);
}
