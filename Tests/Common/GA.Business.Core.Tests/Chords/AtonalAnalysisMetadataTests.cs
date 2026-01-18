namespace GA.Business.Core.Tests.Chords;

using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Chords.Analysis.Atonal;
using GA.Business.Core.Scales;
using NUnit.Framework;
using TonalChordTemplate = GA.Business.Core.Chords.ChordTemplate;

[TestFixture]
public class AtonalAnalysisMetadataTests
{
    [Test]
    [Obsolete("Obsolete")]
    public void AnalyzeAtonally_TonalScale_ExposesForteAndModalDetails()
    {
        // Arrange
        var scale = Scale.Major;
        var chord = TonalChordTemplate.Analytical.FromPitchClassSet(scale.PitchClassSet, "Ionian Scale Analysis");

        // Act
        var analysis = AtonalChordAnalysisService.AnalyzeAtonally(chord, PitchClass.C);

        // Assert
        TestContext.WriteLine($"Scale: {scale}");
        TestContext.WriteLine($"Forte Number: {analysis.ForteNumber}");
        TestContext.WriteLine($"Suggested Name: {analysis.SuggestedName}");
        TestContext.WriteLine($"Alternate Names: {string.Join(", ", analysis.AlternateNames)}");

        Assert.Multiple(() =>
        {
            Assert.That(analysis.SetClass, Is.Not.Null);
            Assert.That(analysis.SetClass.ModalFamily, Is.Not.Null);
            Assert.That(analysis.SetClass.ModalFamily!.ModeIds.Contains(scale.PitchClassSet.Id), Is.True);
            Assert.That(analysis.IsModal, Is.True);
            Assert.That(analysis.ForteNumber, Does.StartWith($"{scale.PitchClassSet.Cardinality.Value}-"));
            Assert.That(analysis.AlternateNames.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(analysis.AlternateNames.Any(name => name.Contains(analysis.ForteNumber)), Is.True);
            Assert.That(analysis.TheoreticalDescription, Does.Contain("Modal family"));
            Assert.That(analysis.SuggestedName, Is.Not.Null.And.Not.Empty);
        });
    }
}
