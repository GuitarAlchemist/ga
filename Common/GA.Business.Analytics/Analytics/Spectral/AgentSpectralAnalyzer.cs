namespace GA.Business.Core.Analytics.Spectral;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.Extensions.Logging;

/// <summary>
///     Computes spectral metrics over agent interaction graphs.
///     Ported from the TARS multi-agent analytics pipeline.
/// </summary>
public sealed class AgentSpectralAnalyzer(ILogger<AgentSpectralAnalyzer> logger)
{
    public AgentSpectralMetrics Analyze(AgentInteractionGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.Agents.Count == 0)
        {
            throw new ArgumentException("Graph must contain at least one agent", nameof(graph));
        }

        var nodeIndex = graph.Agents
            .Select((node, index) => (node, index))
            .ToDictionary(tuple => tuple.node.Id, tuple => tuple.index, StringComparer.OrdinalIgnoreCase);

        var size = graph.Agents.Count;
        var adjacency = DenseMatrix.Create(size, size, 0.0);

        foreach (var edge in graph.Edges)
        {
            if (!nodeIndex.TryGetValue(edge.Source, out var i) || !nodeIndex.TryGetValue(edge.Target, out var j))
            {
                continue;
            }

            adjacency[i, j] += Math.Max(edge.Weight, 0.0);
            if (graph.IsUndirected && i != j)
            {
                adjacency[j, i] += Math.Max(edge.Weight, 0.0);
            }
        }

        // Degree matrix
        var degrees = DenseVector.Create(size, idx => adjacency.Row(idx).Sum());
        var laplacian = DenseMatrix.Create(size, size, (i, j) => i == j ? degrees[i] : -adjacency[i, j]);

        // Eigen decomposition of Laplacian (symmetric)
        var evd = laplacian.Evd(Symmetricity.Symmetric);
        var eigenValues = evd.EigenValues.Real().ToArray();
        Array.Sort(eigenValues);

        var algebraicConnectivity = eigenValues.Length > 1 ? eigenValues[1] : 0.0;
        var spectralGap = eigenValues.Length > 2 ? eigenValues[2] - eigenValues[1] : (double?)null;

        // Adjacency spectral radius and principal eigenvector for centrality
        var adjacencyEvd = adjacency.Evd(Symmetricity.Symmetric);
        var adjacencyEigenValues = adjacencyEvd.EigenValues.Real().ToArray();
        var spectralRadius = adjacencyEigenValues.Max();

        var dominantIndex = Array.IndexOf(adjacencyEigenValues, spectralRadius);
        var principalEigenvector = adjacencyEvd.EigenVectors.Column(dominantIndex).PointwiseAbs();

        // Normalise centrality scores
        var sumCentrality = principalEigenvector.Sum();
        var centrality = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var idx = 0; idx < size; idx++)
        {
            var value = principalEigenvector[idx] / (sumCentrality > 1e-9 ? sumCentrality : 1.0);
            centrality[graph.Agents[idx].Id] = value;
        }

        var metrics = new AgentSpectralMetrics
        {
            Eigenvalues = eigenValues,
            AlgebraicConnectivity = algebraicConnectivity,
            SpectralGap = spectralGap,
            SpectralRadius = spectralRadius,
            DegreeDistribution = degrees.ToArray(),
            Centrality = centrality
        };

        logger.LogInformation(
            "Computed spectral metrics: λ2={AlgebraicConnectivity:F4}, radius={SpectralRadius:F4}, gap={SpectralGap}",
            metrics.AlgebraicConnectivity,
            metrics.SpectralRadius,
            metrics.SpectralGap?.ToString("F4") ?? "n/a");

        return metrics;
    }
}
