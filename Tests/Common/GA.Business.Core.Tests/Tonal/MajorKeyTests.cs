namespace GA.Business.Core.Tests.Tonal;

[TestFixture]
public class MajorKeyTests
{
    [TestCase("Cb", new[] { "Cb", "Db", "Eb", "Fb", "Gb", "Ab", "Bb" })]
    [TestCase("Gb", new[] { "Gb", "Ab", "Bb", "Cb", "Db", "Eb", "F" })]
    [TestCase("Db", new[] { "Db", "Eb", "F", "Gb", "Ab", "Bb", "C" })]
    [TestCase("Ab", new[] { "Ab", "Bb", "C", "Db", "Eb", "F", "G" })]
    [TestCase("Eb", new[] { "Eb", "F", "G", "Ab", "Bb", "C", "D" })]
    [TestCase("Bb", new[] { "Bb", "C", "D", "Eb", "F", "G", "A" })]
    [TestCase("F", new[] { "F", "G", "A", "Bb", "C", "D", "E" })]
    [TestCase("C", new[] { "C", "D", "E", "F", "G", "A", "B" })]
    [TestCase("G", new[] { "G", "A", "B", "C", "D", "E", "F#" })]
    [TestCase("D", new[] { "D", "E", "F#", "G", "A", "B", "C#" })]
    [TestCase("A", new[] { "A", "B", "C#", "D", "E", "F#", "G#" })]
    [TestCase("E", new[] { "E", "F#", "G#", "A", "B", "C#", "D#" })]
    [TestCase("B", new[] { "B", "C#", "D#", "E", "F#", "G#", "A#" })]
    [TestCase("F#", new[] { "F#", "G#", "A#", "B", "C#", "D#", "E#" })]
    [TestCase("C#", new[] { "C#", "D#", "E#", "F#", "G#", "A#", "B#" })]
        
    public void MajorKey_GetNotes_ReturnsCorrectSequenceOfNotes(string sMajorKeyRoot, string[] expectedKeyNotes)
    {
        if (!Key.Major.TryParse(sMajorKeyRoot, out var majorKey)) throw new InvalidOperationException();
        var actualNotes = majorKey.GetNotes().Select(note => note.ToString()).ToArray();

        Assert.AreEqual(expectedKeyNotes, actualNotes);
    }
}