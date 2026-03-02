namespace GA.Domain.Services.Fretboard.Shapes.Spectral;

using System.Collections.Immutable;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Analyzes fretboard shape graphs using spectral graph theory.
///     Provides centrality, clustering, and connectivity metrics.
/// </summary>
public class SpectralGraphAnalyzer(ILogger<SpectralGraphAnalyzer> logger)
{
    public SpectralGraphAnalyzer() : this(NullLogger<SpectralGraphAnalyzer>.Instance) { }

    /// <summary>
    ///     Computes Laplacian eigenvalues and connectivity metrics for the graph.
    /// </summary>
    public SpectralMetrics Analyze(ShapeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var size = graph.ShapeCount;
        if (size == 0) return new SpectralMetrics { NodeCount = 0, Eigenvalues = [] };

        logger.LogDebug("Analyzing shape graph with {NodeCount} nodes", size);

        var laplacian = ComputeLaplacian(graph);
        var evd = laplacian.Evd(Symmetricity.Symmetric);
        var eigenvalues = evd.EigenValues.Real().ToArray();
        Array.Sort(eigenvalues);

        return new SpectralMetrics
        {
            NodeCount = size,
            Eigenvalues = eigenvalues
        };
    }

    /// <summary>
    ///     Finds central shapes in the graph based on eigenvector centrality.
    /// </summary>
    public List<(string Id, double Score)> FindCentralShapes(ShapeGraph graph, int topK = 10)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var size = graph.ShapeCount;
        if (size == 0) return [];

        var adjacency = ComputeWeightMatrix(graph);
        var evd = adjacency.Evd(Symmetricity.Symmetric);
        var eigenvalues = evd.EigenValues.Real().ToArray();

        // Use the absolute value to find the dominant eigenvalue/eigenvector
        var dominantValue = 0.0;
        var dominantIndex = 0;
        for (int i = 0; i < eigenvalues.Length; i++)
        {
            if (Math.Abs(eigenvalues[i]) > dominantValue)
            {
                dominantValue = Math.Abs(eigenvalues[i]);
                dominantIndex = i;
            }
        }

        var principalEigenvector = evd.EigenVectors.Column(dominantIndex).PointwiseAbs();
        var shapeIds = graph.Shapes.Keys.ToList();

        var results = new List<(string Id, double Score)>();
        for (int i = 0; i < size; i++)
        {
            results.Add((shapeIds[i], principalEigenvector[i]));
        }

        return [.. results.OrderByDescending(r => r.Score).Take(topK)];
    }

    /// <summary>
    ///     Clusters shapes into k families using spectral clustering (K-means on the Laplacian eigenvectors).
    /// </summary>
    public List<ShapeFamily> Cluster(ShapeGraph graph, int k)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var size = graph.ShapeCount;
        if (size == 0 || k <= 0) return [];
        if (k >= size) k = size;

        logger.LogInformation("Clustering {NodeCount} shapes into {K} families", size, k);

        var laplacian = ComputeLaplacian(graph);
        var evd = laplacian.Evd(Symmetricity.Symmetric);
        var eigenvalues = evd.EigenValues.Real().ToArray();

        // Pick k smallest eigenvectors (excluding λ1 = 0 if k > 1)
        var indices = Enumerable.Range(0, eigenvalues.Length)
            .OrderBy(i => Math.Abs(eigenvalues[i]))
            .Take(k)
            .ToArray();

        var u = Matrix<double>.Build.DenseOfColumnVectors(indices.Select(i => evd.EigenVectors.Column(i)));

        // Normalize rows for spectral clustering stability
        for (int i = 0; i < size; i++)
        {
            var row = u.Row(i);
            var norm = row.Norm(2);
            if (norm > 1e-9) u.SetRow(i, row / norm);
        }

        var labels = SimpleKMeans(u, k);
        var shapeIds = graph.Shapes.Keys.ToList();
        var families = new List<ShapeFamily>();

        for (int i = 0; i < k; i++)
        {
            var clusterShapeIds = shapeIds.Where((_, idx) => labels[idx] == i).ToImmutableList();
            if (clusterShapeIds.Count == 0) continue;

            var avgErgo = clusterShapeIds.Average(id => graph.Shapes[id].Ergonomics);
            families.Add(new ShapeFamily
            {
                ClusterId = i,
                ShapeIds = clusterShapeIds,
                AverageErgonomics = avgErgo
            });
        }

        return families;
    }

    private Matrix<double> ComputeLaplacian(ShapeGraph graph)
    {
        var size = graph.ShapeCount;
        var adjacency = ComputeWeightMatrix(graph);
        var degrees = Vector<double>.Build.Dense(size, i => adjacency.Row(i).Sum());
        return Matrix<double>.Build.Dense(size, size, (i, j) => i == j ? degrees[i] : -adjacency[i, j]);
    }

    private Matrix<double> ComputeWeightMatrix(ShapeGraph graph)
    {
        var size = graph.ShapeCount;
        var matrix = Matrix<double>.Build.Dense(size, size, 0.0);
        var shapeIds = graph.Shapes.Keys.ToList();
        var idToIndex = shapeIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);

        foreach (var entry in graph.Adjacency)
        {
            if (!idToIndex.TryGetValue(entry.Key, out var fromIdx)) continue;

            foreach (var transition in entry.Value)
            {
                if (!idToIndex.TryGetValue(transition.ToId, out var toIdx)) continue;

                // Use the transition weight (inverse of cost)
                var weight = transition.Weight;
                matrix[fromIdx, toIdx] += weight;
                if (fromIdx != toIdx) matrix[toIdx, fromIdx] += weight; // Undirected harmonic graph
            }
        }
        return matrix;
    }

    private int[] SimpleKMeans(Matrix<double> data, int k)
    {
        int n = data.RowCount;
        int d = data.ColumnCount;

        // Seed centroids with first k distinct points
        var centroids = Matrix<double>.Build.Dense(k, d, (i, j) => data[i, j]);
        var labels = new int[n];
        var changed = true;
        var maxIter = 100;

        while (changed && maxIter-- > 0)
        {
            changed = false;
            // Assignment phase
            for (int i = 0; i < n; i++)
            {
                var row = data.Row(i);
                var bestCluster = 0;
                var minDist = double.MaxValue;

                for (int j = 0; j < k; j++)
                {
                    var dist = (row - centroids.Row(j)).Norm(2);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestCluster = j;
                    }
                }

                if (labels[i] != bestCluster)
                {
                    labels[i] = bestCluster;
                    changed = true;
                }
            }

            // Update phase
            for (int j = 0; j < k; j++)
            {
                var clusterIndices = Enumerable.Range(0, n).Where(i => labels[i] == j).ToList();
                if (clusterIndices.Count > 0)
                {
                    var newCentroid = Vector<double>.Build.Dense(d, 0.0);
                    foreach (var idx in clusterIndices)
                    {
                        newCentroid += data.Row(idx);
                    }
                    centroids.SetRow(j, newCentroid / clusterIndices.Count);
                }
            }
        }

        return labels;
    }
}
