namespace GA.Domain.Core.Tests.Primitives;

using GA.Domain.Core.Primitives.Notes;
using NUnit.Framework;

[TestFixture]
public class PitchCollectionTests
{
    // Flat tunings from Instruments.yaml: the sharp-only parser used to read "Bb3" as B3, then rejected it once anchored
    [TestCase("E2 A2 D3 G3 B3 E4", "E2 A2 D3 G3 B3 E4")]
    [TestCase("Eb4 Eb4 G3 C3", "Eb4 Eb4 G3 C3")]
    [TestCase("D2 G2 D3 G3 Bb3 D4", "D2 G2 D3 G3 Bb3 D4")]
    [TestCase("E2  A2", "E2 A2")]
    public void TryParse_SharpAndFlatPitches(string input, string expected) =>
        Assert.Multiple(() =>
        {
            Assert.That(PitchCollection.TryParse(input, null, out var pitches), Is.True);
            Assert.That(string.Join(" ", pitches.Select(p => p.ToString())), Is.EqualTo(expected));
        });

    [Test]
    public void TryParse_FlatPitch_KeepsItsPitchClass() =>
        Assert.That(PitchCollection.Parse("Bb3").Single().PitchClass.Value, Is.EqualTo(10));

    [TestCase("Bb C4")]
    [TestCase("E2 H2")]
    public void TryParse_InvalidPitch_ReturnsFalse(string input) =>
        Assert.That(PitchCollection.TryParse(input, null, out _), Is.False);
}
