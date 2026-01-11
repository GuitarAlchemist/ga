namespace GA.Business.Core.Fretboard.Biomechanics;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;

/// <summary>
///     Finger type enumeration
/// </summary>
public enum FingerType
{
    Thumb = 0,
    Index = 1,
    Middle = 2,
    Ring = 3,
    Little = 4
}

/// <summary>
///     Hand size categories for personalized biomechanical analysis
/// </summary>
public enum HandSize
{
    /// <summary>
    ///     Small hands (children, small adults) - 85% of standard
    ///     Typical hand span: 7-8 inches (18-20 cm)
    /// </summary>
    Small,

    /// <summary>
    ///     Medium hands (average adult) - 100% standard
    ///     Typical hand span: 8-9 inches (20-23 cm)
    /// </summary>
    Medium,

    /// <summary>
    ///     Large hands (large adult) - 115% of standard
    ///     Typical hand span: 9-10 inches (23-25 cm)
    /// </summary>
    Large,

    /// <summary>
    ///     Extra large hands (very large hands) - 130% of standard
    ///     Typical hand span: 10+ inches (25+ cm)
    /// </summary>
    ExtraLarge
}

/// <summary>
///     Joint type enumeration
/// </summary>
public enum JointType
{
    /// <summary>Carpometacarpal (base of finger)</summary>
    Cmc,

    /// <summary>Metacarpophalangeal (knuckle)</summary>
    Mcp,

    /// <summary>Proximal Interphalangeal (middle joint)</summary>
    Pip,

    /// <summary>Distal Interphalangeal (fingertip joint)</summary>
    Dip,

    /// <summary>Interphalangeal (thumb only)</summary>
    Ip
}

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

/// <summary>
///     Biomechanical model of human hand for guitar playing
/// </summary>
/// <remarks>
///     Based on average adult hand dimensions and joint constraints.
///     Can be customized for individual hand sizes.
///     References:
///     - "Hand Anthropometry" - NASA STD-3000
///     - "Biomechanics of the Hand" - Tubiana et al.
/// </remarks>
public record FingerSpreadConstraint
{
    public required FingerType Primary { get; init; }
    public required FingerType Secondary { get; init; }
    public float PreferredSeparationMm { get; init; }
    public float MaxSeparationMm { get; init; }
    public float MinSeparationMm { get; init; }
}

/// <summary>
///     Wrist joint constraint ranges (radians).
///     Flexion: positive = flexion (palm toward forearm), negative = extension.
///     Deviation: positive = radial, negative = ulnar.
///     Rotation: forearm pronation/supination.
/// </summary>
public record WristConstraint(
    float MinFlexion,
    float MaxFlexion,
    float MinDeviation,
    float MaxDeviation,
    float MinRotation,
    float MaxRotation)
{
    public static WristConstraint Default { get; } = new(
        DegreesToRadians(-35f),
        DegreesToRadians(45f),
        DegreesToRadians(-25f),
        DegreesToRadians(25f),
        DegreesToRadians(-30f),
        DegreesToRadians(30f));

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }
}

public record HandModel
{
    /// <summary>Palm width (mm)</summary>
    public float PalmWidth { get; init; } = 85.0f;

    /// <summary>Palm length (mm)</summary>
    public float PalmLength { get; init; } = 100.0f;

    /// <summary>All fingers</summary>
    public ImmutableList<Finger> Fingers { get; init; } = [];

    /// <summary>Wrist joint constraints.</summary>
    public WristConstraint WristLimits { get; init; } = WristConstraint.Default;

    /// <summary>Ergonomic constraints coupling adjacent finger spread.</summary>
    public ImmutableList<FingerSpreadConstraint> FingerSpreadConstraints { get; init; } = [];

    /// <summary>Total degrees of freedom for entire hand</summary>
    public int TotalDof => Fingers.Sum(f => f.TotalDof);

    /// <summary>
    ///     Create standard adult hand model
    /// </summary>
    public static HandModel CreateStandardAdult()
    {
        var fingers = ImmutableList.CreateBuilder<Finger>();

        // Thumb (3 joints: CMC, MCP, IP)
        fingers.Add(new()
        {
            Type = FingerType.Thumb,
            BasePosition = new(-30, 0, 0), // Left side of palm
            Joints = ImmutableList.Create(
                new FingerJoint
                {
                    Type = JointType.Cmc,
                    BoneLength = 20.0f,
                    MinFlexion = ToRadians(-15),
                    MaxFlexion = ToRadians(15),
                    MinAbduction = ToRadians(0),
                    MaxAbduction = ToRadians(80),
                    RestFlexion = ToRadians(0),
                    RestAbduction = ToRadians(45)
                },
                new FingerJoint
                {
                    Type = JointType.Mcp,
                    BoneLength = 20.0f,
                    MinFlexion = ToRadians(0),
                    MaxFlexion = ToRadians(60),
                    MinAbduction = ToRadians(-10),
                    MaxAbduction = ToRadians(10),
                    RestFlexion = ToRadians(10),
                    RestAbduction = ToRadians(0)
                },
                new FingerJoint
                {
                    Type = JointType.Ip,
                    BoneLength = 20.0f,
                    MinFlexion = ToRadians(0),
                    MaxFlexion = ToRadians(80),
                    MinAbduction = ToRadians(0),
                    MaxAbduction = ToRadians(0),
                    RestFlexion = ToRadians(15),
                    RestAbduction = ToRadians(0)
                }
            )
        });

        // Index finger (4 joints: CMC, MCP, PIP, DIP)
        fingers.Add(CreateStandardFinger(FingerType.Index, new(-15, 100, 0), 40, 25, 10));

        // Middle finger (4 joints: CMC, MCP, PIP, DIP)
        fingers.Add(CreateStandardFinger(FingerType.Middle, new(0, 100, 0), 45, 30, 10));

        // Ring finger (4 joints: CMC, MCP, PIP, DIP)
        fingers.Add(CreateStandardFinger(FingerType.Ring, new(15, 100, 0), 42, 28, 10));

        // Little finger (4 joints: CMC, MCP, PIP, DIP)
        fingers.Add(CreateStandardFinger(FingerType.Little, new(30, 100, 0), 35, 20, 10));

        return new()
        {
            PalmWidth = 85.0f,
            PalmLength = 100.0f,
            Fingers = fingers.ToImmutable(),
            FingerSpreadConstraints = CreateStandardFingerSpreadConstraints(),
            WristLimits = WristConstraint.Default
        };
    }

    /// <summary>
    ///     Create a standard finger (index, middle, ring, little)
    /// </summary>
    private static Finger CreateStandardFinger(
        FingerType type,
        Vector3 basePosition,
        float mcpLength,
        float pipLength,
        float dipLength)
    {
        return new()
        {
            Type = type,
            BasePosition = basePosition,
            Joints = ImmutableList.Create(
                new FingerJoint
                {
                    Type = JointType.Cmc,
                    BoneLength = 0, // Fixed joint
                    MinFlexion = ToRadians(0),
                    MaxFlexion = ToRadians(0),
                    MinAbduction = ToRadians(0),
                    MaxAbduction = ToRadians(0),
                    RestFlexion = ToRadians(0),
                    RestAbduction = ToRadians(0)
                },
                new FingerJoint
                {
                    Type = JointType.Mcp,
                    BoneLength = mcpLength,
                    MinFlexion = ToRadians(0),
                    MaxFlexion = ToRadians(90),
                    MinAbduction = ToRadians(-20),
                    MaxAbduction = ToRadians(20),
                    RestFlexion = ToRadians(10),
                    RestAbduction = ToRadians(0)
                },
                new FingerJoint
                {
                    Type = JointType.Pip,
                    BoneLength = pipLength,
                    MinFlexion = ToRadians(0),
                    MaxFlexion = ToRadians(110),
                    MinAbduction = ToRadians(0),
                    MaxAbduction = ToRadians(0),
                    RestFlexion = ToRadians(20),
                    RestAbduction = ToRadians(0)
                },
                new FingerJoint
                {
                    Type = JointType.Dip,
                    BoneLength = dipLength,
                    MinFlexion = ToRadians(0),
                    MaxFlexion = ToRadians(90),
                    MinAbduction = ToRadians(0),
                    MaxAbduction = ToRadians(0),
                    RestFlexion = ToRadians(15),
                    RestAbduction = ToRadians(0)
                }
            )
        };
    }

    /// <summary>
    ///     Create scaled hand model for different hand sizes
    /// </summary>
    public static HandModel CreateScaled(float scaleFactor)
    {
        var standard = CreateStandardAdult();

        return standard with
        {
            PalmWidth = standard.PalmWidth * scaleFactor,
            PalmLength = standard.PalmLength * scaleFactor,
            Fingers = [.. standard.Fingers.Select(f => f with
            {
                BasePosition = f.BasePosition * scaleFactor,
                Joints = [.. f.Joints.Select(j => j with
                {
                    BoneLength = j.BoneLength * scaleFactor
                })]
            })],
            FingerSpreadConstraints = [.. standard.FingerSpreadConstraints
                .Select(c => c with
                {
                    PreferredSeparationMm = c.PreferredSeparationMm * scaleFactor,
                    MaxSeparationMm = c.MaxSeparationMm * scaleFactor,
                    MinSeparationMm = c.MinSeparationMm * scaleFactor
                })]
        };
    }

    /// <summary>
    ///     Get finger by type
    /// </summary>
    public Finger GetFinger(FingerType type)
    {
        return Fingers[(int)type];
    }

    private static ImmutableList<FingerSpreadConstraint> CreateStandardFingerSpreadConstraints()
    {
        return ImmutableList.Create(
            new FingerSpreadConstraint
            {
                Primary = FingerType.Index,
                Secondary = FingerType.Middle,
                PreferredSeparationMm = 18f,
                MaxSeparationMm = 28f,
                MinSeparationMm = 10f
            },
            new FingerSpreadConstraint
            {
                Primary = FingerType.Middle,
                Secondary = FingerType.Ring,
                PreferredSeparationMm = 20f,
                MaxSeparationMm = 30f,
                MinSeparationMm = 12f
            },
            new FingerSpreadConstraint
            {
                Primary = FingerType.Ring,
                Secondary = FingerType.Little,
                PreferredSeparationMm = 22f,
                MaxSeparationMm = 34f,
                MinSeparationMm = 12f
            }
        );
    }

    /// <summary>
    ///     Convert degrees to radians
    /// </summary>
    private static float ToRadians(float degrees)
    {
        return degrees * MathF.PI / 180.0f;
    }
}

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

/// <summary>
///     Factory for creating personalized hand models based on hand size
/// </summary>
public static class PersonalizedHandModel
{
    /// <summary>
    ///     Create a hand model scaled for the specified hand size
    /// </summary>
    public static HandModel Create(HandSize size)
    {
        var baseModel = HandModel.CreateStandardAdult();

        return size switch
        {
            HandSize.Small => ScaleHandModel(baseModel, 0.85f, "Small Adult Hand"),
            HandSize.Medium => baseModel,
            HandSize.Large => ScaleHandModel(baseModel, 1.15f, "Large Adult Hand"),
            HandSize.ExtraLarge => ScaleHandModel(baseModel, 1.30f, "Extra Large Adult Hand"),
            _ => baseModel
        };
    }

    /// <summary>
    ///     Get the scale factor for a hand size
    /// </summary>
    public static float GetScaleFactor(HandSize size)
    {
        return size switch
        {
            HandSize.Small => 0.85f,
            HandSize.Medium => 1.00f,
            HandSize.Large => 1.15f,
            HandSize.ExtraLarge => 1.30f,
            _ => 1.00f
        };
    }

    /// <summary>
    ///     Get typical hand span in millimeters for a hand size
    /// </summary>
    public static float GetTypicalHandSpanMm(HandSize size)
    {
        return size switch
        {
            HandSize.Small => 190.0f, // ~7.5 inches
            HandSize.Medium => 215.0f, // ~8.5 inches
            HandSize.Large => 240.0f, // ~9.5 inches
            HandSize.ExtraLarge => 265.0f, // ~10.5 inches
            _ => 215.0f
        };
    }

    /// <summary>
    ///     Scale a hand model by the specified factor
    /// </summary>
    private static HandModel ScaleHandModel(HandModel baseModel, float scaleFactor, string name)
    {
        var scaledFingers = baseModel.Fingers.Select(finger => new Finger
        {
            Type = finger.Type,
            BasePosition = finger.BasePosition * scaleFactor,
            Joints = [.. finger.Joints.Select(joint => new FingerJoint
            {
                Type = joint.Type,
                BoneLength = joint.BoneLength * scaleFactor,
                // Joint angles remain the same - biomechanical limits don't change with size
                MinFlexion = joint.MinFlexion,
                MaxFlexion = joint.MaxFlexion,
                MinAbduction = joint.MinAbduction,
                MaxAbduction = joint.MaxAbduction,
                RestFlexion = joint.RestFlexion,
                RestAbduction = joint.RestAbduction
            })]
        }).ToImmutableList();

        return new()
        {
            PalmWidth = baseModel.PalmWidth * scaleFactor,
            PalmLength = baseModel.PalmLength * scaleFactor,
            Fingers = scaledFingers
        };
    }

    /// <summary>
    ///     Determine recommended hand size based on hand span measurement
    /// </summary>
    public static HandSize DetermineHandSize(float handSpanMm)
    {
        return handSpanMm switch
        {
            < 200.0f => HandSize.Small,
            < 230.0f => HandSize.Medium,
            < 255.0f => HandSize.Large,
            _ => HandSize.ExtraLarge
        };
    }

    /// <summary>
    ///     Get difficulty adjustment factor based on hand size and chord characteristics
    /// </summary>
    public static double GetDifficultyAdjustment(HandSize handSize, int fretSpan, int stringSpan)
    {
        // Smaller hands have more difficulty with wide stretches
        var baseAdjustment = handSize switch
        {
            HandSize.Small => 1.20, // 20% harder
            HandSize.Medium => 1.00, // Baseline
            HandSize.Large => 0.90, // 10% easier
            HandSize.ExtraLarge => 0.80, // 20% easier
            _ => 1.00
        };

        // Additional penalty for wide stretches with small hands
        if (handSize == HandSize.Small && fretSpan >= 4)
        {
            baseAdjustment *= 1.15; // Extra 15% penalty for wide stretches
        }

        // Large hands have slight advantage on wide string spans
        if (handSize >= HandSize.Large && stringSpan >= 5)
        {
            baseAdjustment *= 0.95; // 5% easier for wide string spans
        }

        return baseAdjustment;
    }
}

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

/// <summary>
///     Position and orientation of a fingertip
/// </summary>
public sealed record FingertipPosition
{
    /// <summary>
    ///     3D position of the fingertip
    /// </summary>
    public required Vector3 Position { get; init; }

    /// <summary>
    ///     Normal vector at the fingertip
    /// </summary>
    public required Vector3 Normal { get; init; }

    /// <summary>
    ///     Pressure applied by the fingertip (0-1)
    /// </summary>
    public float Pressure { get; init; } = 0.5f;
}

/// <summary>
///     Inverse kinematics solution
/// </summary>
public sealed record IkSolution
{
    /// <summary>
    ///     The solved hand pose
    /// </summary>
    public required HandPose Pose { get; init; }

    /// <summary>
    ///     Number of iterations to converge
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    ///     Time taken to solve
    /// </summary>
    public TimeSpan SolveTime { get; init; }

    /// <summary>
    ///     Final error value
    /// </summary>
    public double FinalError { get; init; }

    /// <summary>
    ///     Whether the solution converged
    /// </summary>
    public bool Converged { get; init; }

    /// <summary>
    ///     Get convergence rate
    /// </summary>
    public double GetConvergenceRate()
    {
        return Iterations > 0 ? 1.0 / Iterations : 1.0;
    }
}
