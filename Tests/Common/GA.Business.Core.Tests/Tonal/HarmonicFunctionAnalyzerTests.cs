namespace GA.Business.Core.Tests.Tonal;

using GA.Business.ML.Embeddings.Services;
using GA.Domain.Core.Theory.Tonal;
using GA.Domain.Services.Tonal;

[TestFixture]
public class HarmonicFunctionAnalyzerTests
{
    [TestCase(1, HarmonicFunction.LeadingTone)]
    [TestCase(2, HarmonicFunction.Subtonic)]
    [TestCase(3, HarmonicFunction.Unknown)]
    public void SeventhDegree_FunctionDependsOnDistanceBelowTonic(
        int semitonesBelowTonic,
        HarmonicFunction expected) =>
        Assert.That(
            HarmonicFunctionAnalyzer.FromScaleDegree(7, semitonesBelowTonic),
            Is.EqualTo(expected));

    [Test]
    public void Parse_SubtonicName_ReturnsSubtonic() =>
        Assert.That(HarmonicFunctionAnalyzer.Parse("Subtonic"), Is.EqualTo(HarmonicFunction.Subtonic));

    [TestCase("Supertonic", HarmonicFunction.Supertonic)]
    [TestCase("Submediant", HarmonicFunction.Submediant)]
    public void Parse_CompoundFunctionNames_BeforeTheirSuffixes(string value, HarmonicFunction expected) =>
        Assert.That(HarmonicFunctionAnalyzer.Parse(value), Is.EqualTo(expected));

    [Test]
    public void Subtonic_MapsToDominantPrimaryCategory() =>
        Assert.That(
            HarmonicFunctionAnalyzer.ToPrimaryCategory(HarmonicFunction.Subtonic),
            Is.EqualTo(HarmonicFunctionCategory.Dominant));

    [Test]
    public void Subtonic_ActivatesDominantContextDimension() =>
        Assert.That(ContextVectorService.ComputeEmbedding("Subtonic")[2], Is.EqualTo(1.0));
}
