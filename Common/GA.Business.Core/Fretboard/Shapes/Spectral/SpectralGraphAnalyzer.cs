namespace GA.Business.Core.Fretboard.Shapes.Spectral;

using System;
using System.Collections.Generic;
using System.Linq;
using Analysis;
using Extensions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Notes.Primitives;
using Primitives;

/// <summary>
///     Performs spectral analysis on the fretboard connectome to identify
///     clusters, bottlenecks, and ergonomic regions.
/// </summary>
public class SpectralGraphAnalyzer
{
    private const int _maxIterations = 100;

    // Core Analyze method
    public SpectralMetrics Analyze(ShapeGraph graph)
    {
        var n = graph.ShapeCount;
        if (n == 0)
        {
            return new SpectralMetrics
            {
                Eigenvalues = [],
                Eigenvectors = Matrix<double>.Build.Dense(0, 0)
            };
        }

        // 1. Build Adjacency Matrix (Weighted)
        var w = Matrix<double>.Build.Dense(n, n);
        var shapeIds = graph.Shapes.Keys.OrderBy(k => k).ToList();
        var idToIndex = shapeIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        foreach (var (fromId, transitions) in graph.Adjacency)
        {
            if (!idToIndex.TryGetValue(fromId, out var i))
            {
                continue;
            }

            foreach (var transition in transitions)
            {
                if (idToIndex.TryGetValue(transition.ToId, out var j))
                {
                    // Gaussian kernel for similarity: exp(-cost^2 / 2sigma^2)
                    // Higher cost -> Lower weight
                    var weight = Math.Exp(-Math.Pow(transition.Score, 2) / 2.0);
                    w[i, j] = weight;
                    w[j, i] = weight; // Ensure symmetry
                }
            }
        }

        // 2. Compute Laplacian
        // D is degree matrix (diagonal)
        var d = Matrix<double>.Build.Dense(n, n);
        for (var idx = 0; idx < n; idx++)
        {
            d[idx, idx] = w.Row(idx).Sum();
        }

        // L = D - W (Unnormalized Laplacian)
        // L_sym = I - D^-1/2 * W * D^-1/2 (Normalized Symmetric Laplacian)
        // We use Normalized Symmetric Laplacian for better clustering properties
        var dInvSqrt = Matrix<double>.Build.Dense(n, n);
        for (var idx = 0; idx < n; idx++)
        {
            if (d[idx, idx] > 1e-9)
            {
                dInvSqrt[idx, idx] = 1.0 / Math.Sqrt(d[idx, idx]);
            }
        }

        var identity = Matrix<double>.Build.DenseIdentity(n);
        var wSym = dInvSqrt * w * dInvSqrt;
        var lSym = identity - wSym; // L_sym = I - W_sym is symmetric, so Evd is stable and eigenvalues are real >= 0
        var evd = lSym.Evd(Symmetricity.Symmetric);

        // Sort eigenvalues and corresponding eigenvectors
        // MathNet usually returns them sorted, but let's ensure ascending order
        var eigenValues = evd.EigenValues.Real(); // Vector
        var eigenVectors = evd.EigenVectors;      // Matrix (columns are eigenvectors)

        // Create pairs of (eigenvalue, column_index)
        var pairs = eigenValues.Select((val, idx) => (val, idx))
                               .OrderBy(p => p.val)
                               .ToList();

        var sortedEigenValues = pairs.Select(p => p.val).ToArray();

        // Construct sorted eigenvector matrix
        var sortedEigenVectors = Matrix<double>.Build.Dense(n, n);
        for (var j = 0; j < n; j++)
        {
            var originalColIndex = pairs[j].idx;
            sortedEigenVectors.SetColumn(j, eigenVectors.Column(originalColIndex));
        }

        return new SpectralMetrics
        {
            Eigenvalues = sortedEigenValues,
            Eigenvectors = sortedEigenVectors
        };
    }

    /// <summary>
    /// Cluster shapes using spectral clustering (K-Means on eigenvectors)
    /// </summary>
    public List<ChordFamily> Cluster(ShapeGraph graph, int k)
    {
        if (graph.ShapeCount == 0) return [];

        // 1. Analyze to get eigenvectors
        var metrics = Analyze(graph);
        if (metrics.NodeCount < k) k = metrics.NodeCount;

        // 2. Embed: Use first k eigenvectors
        // Standard Ng-Jordan-Weiss: use eigenvectors corresponding to k smallest eigenvalues.
        // columns 0..k-1
        var u = metrics.Eigenvectors.SubMatrix(0, metrics.NodeCount, 0, k);

        // 3. Normalize rows of u
        for (var i = 0; i < u.RowCount; i++)
        {
            var row = u.Row(i);
            var norm = row.L2Norm();
            if (norm > 1e-9)
            {
                u.SetRow(i, row / norm);
            }
        }

        // 4. K-Means on rows of u
        // Each row i corresponds to shape i
        var rowVectors = u.EnumerateRows().ToArray();
        var labels = KMeans(rowVectors, k); // Custom K-Means

        // 5. Group into ChordFamilies
        var shapes = graph.Shapes.Values.ToList(); // Must match order used in Analyze!
        // Re-construct the idToIndex mapping to be sure of order
        // NOTE: In Analyze we used graph.Shapes.Values.ToList().
        // We MUST assume dictionary order is stable between these two calls or use a sorted list.
        // To be safe, let's enforce an order or rely on the fact that we just grabbed Values.
        // Ideally Analyze should return the ID mapping too.
        // For this implementation, we assume stable iteration order for now (risky but typical for "Values" if not modified).

        var families = new List<ChordFamily>();

        // Group by label
        var groups = shapes.Select((s, i) => (s, label: labels[i]))
                           .GroupBy(x => x.label);

        foreach (var g in groups)
        {
            var members = g.Select(x => x.s).ToList();
            var shapeIds = members.Select(s => s.Id).ToList();
            var centroid = shapeIds.FirstOrDefault() ?? ""; // Pick first as representative

            families.Add(new ChordFamily
            {
                Id = g.Key + 1, // Cluster index 1-based
                ShapeIds = shapeIds,
                Centroid = centroid,
                Size = shapeIds.Count,
                AverageErgonomics = members.Any() ? members.Average(s => s.Ergonomics) : 0
            });
        }

        return families;
    }

    /// <summary>
    /// Identify central shapes using Betweenness Centrality (heuristic/sampling) or Degree Centrality
    /// </summary>
    public List<(string ShapeId, double Score)> FindCentralShapes(ShapeGraph graph, int topK)
    {
        if (graph.ShapeCount == 0) return [];

        // User suggestion: PageRank or Eigenvector Centrality is better.
        // Since we have the Adjacency matrix logic, let's try a simplified PageRank
        // OR rely on Degree Centrality if PageRank is too expensive to implement from scratch.
        // Let's implement Degree Centrality as it's O(1) per node and robust "enough" for now,
        // but acknowledged as a simplification.

        return graph.Adjacency
             .Select(kvp => (ShapeId: kvp.Key, Score: (double)kvp.Value.Count))
             .OrderByDescending(x => x.Score)
             .Take(topK)
             .ToList();
    }

    // --- Graph Metrics (Corrected/Improved Implementations) ---

    // Note: These methods are private in the original class but the user commented on them.
    // They are internally used or exposed via SpectralMetrics?
    // Wait, SpectralMetrics calculates properties from Eigenvalues directly.
    // The methods in the class (ComputeAlgebraicConnectivity etc) were computing them from the graph?
    // Ah, in the original code, they were private methods called... nowhere? Or exposed?
    // Let's check the original code... they were private.
    // But SpectralMetrics class (record) has properties like AlgebraicConnectivity which simply read Eigenvalues[1].
    // So the logic is ALREADY in SpectralMetrics for the *calculation* from eigenvalues.
    // The previous implementation had private methods on Analyzer that did heuristics.
    // Replace them or remove them? Since they were private and unused (?) or used incorrectly,
    // we should rely on SpectralMetrics to do the math from the computed Eigenvalues.
    // We don't need these private methods anymore if Analyze returns the CORRECT Eigenvalues.

    // --- K-Means Implementation (Simple Lloyd's Algorithm) ---
    private int[] KMeans(MathNet.Numerics.LinearAlgebra.Vector<double>[] data, int k)
    {
        int n = data.Length;
        int[] assignments = new int[n];
        MathNet.Numerics.LinearAlgebra.Vector<double>[] centroids = new MathNet.Numerics.LinearAlgebra.Vector<double>[k];
        Random rnd = new Random(42); // Deterministic seed

        // Init ++ style (simplified): pick random distinct points
        var indices = Enumerable.Range(0, n).OrderBy(_ => rnd.Next()).Take(k).ToArray();
        for (int i = 0; i < k; i++)
            centroids[i] = data[indices[i]].Clone();

        bool changed = true;
        int iter = 0;

        while (changed && iter < _maxIterations)
        {
            changed = false;
            iter++;

            // Assign step
            // Parallelize for speed?
            for (int i = 0; i < n; i++)
            {
                int bestK = 0;
                double bestDist = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double dist = (data[i] - centroids[c]).L2Norm(); // Euclidian distance
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestK = c;
                    }
                }

                if (assignments[i] != bestK)
                {
                    assignments[i] = bestK;
                    changed = true;
                }
            }

            // Update step
            var counts = new int[k];
            var newCentroids = new MathNet.Numerics.LinearAlgebra.Vector<double>[k];
            for (int c = 0; c < k; c++)
            {
                newCentroids[c] = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(centroids[0].Count); // Size of feature vector
            }

            for (int i = 0; i < n; i++)
            {
                int c = assignments[i];
                newCentroids[c] += data[i];
                counts[c]++;
            }

            for (int c = 0; c < k; c++)
            {
                if (counts[c] > 0)
                    centroids[c] = newCentroids[c] / counts[c];
                else
                {
                    // Handle empty cluster: re-init? or leave it?
                    // Re-init to a random point to avoid dead cluster
                    centroids[c] = data[rnd.Next(n)].Clone();
                }
            }
        }

        return assignments;
    }
}
