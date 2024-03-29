namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class MinorKeyTests
{
    [TestCase("Ab", new[] { "Ab", "Bb", "Cb", "Db", "Eb", "Fb", "Gb" })]
    [TestCase("Eb", new[] { "Eb", "F", "Gb", "Ab", "Bb", "Cb", "Db" })]
    [TestCase("Bb", new[] { "Bb", "C", "Db", "Eb", "F", "Gb", "Ab" })]
    [TestCase("F", new[] { "F", "G", "Ab", "Bb", "C", "Db", "Eb" })]
    [TestCase("C", new[] { "C", "D", "Eb", "F", "G", "Ab", "Bb" })]
    [TestCase("G", new[] { "G", "A", "Bb", "C", "D", "Eb", "F" })]
    [TestCase("D", new[] { "D", "E", "F", "G", "A", "Bb", "C" })]
    [TestCase("A", new[] { "A", "B", "C", "D", "E", "F", "G" })]
    [TestCase("E", new[] { "E", "F#", "G", "A", "B", "C", "D" })]
    [TestCase("B", new[] { "B", "C#", "D", "E", "F#", "G", "A" })]
    [TestCase("F#", new[] { "F#", "G#", "A", "B", "C#", "D", "E" })]
    [TestCase("C#", new[] { "C#", "D#", "E", "F#", "G#", "A", "B" })]
    [TestCase("G#", new[] { "G#", "A#", "B", "C#", "D#", "E", "F#" })]
    [TestCase("D#", new[] { "D#", "E#", "F#", "G#", "A#", "B", "C#" })]
    [TestCase("A#", new[] { "A#", "B#", "C#", "D#", "E#", "F#", "G#" })]
    
    public void MinorKey_GetNotes_ReturnsCorrectSequenceOfNotes(string minorKeyRoot, string[] expectedKeyNotes)
    {
        if (!Key.Minor.TryParse(minorKeyRoot, out var minorKey)) throw new InvalidOperationException();
        var actualNotes = minorKey.GetNotes().Select(note => note.ToString()).ToArray();

        Assert.That(expectedKeyNotes, Is.EqualTo(actualNotes));
    }
}