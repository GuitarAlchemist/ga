namespace GA.Domain.Core.Instruments.Biomechanics;

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
    public static WristConstraint Default { get; } = new(DegreesToRadians(-35f), DegreesToRadians(45f),
        DegreesToRadians(-25f), DegreesToRadians(25f), DegreesToRadians(-30f), DegreesToRadians(30f));

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;
}
