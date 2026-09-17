namespace GA.Business.ML.Tests;

using GA.Business.Core.Analysis.Voicings;
using GA.Business.ML.Musical.Analysis;

[TestFixture]
public class ChordIdentificationExtensionsTests
{
    private static ChordIdentification Chord(string root, int? matchDistance) =>
        new(ChordName: "test", RootPitchClass: root, HarmonicFunction: "", IsNaturallyOccurring: true,
            FunctionalDescription: "", Quality: "")
        {
            MatchDistance = matchDistance
        };

    // Recognized root: RootPitchClass is a note name.
    [TestCase("C", 0)]
    [TestCase("Db", 1)]
    [TestCase("E", 4)]
    [TestCase("F#", 6)]
    [TestCase("A", 9)]
    [TestCase("Bb", 10)]
    [TestCase("B", 11)]
    public void TryGetRootPitchClass_NoteName(string root, int expected) =>
        Assert.Multiple(() =>
        {
            foreach (var matchDistance in new int?[] { 0, 1, null })
            {
                Assert.That(Chord(root, matchDistance).TryGetRootPitchClass(out var pitchClass), Is.True);
                Assert.That(pitchClass.Value, Is.EqualTo(expected), $"{root}, MatchDistance {matchDistance}");
            }
        });

    // No root recognized (MatchDistance -1): VoicingHarmonicAnalyzer stores the bass pitch class in set notation.
    [TestCase("0", 0)]
    [TestCase("4", 4)]
    [TestCase("T", 10)]
    [TestCase("E", 11)]
    public void TryGetRootPitchClass_Fallback_SetNotation(string root, int expected) =>
        Assert.Multiple(() =>
        {
            Assert.That(Chord(root, -1).TryGetRootPitchClass(out var pitchClass), Is.True);
            Assert.That(pitchClass.Value, Is.EqualTo(expected));
        });

    [TestCase("")]
    [TestCase("H")]
    public void TryGetRootPitchClass_Invalid(string root) =>
        Assert.That(Chord(root, 0).TryGetRootPitchClass(out _), Is.False);
}
