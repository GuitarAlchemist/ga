namespace GA.Business.ML.Tests.Search;

using GA.Business.ML.Embeddings;

/// <summary>
///     Pins the actual magnitude of the on-disk OPTK compact vectors.
///
///     <para>
///         <c>OptickIndexReader</c> and <c>OptickIndexWriter</c> both described these vectors as
///         "L2-normalized", which they are not. <see cref="EmbeddingSchema.ExtractCompact" />
///         normalizes each similarity partition to unit length *independently* and then scales it
///         by <c>sqrt(weight)</c>. Squaring and summing gives
///         <c>‖v‖² = Σ weight[p]</c> over the partitions with a non-zero raw slice — not 1.
///     </para>
///     <para>
///         With the v4-pp-r weights (STRUCTURE .45, MORPHOLOGY .25, CONTEXT .20, SYMBOLIC .10,
///         MODAL .10, ROOT .05) a fully populated vector has ‖v‖² = 1.15, and a vector missing one
///         0.10 partition has ‖v‖² = 1.05 — the two values measured against a real index.
///     </para>
///     <para>
///         The dot product of two such vectors is still exactly the intended score — it equals
///         <see cref="EmbeddingSchema.WeightedPartitionCosine" /> of the raws — but it is a
///         weighted partition cosine, not a plain cosine, and it is bounded by Σ weight rather
///         than by 1. Anything that assumes unit vectors (a similarity threshold read as a
///         cosine, a distance derived as <c>2 - 2·dot</c>) is wrong by that factor.
///     </para>
/// </summary>
[TestFixture]
public class CompactVectorNormTests
{
    private static double[] MakeRaw(int seed, params string[] zeroPartitions)
    {
        var v = new double[EmbeddingSchema.TotalDimension];
        foreach (var p in EmbeddingSchema.SimilarityPartitions)
        {
            if (zeroPartitions.Contains(p.Name)) continue;

            for (var j = 0; j < p.Dim; j++)
            {
                v[p.Start + j] = Math.Sin(0.1 * (p.Start + j) + seed) + 1.5;
            }
        }

        return v;
    }

    private static double SquaredNorm(double[] v) => v.Sum(x => x * x);

    [Test]
    public void SimilarityWeights_DoNotSumToOne()
    {
        var sum = EmbeddingSchema.SimilarityPartitions.Sum(p => (double)p.SimilarityWeight);

        Assert.That(sum, Is.EqualTo(1.15).Within(1e-6),
            "The v4-pp-r similarity weights sum to 1.15; this is the squared norm of a fully "
            + "populated compact vector.");
    }

    [Test]
    public void ExtractCompact_IsNotL2Normalized()
    {
        var compact = EmbeddingSchema.ExtractCompact(MakeRaw(seed: 1));

        Assert.That(SquaredNorm(compact), Is.Not.EqualTo(1.0).Within(1e-3),
            "Compact vectors are per-partition normalized then sqrt(weight)-scaled, so they are "
            + "not unit vectors.");
    }

    [Test]
    public void ExtractCompact_SquaredNorm_EqualsSumOfSimilarityWeights()
    {
        var expected = EmbeddingSchema.SimilarityPartitions.Sum(p => (double)p.SimilarityWeight);

        foreach (var seed in new[] { 1, 2, 7 })
        {
            var compact = EmbeddingSchema.ExtractCompact(MakeRaw(seed));
            Assert.That(SquaredNorm(compact), Is.EqualTo(expected).Within(1e-6),
                $"seed {seed}: ‖v‖² must equal Σ weight over the populated partitions.");
        }
    }

    [Test]
    public void ExtractCompact_SquaredNorm_DropsTheWeightOfAnEmptyPartition()
    {
        // A voicing with no modal context leaves MODAL (weight 0.10) all-zero: 1.15 - 0.10 = 1.05,
        // the second value measured against a real index.
        var compact = EmbeddingSchema.ExtractCompact(MakeRaw(seed: 1, zeroPartitions: "MODAL"));

        Assert.That(SquaredNorm(compact), Is.EqualTo(1.05).Within(1e-6));
    }

    [Test]
    public void DotProduct_OfCompactVectors_EqualsWeightedPartitionCosineOfRaws()
    {
        // The property the "so cosine similarity reduces to a dot product" sentence was reaching
        // for: the dot product is the weighted partition cosine — which needs no unit vectors.
        var a = MakeRaw(1);
        var b = MakeRaw(2);

        var dot = EmbeddingSchema.ExtractCompact(a).Zip(EmbeddingSchema.ExtractCompact(b), (x, y) => x * y).Sum();

        Assert.That(dot, Is.EqualTo(EmbeddingSchema.WeightedPartitionCosine(a, b)).Within(1e-6));
    }
}
