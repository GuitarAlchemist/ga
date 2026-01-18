namespace GA.Business.ML.Tests.Tabs;

using GA.Business.ML.Tabs;
using NUnit.Framework;

[TestFixture]
public class TabTokenizerTests
{
    private TabTokenizer _tokenizer;
    private TabToPitchConverter _converter;

    [SetUp]
    public void Setup()
    {
        _tokenizer = new TabTokenizer();
        _converter = new TabToPitchConverter();
    }

    [Test]
    public void TestBasicGChord()
    {
        // Arrange
        // Standard G Major: 3 2 0 0 0 3
        var tab = @"
e|---3---|
B|---0---|
G|---0---|
D|---0---|
A|---2---|
E|---3---|
";
        // Act
        var blocks = _tokenizer.Tokenize(tab);
        var block = blocks[0];
        var noteSlice = block.Slices.FirstOrDefault(s => s.Notes.Count > 0);
        var lowE = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 0);
        var highE = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 5);
        var midi = noteSlice != null ? _converter.GetMidiNotes(noteSlice) : new List<int>();

        // Assert
        TestContext.WriteLine($"Tokenized Tab:\n{tab}");
        TestContext.WriteLine($"Block Count: {blocks.Count}, String Count: {block.StringCount}");
        TestContext.WriteLine($"Low E Fret: {lowE?.Fret}, High E Fret: {highE?.Fret}");
        TestContext.WriteLine($"MIDI Notes: {string.Join(", ", midi)}");

        Assert.Multiple(() =>
        {
            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(block.StringCount, Is.EqualTo(6));
            Assert.That(noteSlice, Is.Not.Null);
            Assert.That(noteSlice!.Notes.Count, Is.EqualTo(6));
            Assert.That(lowE?.Fret, Is.EqualTo(3));
            Assert.That(highE?.Fret, Is.EqualTo(3));
            Assert.That(midi, Does.Contain(43)); // G2 (Low E + 3 = 40+3)
            Assert.That(midi, Does.Contain(67)); // G4 (High e + 3 = 64+3)
        });
    }

    [Test]
    public void TestTwoDigitFret()
    {
        // Arrange
        // Power chord at 12th fret
        var tab = @"
e|-------|
B|-------|
G|-------|
D|--12---|
A|--12---|
E|--10---|
";
        // Act
        var blocks = _tokenizer.Tokenize(tab);
        var block = blocks[0];
        var noteSlice = block.Slices.FirstOrDefault(s => s.Notes.Count > 0);
        var lowE = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 0);
        var aString = noteSlice?.Notes.FirstOrDefault(n => n.StringIndex == 1);

        // Assert
        TestContext.WriteLine($"Two-digit Fret Tab:\n{tab}");
        TestContext.WriteLine($"Low E Fret: {lowE?.Fret}, A String Fret: {aString?.Fret}");

        Assert.Multiple(() =>
        {
            Assert.That(noteSlice, Is.Not.Null);
            Assert.That(lowE?.Fret, Is.EqualTo(10));
            Assert.That(aString?.Fret, Is.EqualTo(12));
        });
    }
}
