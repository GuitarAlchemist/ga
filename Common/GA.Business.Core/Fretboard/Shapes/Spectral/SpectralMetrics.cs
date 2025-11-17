namespace GA.Business.Core.Fretboard.Shapes.Spectral;

using MathNet.Numerics.LinearAlgebra;

/// <summary>
///     Spectral metrics computed from the graph Laplacian
/// </summary>
/// <remarks>
///     Spectral graph theory analyzes graphs using eigenvalues and eigenvectors of matrices
///     associated with the graph (adjacency matrix, Laplacian matrix, etc.)
///     Key concepts:
///     - Eigenvalues reveal structural properties (connectivity, clustering, etc.)
///     - Algebraic connectivity (?2) measures how well-connected the graph is
///     - Spectral gap (?2 - ?1) indicates clustering tendency
///     - Eigenvectors can be used for graph partitioning and clustering
///     References:
///     - Chung, F. R. K. (1997). Spectral Graph Theory. American Mathematical Society.
///     - Von Luxburg, U. (2007). A tutorial on spectral clustering. Statistics and Computing.
/// </remarks>
[PublicAPI]
public sealed record SpectralMetrics
{
    /// <summary>
    ///     All eigenvalues of the Laplacian matrix (sorted ascending)
    /// </summary>
    public required double[] Eigenvalues { get; init; }

    /// <summary>
    ///     Corresponding eigenvectors (columns are eigenvectors)
    /// </summary>
    public required Matrix<double> Eigenvectors { get; init; }

    /// <summary>
    ///     Number of nodes in the graph
    /// </summary>
    public int NodeCount => Eigenvalues.Length;

    /// <summary>
    ///     Smallest eigenvalue (should be ~0 for connected graphs)
    ///     ?1 = 0 indicates the graph is connected
    /// </summary>
    public double Lambda1 => Eigenvalues.Length > 0 ? Eigenvalues[0] : 0;

    /// <summary>
    ///     Second smallest eigenvalue (algebraic connectivity / Fiedler value)
    ///     Measures how well-connected the graph is
    ///     - ?2 = 0: Graph is disconnected
    ///     - ?2 > 0: Graph is connected (higher = more connected)
    /// </summary>
    public double AlgebraicConnectivity => Eigenvalues.Length > 1 ? Eigenvalues[1] : 0;

    /// <summary>
    ///     Fiedler vector (eigenvector corresponding to ?2)
    ///     Used for graph partitioning - sign of components indicates partition
    /// </summary>
    public Vector<double>? FiedlerVector => Eigenvalues.Length > 1 ? Eigenvectors.Column(1) : null;

    /// <summary>
    ///     Spectral gap (?2 - ?1)
    ///     Larger gap indicates stronger clustering tendency
    /// </summary>
    public double SpectralGap => AlgebraicConnectivity - Lambda1;

    /// <summary>
    ///     Largest eigenvalue
    ///     Related to graph diameter and expansion properties
    /// </summary>
    public double LambdaMax => Eigenvalues.Length > 0 ? Eigenvalues[^1] : 0;

    /// <summary>
    ///     Number of connected components (approximately)
    ///     Equal to the number of eigenvalues � 0 (within tolerance)
    /// </summary>
    public int EstimatedComponentCount
    {
        get
        {
            const double tolerance = 1e-6;
            return Eigenvalues.Count(ev => Math.Abs(ev) < tolerance);
        }
    }

    /// <summary>
    ///     Is the graph connected?
    ///     True if there's only one eigenvalue � 0
    /// </summary>
    public bool IsConnected => EstimatedComponentCount == 1;

    /// <summary>
    ///     Cheeger constant (lower bound)
    ///     Measures bottleneck in the graph
    ///     h = ?2 / 2
    /// </summary>
    public double CheegerLowerBound => AlgebraicConnectivity / 2.0;

    /// <summary>
    ///     Effective resistance (sum of reciprocals of non-zero eigenvalues)
    ///     Measures overall connectivity
    /// </summary>
    public double EffectiveResistance
    {
        get
        {
            const double tolerance = 1e-6;
            return Eigenvalues
                .Where(ev => Math.Abs(ev) > tolerance)
                .Sum(ev => 1.0 / ev);
        }
    }

    /// <summary>
    ///     Spectral radius (max absolute eigenvalue)
    /// </summary>
    public double SpectralRadius => Eigenvalues.Max(Math.Abs);

    /// <summary>
    ///     Get eigenvalue at index i (0-based)
    /// </summary>
    public double GetEigenvalue(int i)
    {
        return Eigenvalues[i];
    }

    /// <summary>
    ///     Get eigenvector at index i (0-based)
    /// </summary>
    public Vector<double> GetEigenvector(int i)
    {
        return Eigenvectors.Column(i);
    }

    /// <summary>
    ///     Get the k smallest eigenvalues
    /// </summary>
    public double[] GetSmallestEigenvalues(int k)
    {
        k = Math.Min(k, Eigenvalues.Length);
        return [.. Eigenvalues.Take(k)];
    }

    /// <summary>
    ///     Get the k largest eigenvalues
    /// </summary>
    public double[] GetLargestEigenvalues(int k)
    {
        k = Math.Min(k, Eigenvalues.Length);
        return [.. Eigenvalues.TakeLast(k).Reverse()];
    }

    /// <summary>
    ///     Partition graph into two parts using Fiedler vector
    ///     Returns indices of nodes in each partition
    /// </summary>
    public (int[] Partition1, int[] Partition2) GetFiedlerPartition()
    {
        if (FiedlerVector == null)
        {
            return ([], []);
        }

        var partition1 = new List<int>();
        var partition2 = new List<int>();

        for (var i = 0; i < FiedlerVector.Count; i++)
        {
            if (FiedlerVector[i] >= 0)
            {
                partition1.Add(i);
            }
            else
            {
                partition2.Add(i);
            }
        }

        return (partition1.ToArray(), partition2.ToArray());
    }

    public override string ToString()
    {
        return $"SpectralMetrics[n={NodeCount}, ?1={Lambda1:F4}, ?2={AlgebraicConnectivity:F4}, " +
               $"gap={SpectralGap:F4}, connected={IsConnected}, components�{EstimatedComponentCount}]";
    }
}
