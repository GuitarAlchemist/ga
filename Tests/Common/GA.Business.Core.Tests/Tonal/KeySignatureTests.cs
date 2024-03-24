#pragma warning disable CA1861 // Avoid constant arrays as arguments

namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class KeySignatureTests
{
    [TestCase(-7, new[] { "Bb", "Eb", "Ab", "Db", "Gb", "Cb", "Fb" })]
    [TestCase(-6, new[] { "Bb", "Eb", "Ab", "Db", "Gb", "Cb" })]
    [TestCase(-5, new[] { "Bb", "Eb", "Ab", "Db", "Gb" })]
    [TestCase(-4, new[] { "Bb", "Eb", "Ab", "Db" })]
    [TestCase(-3, new[] { "Bb", "Eb", "Ab" })]
    [TestCase(-2, new[] { "Bb", "Eb" })]
    [TestCase(-1, new[] { "Bb" })]
    [TestCase(0, new string[] { })]
    [TestCase(1, new[] { "F#" })]
    [TestCase(2, new[] { "F#", "C#" })]
    [TestCase(3, new[] { "F#", "C#", "G#" })]
    [TestCase(4, new[] { "F#", "C#", "G#", "D#" })]
    [TestCase(5, new[] { "F#", "C#", "G#", "D#", "A#" })]
    [TestCase(6, new[] { "F#", "C#", "G#", "D#", "A#", "E#" })]
    [TestCase(7, new[] { "F#", "C#", "G#", "D#", "A#", "E#", "B#" })]
    public void SignatureNotes_CorrectOrderAndAccidentals(int value, string[] expectedAccidentedNotes)
    {
        var keySignature = KeySignature.FromValue(value);
        var actualAccidentedNotes = keySignature.AccidentedNotes.Select(note => note.ToString()).ToArray();
        
        Assert.AreEqual(expectedAccidentedNotes, actualAccidentedNotes);
    }
}