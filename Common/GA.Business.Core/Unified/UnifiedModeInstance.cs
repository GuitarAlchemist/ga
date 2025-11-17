namespace GA.Business.Core.Unified;

using Atonal;

/// <summary>
///     Unified instance: a specific rotation (mode index) contextualized at a root pitch class.
/// </summary>
public sealed class UnifiedModeInstance(
    UnifiedModeClass @class,
    int rotationIndex,
    PitchClass root,
    PitchClassSet rotationSet)
{
    public UnifiedModeClass Class { get; } = @class;
    public int RotationIndex { get; } = rotationIndex;
    public PitchClass Root { get; } = root;
    public PitchClassSet RotationSet { get; } = rotationSet;
}
