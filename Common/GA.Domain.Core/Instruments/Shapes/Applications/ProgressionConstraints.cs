namespace GA.Domain.Core.Instruments.Shapes.Applications;

/// <summary>
/// Constraints for progression optimization
/// </summary>
public record ProgressionConstraints
{
    public int TargetLength { get; init; } = 8;
    public double MinErgonomics { get; init; } = 0.3;
    public OptimizationStrategy Strategy { get; init; } = OptimizationStrategy.BalancedPractice;
    public bool PreferCentralShapes { get; init; } = true;
    public bool AllowRandomness { get; init; } = false;
    public string? StartShapeId { get; init; }
}