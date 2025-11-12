namespace GA.Business.Analytics.Analytics.Spectral;

using JetBrains.Annotations;

/// <summary>
///     Summary of spectral graph characteristics derived from agent interactions.
/// </summary>
[PublicAPI]
public sealed record AgentSpectralMetrics
{
    public required double[] Eigenvalues { get; init; }
    public required double AlgebraicConnectivity { get; init; }
    public double? SpectralGap { get; init; }
    public required double SpectralRadius { get; init; }
    public required double[] DegreeDistribution { get; init; }
    public required IReadOnlyDictionary<string, double> Centrality { get; init; }
}
