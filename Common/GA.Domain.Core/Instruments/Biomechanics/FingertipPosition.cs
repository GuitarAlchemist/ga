namespace GA.Domain.Core.Instruments.Biomechanics;

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
