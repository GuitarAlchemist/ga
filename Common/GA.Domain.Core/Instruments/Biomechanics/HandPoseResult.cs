namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Result of a hand pose computation
/// </summary>
public sealed record HandPoseResult
{
    /// <summary>
    ///     The computed hand pose
    /// </summary>
    public required HandPose Pose { get; init; }

    /// <summary>
    ///     Fingertip positions for each finger
    /// </summary>
    public required ImmutableDictionary<FingerType, FingertipPosition> Fingertips { get; init; }

    /// <summary>
    ///     Whether the pose is valid (within biomechanical limits)
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    ///     Error or quality metric for the pose
    /// </summary>
    public double Error { get; init; } = 0.0;

    /// <summary>
    ///     Get fingertip position for a specific finger
    /// </summary>
    public FingertipPosition GetFingertip(FingerType finger)
    {
        return Fingertips.TryGetValue(finger, out var position)
            ? position
            : new()
                { Position = Vector3.Zero, Normal = Vector3.UnitZ };
    }
}