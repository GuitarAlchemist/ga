namespace GA.Domain.Core.Instruments.Shapes.DynamicalSystems;

/// <summary>
/// An attractor in the dynamical system
/// </summary>
public record Attractor
{
    public required string ShapeId { get; init; }
    public required double Strength { get; init; }
    public required string Type { get; init; }
}