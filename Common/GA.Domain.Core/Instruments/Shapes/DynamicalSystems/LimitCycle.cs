namespace GA.Domain.Core.Instruments.Shapes.DynamicalSystems;

/// <summary>
/// A limit cycle in the dynamical system
/// </summary>
public record LimitCycle
{
    public required List<string> ShapeIds { get; init; }
    public required int Period { get; init; }
    public required double Stability { get; init; }
}