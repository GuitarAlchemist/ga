namespace GA.Business.Core.Tests.Tonal;

using Domain.Core.Theory.Atonal;
using Domain.Core.Theory.Tonal;

[TestFixture]
public class ModeCatalogTests
{
    [Test]
    public void ModeCatalog_ShouldLoadFamiliesFromYaml()
    {
        var metadata = ModeCatalog.Metadata;
        Assert.That(metadata, Is.Not.Empty);
        // Diatonic ICV: <2 5 4 3 6 1>
        var icv = IntervalClassVector.Parse("<2 5 4 3 6 1>");
        Assert.That(metadata.ContainsKey(icv), Is.True, "Should contain Major Scale Family");
        var majorFamily = metadata[icv];
        Assert.That(majorFamily.FamilyName, Is.EqualTo("Major Scale Family"));
        Assert.That(majorFamily.ModeNames, Has.Length.EqualTo(7));
        Assert.That(majorFamily.ModeNames[0], Is.EqualTo("Ionian"));
    }

    [Test]
    public void ModeCatalog_ShouldLoadNewFamilies()
    {
        // Melodic Minor Family: [0, 2, 3, 5, 7, 9, 11]
        var melodicMinorPcs = new[] { 0, 2, 3, 5, 7, 9, 11 };
        var pcs = new PitchClassSet(melodicMinorPcs.Select(p => PitchClass.FromValue(p)));
        var icv = pcs.IntervalClassVector;
        Assert.That(ModeCatalog.Metadata.ContainsKey(icv), Is.True,
            $"Should contain Melodic Minor Family with ICV {icv}");
        var family = ModeCatalog.Metadata[icv];
        Assert.That(family.FamilyName, Is.EqualTo("Melodic Minor Family"));
    }

    [Test]
    public void ModeCatalog_TryGetMode_ShouldWorkForNewModes()
    {
        // Double Harmonic: [0, 1, 4, 5, 7, 8, 11]
        var doubleHarmonicPcs = new[] { 0, 1, 4, 5, 7, 8, 11 };
        var pcs = new PitchClassSet(doubleHarmonicPcs.Select(p => PitchClass.FromValue(p)));
        var found = ModeCatalog.TryGetMode(pcs.Id, out var info);
        Assert.That(found, Is.True);
        Assert.That(info.FamilyName, Is.EqualTo("Double Harmonic Family"));
        Assert.That(info.ModeName, Is.EqualTo("Double Harmonic (Byzantine)"));
        Assert.That(info.Degree, Is.EqualTo(1));
    }
}
