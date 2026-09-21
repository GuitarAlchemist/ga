namespace GA.Domain.Core.Tests.Theory.Harmony;

using GA.Domain.Core.Theory.Harmony;

[TestFixture]
public class ChordQualityTests
{
    [TestCase(ChordQuality.Major, ChordQuality.Major)]
    [TestCase(ChordQuality.Major7, ChordQuality.Major)]
    [TestCase(ChordQuality.Dominant, ChordQuality.Major)]
    [TestCase(ChordQuality.Minor, ChordQuality.Minor)]
    [TestCase(ChordQuality.Minor7, ChordQuality.Minor)]
    [TestCase(ChordQuality.Diminished, ChordQuality.Diminished)]
    [TestCase(ChordQuality.Diminished7, ChordQuality.Diminished)]
    [TestCase(ChordQuality.HalfDiminished, ChordQuality.Diminished)]
    public void ToTriadFamily_MapsSeventhSpeciesToTheirTriadFamily(
        ChordQuality quality,
        ChordQuality expected) =>
        Assert.That(quality.ToTriadFamily(), Is.EqualTo(expected));
}
