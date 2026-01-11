namespace GA.Business.Core.Fretboard.Biomechanics;

using System;
using System.Collections.Immutable;
using System.Numerics;

/// <summary>
///     Forward kinematics computations for hand models
/// </summary>
public static class ForwardKinematics
{
    /// <summary>
    ///     Compute fingertip positions from a hand pose
    /// </summary>
    public static ImmutableDictionary<FingerType, Vector3> ComputeFingertipPositions(HandPose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);

        var fingertips = ImmutableDictionary.CreateBuilder<FingerType, Vector3>();
        var angleIndex = 0;

        foreach (var finger in pose.Model.Fingers)
        {
            var fingertipPosition = ComputeFingerTip(finger, pose.JointAngles, ref angleIndex, pose.WristAngles);
            fingertips[finger.Type] = fingertipPosition;
        }

        return fingertips.ToImmutable();
    }

    /// <summary>
    ///     Compute the position of a single fingertip
    /// </summary>
    private static Vector3 ComputeFingerTip(Finger finger, ImmutableArray<float> jointAngles, ref int angleIndex,
        Vector3 wristAngles)
    {
        // Start at the finger base position
        var position = finger.BasePosition;
        var orientation = Matrix4x4.Identity;

        // Apply wrist transformation
        var wristTransform = CreateWristTransform(wristAngles);
        position = Vector3.Transform(position, wristTransform);

        // Apply each joint transformation
        foreach (var joint in finger.Joints)
        {
            if (angleIndex >= jointAngles.Length)
            {
                break;
            }

            var flexion = jointAngles[angleIndex++];
            var abduction = angleIndex < jointAngles.Length ? jointAngles[angleIndex++] : 0f;

            // Create joint transformation matrix
            var jointTransform = CreateJointTransform(flexion, abduction);

            // Move along the bone
            var boneVector = new Vector3(0, joint.BoneLength, 0); // Assume bones point along Y-axis
            boneVector = Vector3.Transform(boneVector, jointTransform);

            position += boneVector;
        }

        return position;
    }

    /// <summary>
    ///     Create wrist transformation matrix
    /// </summary>
    private static Matrix4x4 CreateWristTransform(Vector3 wristAngles)
    {
        var rotX = Matrix4x4.CreateRotationX(wristAngles.X);
        var rotY = Matrix4x4.CreateRotationY(wristAngles.Y);
        var rotZ = Matrix4x4.CreateRotationZ(wristAngles.Z);

        return rotZ * rotY * rotX;
    }

    /// <summary>
    ///     Create joint transformation matrix
    /// </summary>
    private static Matrix4x4 CreateJointTransform(float flexion, float abduction)
    {
        var flexionRot = Matrix4x4.CreateRotationX(flexion);
        var abductionRot = Matrix4x4.CreateRotationZ(abduction);

        return abductionRot * flexionRot;
    }

    /// <summary>
    ///     Compute full hand pose result with fingertip positions
    /// </summary>
    public static HandPoseResult ComputeHandPoseResult(HandPose pose)
    {
        var fingertipPositions = ComputeFingertipPositions(pose);
        var fingertips = ImmutableDictionary.CreateBuilder<FingerType, FingertipPosition>();

        foreach (var (finger, position) in fingertipPositions)
        {
            fingertips[finger] = new()
            {
                Position = position,
                Normal = ComputeFingertipNormal(finger, pose),
                Pressure = 0.5f // Default pressure
            };
        }

        return new()
        {
            Pose = pose,
            Fingertips = fingertips.ToImmutable(),
            IsValid = ValidatePose(pose),
            Error = ComputePoseError(pose)
        };
    }

    /// <summary>
    ///     Compute fingertip normal vector
    /// </summary>
    private static Vector3 ComputeFingertipNormal(FingerType finger, HandPose pose)
    {
        // Simplified: assume fingertips point downward for fretting
        return finger == FingerType.Thumb ? Vector3.UnitZ : -Vector3.UnitZ;
    }

    /// <summary>
    ///     Validate that a pose is within biomechanical limits
    /// </summary>
    private static bool ValidatePose(HandPose pose)
    {
        var angleIndex = 0;

        foreach (var finger in pose.Model.Fingers)
        {
            foreach (var joint in finger.Joints)
            {
                if (angleIndex >= pose.JointAngles.Length)
                {
                    return false;
                }

                var flexion = pose.JointAngles[angleIndex++];
                var abduction = angleIndex < pose.JointAngles.Length ? pose.JointAngles[angleIndex++] : 0f;

                // Check joint limits
                if (flexion < joint.MinFlexion || flexion > joint.MaxFlexion ||
                    abduction < joint.MinAbduction || abduction > joint.MaxAbduction)
                {
                    return false;
                }
            }
        }

        // Check wrist limits
        var limits = pose.Model.WristLimits;
        return pose.WristAngles.X >= limits.MinFlexion && pose.WristAngles.X <= limits.MaxFlexion &&
               pose.WristAngles.Y >= limits.MinDeviation && pose.WristAngles.Y <= limits.MaxDeviation &&
               pose.WristAngles.Z >= limits.MinRotation && pose.WristAngles.Z <= limits.MaxRotation;
    }

    /// <summary>
    ///     Compute error metric for a pose
    /// </summary>
    private static double ComputePoseError(HandPose pose)
    {
        if (!ValidatePose(pose))
        {
            return double.MaxValue;
        }

        // Simple error metric based on deviation from rest pose
        var restPose = HandPose.CreateRestPose(pose.Model);
        var error = 0.0;

        for (var i = 0; i < Math.Min(pose.JointAngles.Length, restPose.JointAngles.Length); i++)
        {
            var diff = pose.JointAngles[i] - restPose.JointAngles[i];
            error += diff * diff;
        }

        var wristDiff = pose.WristAngles - restPose.WristAngles;
        error += wristDiff.LengthSquared();

        return Math.Sqrt(error);
    }
}
