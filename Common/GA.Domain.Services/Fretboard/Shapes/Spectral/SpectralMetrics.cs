namespace GA.Domain.Services.Fretboard.Shapes.Spectral;

/// <summary>
///     Spectral metrics for a shape graph
/// </summary>
public record SpectralMetrics
{
    /// <summary>
    ///     Number of nodes in the graph
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    ///     Laplacian eigenvalues (sorted ascending)
    /// </summary>
    public required double[] Eigenvalues { get; init; }

    /// <summary>
    ///     The smallest eigenvalue (λ1), which is approximately 0 for connected components.
    ///     Note: In some literature, this is λ0, but GA tests use λ1 naming occasionally.
    /// </summary>
    public double Lambda1 => Eigenvalues.Length > 0 ? Eigenvalues[0] : 0;

    /// <summary>
    ///     Algebraic connectivity (λ2): The second smallest eigenvalue of the Laplacian.
    ///     Measures how well-connected the graph is.
    /// </summary>
    public double AlgebraicConnectivity => Eigenvalues.Length > 1 ? Eigenvalues[1] : 0;

    /// <summary>
    ///     Spectral gap (λ3 - λ2 or similar)
    /// </summary>
    public double? SpectralGap => Eigenvalues.Length > 2 ? Eigenvalues[2] - Eigenvalues[1] : null;
}
