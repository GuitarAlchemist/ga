namespace GA.Domain.Core.Tests.Theory.Atonal;

using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

[TestFixture]
public class ScaleStructuralPropertiesTests
{
    [Test]
    public void Dorian_HasMyhillProperty_ReturnsTrue()
    {
        // Major / Dorian (1709) is diatonic and possesses Myhill's Property
        var scale = Scale.Major;
        Assert.That(scale.HasMyhillProperty, Is.True);
    }

    [Test]
    public void MajorScale_RothenbergPropriety_IsProper()
    {
        var scale = Scale.Major;
        // Major scale generic 3rds max size = 4, generic 4ths min size = 5, but generic 4ths max size = 6 (tritone) and generic 5ths min size = 6 (tritone) => Proper
        Assert.That(scale.RothenbergPropriety, Is.EqualTo(RothenbergPropriety.Proper));
    }

    [Test]
    public void DorianScale_ImperfectionCount_IsOne()
    {
        // Dorian / Major scale has 1 imperfect tone (Locrian degree lacks a perfect 5th)
        var scale = Scale.Major;
        Assert.That(scale.ImperfectionCount, Is.EqualTo(1));
    }

    [Test]
    public void MajorScale_IsWellFormed_ReturnsTrue()
    {
        var scale = Scale.Major;
        Assert.That(scale.IsWellFormed, Is.True);
    }

    [Test]
    public void MajorScale_ZeitlerLegitimacy_IsLegitimateScale()
    {
        var scale = Scale.Major;
        Assert.That(scale.ZeitlerLegitimacy, Is.EqualTo(ZeitlerLegitimacy.LegitimateScale));
    }
}
