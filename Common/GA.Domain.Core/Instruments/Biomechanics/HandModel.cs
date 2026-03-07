// Note: This module models physical hand/finger biomechanics for ergonomic analysis.
// It is a pragmatic inclusion in GA.Domain.Core for proximity to instrument types,
// but is not a music-theory primitive. Candidate for future move to GA.Domain.Services.
namespace GA.Domain.Core.Instruments.Biomechanics;

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
    public static HandModel CreateStandardAdult() =>
        new()
        {
            PalmWidth = 85.0f,
            PalmLength = 100.0f,
            Fingers =
            [
                new()
                {
                    Type = FingerType.Thumb,
                    BasePosition = new(-30, 0, 0), // Left side of palm
                    Joints =
                    [
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
                    ]
                },
                // Index finger (4 joints: CMC, MCP, PIP, DIP)
                CreateStandardFinger(FingerType.Index, new(-15, 100, 0), 40, 25, 10),

                // Middle finger (4 joints: CMC, MCP, PIP, DIP)
                CreateStandardFinger(FingerType.Middle, new(0, 100, 0), 45, 30, 10),

                // Ring finger (4 joints: CMC, MCP, PIP, DIP)
                CreateStandardFinger(FingerType.Ring, new(15, 100, 0), 42, 28, 10),

                // Little finger (4 joints: CMC, MCP, PIP, DIP)
                CreateStandardFinger(FingerType.Little, new(30, 100, 0), 35, 20, 10)
            ],
            FingerSpreadConstraints = CreateStandardFingerSpreadConstraints(),
            WristLimits = WristConstraint.Default
        };

    /// <summary>
    ///     Create a standard finger (index, middle, ring, little)
    /// </summary>
    private static Finger CreateStandardFinger(
        FingerType type,
        Vector3 basePosition,
        float mcpLength,
        float pipLength,
        float dipLength) =>
        new()
        {
            Type = type,
            BasePosition = basePosition,
            Joints =
            [
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
            ]
        };

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
            Fingers =
            [
                .. standard.Fingers.Select(f => f with
                {
                    BasePosition = f.BasePosition * scaleFactor,
                    Joints =
                    [
                        .. f.Joints.Select(j => j with
                        {
                            BoneLength = j.BoneLength * scaleFactor
                        })
                    ]
                })
            ],
            FingerSpreadConstraints =
            [
                .. standard.FingerSpreadConstraints
                    .Select(c => c with
                    {
                        PreferredSeparationMm = c.PreferredSeparationMm * scaleFactor,
                        MaxSeparationMm = c.MaxSeparationMm * scaleFactor,
                        MinSeparationMm = c.MinSeparationMm * scaleFactor
                    })
            ]
        };
    }

    /// <summary>
    ///     Get finger by type
    /// </summary>
    public Finger GetFinger(FingerType type) => Fingers[(int)type];

    private static ImmutableList<FingerSpreadConstraint> CreateStandardFingerSpreadConstraints() =>
    [
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
    ];

    /// <summary>
    ///     Convert degrees to radians
    /// </summary>
    private static float ToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
