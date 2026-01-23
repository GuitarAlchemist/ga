namespace GA.Domain.Core.Instruments.Shapes.Applications;

/// <summary>
/// Result of progression optimization
/// </summary>
public record OptimizedProgression
{
    public required List<FretboardShape> Shapes { get; init; }
    public required double Score { get; init; }
    public required double Quality { get; init; }
        
    public List<string> ShapeIds => Shapes.Select(s => s.Id).ToList();
    public double Entropy { get; init; } = 0.5; // Placeholder
    public double Complexity { get; init; } = 0.5; // Placeholder
    public double Predictability { get; init; } = 0.5; // Placeholder
    public double Diversity { get; init; } = 0.5; // Placeholder
}