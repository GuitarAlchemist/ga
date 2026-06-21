namespace GA.Business.ML.Tests;

using GA.Business.ML.Embeddings;

/// <summary>
///     Contract tests for the layout operations that now live on <see cref="EmbeddingSchema"/>
///     (the deepening from /improve-codebase-architecture candidate #1). These guard the two
///     invariants the consumers rely on but nothing could test before the layout owned its
///     read/write/score: the compact-vs-raw scoring equivalence, and the agreement between the
///     flat partition consts (which the ILGPU kernel must bake at compile time) and the
///     authoritative <see cref="EmbeddingSchema.Partitions"/> registry.
/// </summary>
[TestFixture]
public class SchemaLayoutContractTests
{
    private static double[] MakeRaw(int seed)
    {
        var v = new double[EmbeddingSchema.TotalDimension];
        foreach (var p in EmbeddingSchema.SimilarityPartitions)
        {
            for (var j = 0; j < p.Dim; j++)
            {
                // Deterministic, non-zero per-slot pattern so every similarity partition contributes.
                v[p.Start + j] = System.Math.Sin(0.1 * (p.Start + j) + seed) + 1.5;
            }
        }

        return v;
    }

    [Test]
    public void WeightedPartitionCosine_Equals_CompactDotProduct()
    {
        var a = MakeRaw(1);
        var b = MakeRaw(2);

        var weighted = EmbeddingSchema.WeightedPartitionCosine(a, b);

        var ca = EmbeddingSchema.ExtractCompact(a);
        var cb = EmbeddingSchema.ExtractCompact(b);
        var dot = 0.0;
        for (var i = 0; i < ca.Length; i++)
        {
            dot += ca[i] * cb[i];
        }

        TestContext.WriteLine($"weighted-partition-cosine={weighted:F9}  compact-dot={dot:F9}");
        // The v4-pp identity: dot(ExtractCompact(a), ExtractCompact(b)) == WeightedPartitionCosine(a,b),
        // up to float weight precision (SqrtWeight^2 vs SimilarityWeight).
        Assert.That(dot, Is.EqualTo(weighted).Within(1e-6));
    }

    [Test]
    public void WeightedPartitionCosine_IdenticalVectors_SumsToTotalWeight()
    {
        var a = MakeRaw(7);
        // cosine of a partition with itself is 1, so the score is the sum of similarity weights.
        var expected = 0.0;
        foreach (var p in EmbeddingSchema.SimilarityPartitions)
        {
            expected += p.SimilarityWeight;
        }

        Assert.That(EmbeddingSchema.WeightedPartitionCosine(a, a), Is.EqualTo(expected).Within(1e-6));
    }

    [Test]
    public void WeightedPartitionCosine_IsLengthTolerant_OnLegacyVectors()
    {
        var full = MakeRaw(3);
        // A v1.7-length vector (228) lacks the ROOT partition (228-239); it must still score on the
        // partitions it has, not throw or return 0.
        var legacy = full[..228];
        Assert.That(() => EmbeddingSchema.WeightedPartitionCosine(legacy, full), Throws.Nothing);
        Assert.That(EmbeddingSchema.WeightedPartitionCosine(legacy, full), Is.GreaterThan(0.0));
    }

    [Test]
    public void ExtractCompact_HasCompactDimension()
    {
        var compact = EmbeddingSchema.ExtractCompact(MakeRaw(4));
        Assert.That(compact.Length, Is.EqualTo(EmbeddingSchema.CompactDimension));
    }

    [Test]
    public void WriteInto_PlacesSliceAtPartitionOffset()
    {
        var raw = new double[EmbeddingSchema.TotalDimension];
        double[] slice = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        EmbeddingSchema.WriteInto(raw, "ROOT", slice);

        var root = EmbeddingSchema.GetPartition("ROOT");
        Assert.Multiple(() =>
        {
            Assert.That(raw[root.Start], Is.EqualTo(1));
            Assert.That(raw[root.Start + 11], Is.EqualTo(12));
            Assert.That(raw[root.Start - 1], Is.EqualTo(0), "must not write before the partition");
        });
    }

    [Test]
    public void GetPartition_UnknownName_Throws() =>
        Assert.That(() => EmbeddingSchema.GetPartition("NOPE"), Throws.ArgumentException);

    // The flat consts the ILGPU kernel bakes at compile time must equal the authoritative
    // Partitions[] registry. This is the guard referenced by GpuVoicingSearchStrategy.
    [Test]
    public void FlatConsts_Match_PartitionRegistry()
    {
        Assert.Multiple(() =>
        {
            Check("STRUCTURE",  EmbeddingSchema.StructureOffset,  EmbeddingSchema.StructureDim,  EmbeddingSchema.StructureWeight);
            Check("MORPHOLOGY", EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyDim, EmbeddingSchema.MorphologyWeight);
            Check("CONTEXT",    EmbeddingSchema.ContextOffset,    EmbeddingSchema.ContextDim,    EmbeddingSchema.ContextWeight);
            Check("SYMBOLIC",   EmbeddingSchema.SymbolicOffset,   EmbeddingSchema.SymbolicDim,   EmbeddingSchema.SymbolicWeight);
            Check("MODAL",      EmbeddingSchema.ModalOffset,      EmbeddingSchema.ModalDim,      EmbeddingSchema.ModalWeight);
            Check("ROOT",       EmbeddingSchema.RootOffset,       EmbeddingSchema.RootDim,       EmbeddingSchema.RootWeight);
        });

        static void Check(string name, int offset, int dim, double weight)
        {
            var p = EmbeddingSchema.GetPartition(name);
            Assert.That(p.Start, Is.EqualTo(offset), $"{name} offset");
            Assert.That(p.Dim, Is.EqualTo(dim), $"{name} dim");
            Assert.That((double)p.SimilarityWeight, Is.EqualTo(weight).Within(1e-6), $"{name} weight");
        }
    }
}
