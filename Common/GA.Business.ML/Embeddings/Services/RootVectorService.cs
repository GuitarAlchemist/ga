namespace GA.Business.ML.Embeddings.Services;

/// <summary>
///     Generates the ROOT partition of the musical embedding — a 12-dim one-hot over
///     pitch classes 0-11. Added in schema v1.8 (2026-04-19) to carry root-pitch-class
///     identity <b>outside</b> STRUCTURE, so that STRUCTURE remains genuinely
///     O+P+T+I-invariant per the schema contract.
///     <para>
///         When the invariant checker <c>ix-optick-invariants</c> tested same-PC-set
///         voicings across instruments, only 67/793 (8.4%) produced bit-identical
///         STRUCTURE slices. Root-privileging via STRUCTURE's root-boost was the
///         confound. Moving root to its own partition with low weight (0.05) preserves
///         root-specific retrieval signal without polluting set-class identity.
///     </para>
/// </summary>
public class RootVectorService
{
    /// <summary>Partition dimension — one slot per pitch class.</summary>
    public const int Dimension = 12;

    /// <summary>
    ///     Returns a 12-dim one-hot vector with bit <paramref name="rootPitchClass"/> set
    ///     to 1.0. Returns the zero vector if <paramref name="rootPitchClass"/> is null
    ///     (rootless voicings, no-root-specified queries).
    /// </summary>
    public double[] ComputeEmbedding(int? rootPitchClass)
    {
        var v = new double[Dimension];
        if (!rootPitchClass.HasValue) return v;
        var r = ((rootPitchClass.Value % 12) + 12) % 12;
        v[r] = 1.0;
        return v;
    }
}
