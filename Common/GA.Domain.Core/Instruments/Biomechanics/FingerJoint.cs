namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Represents a single finger joint with biomechanical constraints
/// </summary>
public record FingerJoint
{
    /// <summary>Joint type</summary>
    public JointType Type { get; init; }

    /// <summary>Bone length from this joint to next (mm)</summary>
    public float BoneLength { get; init; }

    /// <summary>Minimum flexion angle (radians)</summary>
    public float MinFlexion { get; init; }

    /// <summary>Maximum flexion angle (radians)</summary>
    public float MaxFlexion { get; init; }

    /// <summary>Minimum abduction angle (radians, if applicable)</summary>
    public float MinAbduction { get; init; }

    /// <summary>Maximum abduction angle (radians, if applicable)</summary>
    public float MaxAbduction { get; init; }

    /// <summary>Rest position flexion angle (radians)</summary>
    public float RestFlexion { get; init; }

    /// <summary>Rest position abduction angle (radians)</summary>
    public float RestAbduction { get; init; }

    /// <summary>Degrees of freedom (1 or 2)</summary>
    public int DegreesOfFreedom => MinAbduction < MaxAbduction ? 2 : 1;

    /// <summary>
    ///     Check if joint angles are within biomechanical limits
    /// </summary>
    public bool IsWithinLimits(float flexion, float abduction = 0)
    {
        return flexion >= MinFlexion && flexion <= MaxFlexion &&
               abduction >= MinAbduction && abduction <= MaxAbduction;
    }

    /// <summary>
    ///     Clamp joint angles to biomechanical limits
    /// </summary>
    public (float flexion, float abduction) ClampToLimits(float flexion, float abduction = 0)
    {
        return (
            Math.Clamp(flexion, MinFlexion, MaxFlexion),
            Math.Clamp(abduction, MinAbduction, MaxAbduction)
        );
    }
}