namespace GA.Domain.Core.Instruments.Biomechanics;

/// <summary>
///     Represents a complete hand pose (all joint angles)
/// </summary>
public record HandPose
{
    /// <summary>Joint angles for all fingers (radians)</summary>
    /// <remarks>
    ///     Organized as: [finger0_joint0_flexion, finger0_joint0_abduction, finger0_joint1_flexion, ...]
    /// </remarks>
    public ImmutableArray<float> JointAngles { get; init; } = [];

    /// <summary>Hand model this pose is for</summary>
    public HandModel Model { get; init; } = HandModel.CreateStandardAdult();

    /// <summary>Wrist angles (flexion, deviation, rotation) in radians.</summary>
    public Vector3 WristAngles { get; init; } = Vector3.Zero;

    /// <summary>
    ///     Create rest pose (all joints at rest angles)
    /// </summary>
    public static HandPose CreateRestPose(HandModel model)
    {
        var angles = new List<float>();

        foreach (var finger in model.Fingers)
        {
            foreach (var joint in finger.Joints)
            {
                angles.Add(joint.RestFlexion);
                if (joint.DegreesOfFreedom == 2)
                {
                    angles.Add(joint.RestAbduction);
                }
            }
        }

        return new()
        {
            JointAngles = [..angles],
            Model = model,
            WristAngles = Vector3.Zero
        };
    }

    /// <summary>
    ///     Check if all joint angles are within biomechanical limits
    /// </summary>
    public bool IsValid()
    {
        var angleIndex = 0;

        foreach (var finger in Model.Fingers)
        {
            foreach (var joint in finger.Joints)
            {
                var flexion = JointAngles[angleIndex++];
                var abduction = joint.DegreesOfFreedom == 2 ? JointAngles[angleIndex++] : 0;

                if (!joint.IsWithinLimits(flexion, abduction))
                {
                    return false;
                }
            }
        }

        var limits = Model.WristLimits;
        return WristAngles.X >= limits.MinFlexion && WristAngles.X <= limits.MaxFlexion &&
               WristAngles.Y >= limits.MinDeviation && WristAngles.Y <= limits.MaxDeviation &&
               WristAngles.Z >= limits.MinRotation && WristAngles.Z <= limits.MaxRotation;
    }

    /// <summary>
    ///     Clamp all joint angles to biomechanical limits
    /// </summary>
    public HandPose ClampToLimits()
    {
        var clampedAngles = new List<float>();
        var angleIndex = 0;

        foreach (var finger in Model.Fingers)
        {
            foreach (var joint in finger.Joints)
            {
                var flexion = JointAngles[angleIndex++];
                var abduction = joint.DegreesOfFreedom == 2 ? JointAngles[angleIndex++] : 0;

                var (clampedFlexion, clampedAbduction) = joint.ClampToLimits(flexion, abduction);
                clampedAngles.Add(clampedFlexion);
                if (joint.DegreesOfFreedom == 2)
                {
                    clampedAngles.Add(clampedAbduction);
                }
            }
        }

        var limits = Model.WristLimits;
        var clampedWrist = new Vector3(
            Math.Clamp(WristAngles.X, limits.MinFlexion, limits.MaxFlexion),
            Math.Clamp(WristAngles.Y, limits.MinDeviation, limits.MaxDeviation),
            Math.Clamp(WristAngles.Z, limits.MinRotation, limits.MaxRotation));

        return this with { JointAngles = [..clampedAngles], WristAngles = clampedWrist };
    }
}