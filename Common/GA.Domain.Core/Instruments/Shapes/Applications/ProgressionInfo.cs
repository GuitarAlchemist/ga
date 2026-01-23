namespace GA.Domain.Core.Instruments.Shapes.Applications;

/// <summary>
/// Information about a progression
/// </summary>
public record ProgressionInfo
{
    public required double Entropy { get; init; }
    public required double Perplexity { get; init; }
    public required double Complexity { get; init; }
    public required double Predictability { get; init; }
}