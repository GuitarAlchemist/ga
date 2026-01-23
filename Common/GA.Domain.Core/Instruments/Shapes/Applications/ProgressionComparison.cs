namespace GA.Domain.Core.Instruments.Shapes.Applications;

/// <summary>
/// Comparison of two progressions
/// </summary>
public record ProgressionComparison
{
    public double Similarity { get; init; }
    public double WassersteinDistance { get; init; }
    public ProgressionInfo Info1 { get; init; } = null!;
    public ProgressionInfo Info2 { get; init; } = null!;
}