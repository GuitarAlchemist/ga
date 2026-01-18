namespace GA.Business.Core.Tests.Tonal;

using Core.Scales;
using NUnit.Framework;

[TestFixture]
public class ScaleModalMetadataTests
{
    [Test]
    public void ModalScales_ExposeModalFamilyMetadata()
    {
        var modalScales = Scale.Items.Where(scale => scale.IsModal).ToList();

        Assert.That(modalScales.Count, Is.GreaterThan(10), "Expect multiple modal scales in the catalog.");

        foreach (var scale in modalScales)
        {
            Assert.That(scale.ModalFamily, Is.Not.Null, $"Modal family missing for scale {scale.PitchClassSet.Id}");
            Assert.That(scale.ModalFamily!.Modes.Any(mode => mode.Id == scale.PitchClassSet.Id),
                Is.True,
                $"Modal family {scale.ModalFamily} for {scale.PitchClassSet.Id} does not expose the scale's mode id.");
        }
    }

    [Test]
    public void MajorScale_IsAssociatedWithMajorModalFamily()
    {
        var major = Scale.Major;
        var modalFamily = major.ModalFamily;

        Assert.That(modalFamily, Is.Not.Null, "Major scale should expose a modal family.");
        Assert.That(modalFamily!.ModeIds.Contains(major.PitchClassSet.Id), Is.True,
            "Major modal family should contain the major scale ID.");
        Assert.That(modalFamily.NoteCount, Is.EqualTo(major.Count), "Modal family note count should match the scale's count.");
    }
}
