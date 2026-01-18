namespace GA.Business.Core.Tests.AI.Embeddings;

using GA.Business.ML.Embeddings.Services;
using NUnit.Framework;

[TestFixture]
public class TheoryVectorServiceTests
{
    private TheoryVectorService _service;

    [SetUp]
    public void Setup()
    {
        _service = new TheoryVectorService();
    }

    [Test]
    public void ComputeEmbedding_CalculatesPCP()
    {
        var pcs = new[] { 0, 4, 7 }; // C Major
        var vector = _service.ComputeEmbedding(pitchClasses: pcs);

        Assert.That(vector[0], Is.EqualTo(1.0)); // C
        Assert.That(vector[4], Is.EqualTo(1.0)); // E
        Assert.That(vector[7], Is.EqualTo(1.0)); // G
        Assert.That(vector[1], Is.EqualTo(0.0)); // Db should be 0
    }

    [Test]
    public void ComputeEmbedding_BoostsRoot()
    {
        var pcs = new[] { 0, 4, 7 };
        var vector = _service.ComputeEmbedding(pitchClasses: pcs, rootPitchClass: 0);

        Assert.That(vector[0], Is.EqualTo(2.0)); // 1.0 (Presence) + 1.0 (Boost)
    }

    [Test]
    public void ComputeEmbedding_CalculatesCardinality()
    {
        // C Major Triad (3 notes)
        var pcs = new[] { 0, 4, 7, 0 }; // Duplicate 0 should be ignored for cardinality
        var vector = _service.ComputeEmbedding(pitchClasses: pcs);

        // Dim 12: Cardinality / 12.0 * 2.0 in the Structure subspace (which is Index 18 of full vector)
        // (3 / 12.0) * 2.0 = 0.5
        Assert.That(vector[12], Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void ComputeEmbedding_SetsTonalStability()
    {
        // With Root -> Stability 1.0
        var v1 = _service.ComputeEmbedding(new[] { 0, 4, 7 }, rootPitchClass: 0);
        // Index 22 is Tonal Stability in the Structure subspace
        Assert.That(v1[22], Is.EqualTo(1.0));

        // Without Root -> Stability 0.0
        var v2 = _service.ComputeEmbedding(new[] { 0, 4, 7 }, rootPitchClass: null);
        Assert.That(v2[22], Is.EqualTo(0.0));
    }
}
