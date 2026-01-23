namespace GA.Domain.Core.Instruments.Shapes.DynamicalSystems;

/// <summary>
/// Information about the dynamical system
/// </summary>
public record DynamicalSystemInfo
{
    public required List<Attractor> Attractors { get; init; }
    public required List<string> FixedPoints { get; init; }
    public required List<LimitCycle> LimitCycles { get; init; }
    public required double LyapunovExponent { get; init; }
    public required bool IsChaotic { get; init; }
}