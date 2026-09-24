namespace GA.Business.Core.Tests.Tonal;

using GA.Domain.Services.Tonal.Cadences;

[TestFixture]
public class CadenceCatalogTests
{
    [Test]
    public void ChromaticMediantMetal_UsesMinorKeyScaleDegree() =>
        Assert.That(
            CadenceCatalog.Items.Single(item => item.Name == "Chromatic Mediant (Metal)").RomanNumerals,
            Is.EqualTo(new[] { "i", "iii" }));
}
