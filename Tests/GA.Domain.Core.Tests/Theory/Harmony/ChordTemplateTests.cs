namespace GA.Domain.Core.Tests.Theory.Harmony;

using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Core.Theory.Tonal.Modes.Diatonic;
using NUnit.Framework;

[TestFixture]
public class ChordTemplateTests
{
    // A derived record synthesizes its own ToString unless the base's is sealed
    [Test]
    public void TonalModal_ToString_IsTheName()
    {
        var template = new ChordTemplate.TonalModal(ChordFormula.Major7, MajorScaleMode.Get(1), 1);

        Assert.Multiple(() =>
        {
            Assert.That(template.ToString(), Is.EqualTo(template.Name));
            Assert.That($"{template}", Is.EqualTo(template.Name));
        });
    }

    [Test]
    public void Analytical_ToString_IsTheName()
    {
        var template = ChordTemplate.Analytical.FromSetTheory(ChordFormula.Major, "Major triad");

        Assert.Multiple(() =>
        {
            Assert.That(template.ToString(), Is.EqualTo("Major triad"));
            Assert.That($"{template}", Is.EqualTo("Major triad"));
        });
    }
}
