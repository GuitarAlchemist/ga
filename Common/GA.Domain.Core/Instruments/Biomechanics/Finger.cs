namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Represents a complete finger with all joints
/// </summary>
public record Finger
{
    /// <summary>Finger type</summary>
    public FingerType Type { get; init; }

    /// <summary>Joints from base to tip</summary>
    public ImmutableList<FingerJoint> Joints { get; init; } = [];

    /// <summary>Base position relative to palm origin (mm)</summary>
    public Vector3 BasePosition { get; init; }

    /// <summary>Total degrees of freedom</summary>
    public int TotalDof => Joints.Sum(j => j.DegreesOfFreedom);

    /// <summary>Total finger length (mm)</summary>
    public float TotalLength => Joints.Sum(j => j.BoneLength);
}
