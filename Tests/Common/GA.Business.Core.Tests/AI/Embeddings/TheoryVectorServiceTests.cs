namespace GA.Business.Core.Tests.AI.Embeddings;

using ML.Embeddings.Services;

[TestFixture]
public class TheoryVectorServiceTests
{
    [Test]
    public void ComputeEmbedding_CalculatesPCP()
    {
        var pcs = new[] { 0, 4, 7 }; // C Major
        var vector = TheoryVectorService.ComputeEmbedding(pcs);
        Assert.That(vector[0], Is.EqualTo(1.0)); // C
        Assert.That(vector[4], Is.EqualTo(1.0)); // E
        Assert.That(vector[7], Is.EqualTo(1.0)); // G
        Assert.That(vector[1], Is.EqualTo(0.0)); // Db should be 0
    }

    [Test]
    public void ComputeEmbedding_RootArg_DoesNotBoostStructurePcp()
    {
        // v1.8 removed the STRUCTURE root-boost: the root pitch class now lives in the dedicated ROOT
        // partition, so passing a root must leave the STRUCTURE pitch-class chroma (slots 0-11) unchanged.
        var pcs = new[] { 0, 4, 7 };
        var withRoot = TheoryVectorService.ComputeEmbedding(pcs, 0);
        var withoutRoot = TheoryVectorService.ComputeEmbedding(pcs);
        Assert.That(withRoot[0], Is.EqualTo(1.0));
        Assert.That(withRoot[..12], Is.EqualTo(withoutRoot[..12]));
    }

    [Test]
    public void ComputeEmbedding_CalculatesCardinality()
    {
        // C Major Triad (3 notes)
        var pcs = new[] { 0, 4, 7, 0 }; // Duplicate 0 should be ignored for cardinality
        var vector = TheoryVectorService.ComputeEmbedding(pcs);
        // Dim 12: Cardinality / 12.0 * 2.0 in the Structure subspace (which is Index 18 of full vector)
        // (3 / 12.0) * 2.0 = 0.5
        Assert.That(vector[12], Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void ComputeEmbedding_SetsTonalStability()
    {
        // With Root -> Stability 1.0
        var v1 = TheoryVectorService.ComputeEmbedding(new[] { 0, 4, 7 }, 0);
        // Index 22 is Tonal Stability in the Structure subspace
        Assert.That(v1[22], Is.EqualTo(1.0));
        // Without Root -> Stability 0.0
        var v2 = TheoryVectorService.ComputeEmbedding(new[] { 0, 4, 7 });
        Assert.That(v2[22], Is.EqualTo(0.0));
    }
}
