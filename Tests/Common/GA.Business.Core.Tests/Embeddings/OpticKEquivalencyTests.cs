namespace GA.Business.Core.Tests.Embeddings;

using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using NUnit.Framework;

[TestFixture]
public class OpticKEquivalencyTests
{
    private TheoryVectorService _theoryService;

    [SetUp]
    public void SetUp()
    {
        _theoryService = new TheoryVectorService();
    }

    [Test]
    public void Transposition_Invariance_ICV_Match()
    {
        // C Major: {0, 4, 7} -> ICV: 001110
        // G Major: {7, 11, 2} -> ICV: 001110
        var cMajorPcs = new[] { 0, 4, 7 };
        var gMajorPcs = new[] { 7, 11, 2 };
        const string icv = "001110";

        var vC = _theoryService.ComputeEmbedding(cMajorPcs, intervalClassVector: icv);
        var vG = _theoryService.ComputeEmbedding(gMajorPcs, intervalClassVector: icv);

        // ICV indices are 13-18
        for (int i = 13; i <= 18; i++)
        {
            Assert.That(vC[i], Is.EqualTo(vG[i]), $"Index {i} should match for transpositionally equivalent sets.");
        }
    }

    [Test]
    public void Involution_Invariance_ICV_Match()
    {
        // C Major: {0, 4, 7} -> ICV: 001110
        // C Minor: {0, 3, 7} -> ICV: 001110 (Inversion of Major)
        var cMajorPcs = new[] { 0, 4, 7 };
        var cMinorPcs = new[] { 0, 3, 7 };
        const string icv = "001110";

        var vMaj = _theoryService.ComputeEmbedding(cMajorPcs, intervalClassVector: icv);
        var vMin = _theoryService.ComputeEmbedding(cMinorPcs, intervalClassVector: icv);

        // ICV indices are 13-18
        for (int i = 13; i <= 18; i++)
        {
            Assert.That(vMaj[i], Is.EqualTo(vMin[i]), $"Index {i} should match for inversionally equivalent sets.");
        }
    }

    [Test]
    public void Cardinality_Encoding_Correct()
    {
        var pcs3 = new[] { 0, 4, 7 };
        var pcs4 = new[] { 0, 4, 7, 10 };

        var v3 = _theoryService.ComputeEmbedding(pcs3);
        var v4 = _theoryService.ComputeEmbedding(pcs4);

        // Index 12 is Cardinality (Weighted: * 2.0)
        Assert.That(v3[12], Is.EqualTo(0.5));
        Assert.That(v4[12], Is.EqualTo(4.0 / 12.0 * 2.0).Within(0.001));
    }

    [Test]
    public void Identity_Encoding_OneHot_Correct()
    {
        var identityService = new IdentityVectorService();

        var vChord = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Chord);
        var vScale = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Scale);

        // Chord is index 0, Scale is index 1
        Assert.That(vChord[0], Is.EqualTo(1.0));
        Assert.That(vChord[1], Is.EqualTo(0.0));

        Assert.That(vScale[1], Is.EqualTo(1.0));
        Assert.That(vScale[0], Is.EqualTo(0.0));
    }

    [Test]
    public void Composite_Identity_Logic_Works()
    {
        var identityService = new IdentityVectorService();

        var vVoicing = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Voicing);

        // A Voicing (2) is also a Chord (0) and a PitchClassSet (5)
        Assert.That(vVoicing[2], Is.EqualTo(1.0), "Should be a Voicing");
        Assert.That(vVoicing[0], Is.EqualTo(1.0), "A Voicing should also be a Chord");
        Assert.That(vVoicing[5], Is.EqualTo(1.0), "A Chord should imply a PitchClassSet");
    }
}
