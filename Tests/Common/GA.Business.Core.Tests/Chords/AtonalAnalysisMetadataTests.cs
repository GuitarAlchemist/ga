namespace GA.Business.Core.Tests.Chords;

using System.Linq;
using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Notes;
using GA.Business.Core.Scales;
using TonalChordTemplate = GA.Business.Core.Chords.ChordTemplate;
using NUnit.Framework;

[TestFixture]
public class AtonalAnalysisMetadataTests
{
    [Test]
    [Obsolete("Obsolete")]
    public void AnalyzeAtonally_TonalScale_ExposesForteAndModalDetails()
    {
        var scale = Scale.Major;
        var chord = TonalChordTemplate.Analytical.FromPitchClassSet(scale.PitchClassSet, "Ionian Scale Analysis");
        var analysis = AtonalChordAnalysisService.AnalyzeAtonally(chord, PitchClass.C);

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
