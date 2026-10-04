namespace GA.Business.Core.Tests.Chords;

using GA.Domain.Services.Chords.Parsing;

/// <summary>
///     Tests for the ChordSymbolParser.
/// </summary>
[Category("ChordParsing")]
public class ChordSymbolParserTests
{
    private static readonly string[] _validSymbols =
    [
        "C", "Am", "F#dim", "G7", "Cmaj7", "Dm7", "Bdim7", "Fø7", "Bb△9",
        "Dsus2", "Esus4", "A6", "Em6", "C9", "Cmaj11", "Dm11", "G13",
        "Cmaj13", "Em13", "Cadd9", "Emadd9", "C6/9", "Em6/9",
        "Galt", "C7b5", "C7#5", "C7b9", "C7#9", "Cmaj7#11", "C7b13",
        "C/E"
    ];

    private static readonly string[] _invalidSymbols =
    [
        "", " ", "Hmaj7", "R7", "7", "#C", "Z",
        // Unknown suffixes, and words of a chat message: none is a major triad
        "Cwobble", "Can", "Give", "and", "a", "compare", "Cmaj7 jazz voicings"
    ];

    [Test]
    public void Parse_ReturnsValidChord_On_ValidSymbols()
    {
        // Arrange
        var parser = new ChordSymbolParser();
        // Act & Assert
        foreach (var symbol in _validSymbols)
        {
            var chord = parser.Parse(symbol);
            TestContext.WriteLine($"Parsed '{symbol}' -> Root: {chord.Root}, Full Name: {chord.Symbol}");
            Assert.Multiple(() =>
            {
                Assert.That(chord, Is.Not.Null, $"Failed to parse '{symbol}'");
                Assert.That(chord.Symbol, Is.Not.Null);
                Assert.That(chord.Root, Is.Not.Null);
            });
        }
    }

    [Test]
    public void Parse_Rejects_Invalid_Symbols()
    {
        // Arrange
        var parser = new ChordSymbolParser();
        // Act & Assert
        foreach (var symbol in _invalidSymbols)
        {
            TestContext.WriteLine($"Testing invalid symbol: '{symbol}'");
            Assert.Multiple(() =>
            {
                Assert.That(() => parser.Parse(symbol), Throws.Exception,
                    $"Should throw for invalid symbol '{symbol}'");
                Assert.That(parser.TryParse(symbol, out var chord), Is.False);
                Assert.That(chord, Is.Null);
            });
        }
    }

    [TestCase("CM7", new[] { 0, 4, 7, 11 })]
    [TestCase("CM", new[] { 0, 4, 7 })]
    [TestCase("CΔ7", new[] { 0, 4, 7, 11 })]
    [TestCase("Cmi7", new[] { 0, 3, 7, 10 })]
    [TestCase("Co7", new[] { 0, 3, 6, 9 })]
    [TestCase("C+7", new[] { 0, 4, 8, 10 })]
    [TestCase("Cm(maj7)", new[] { 0, 3, 7, 11 })]
    [TestCase("Cm(b5)", new[] { 0, 3, 6 })]
    [TestCase("C7b5", new[] { 0, 4, 6, 10 })]
    [TestCase("C7#11", new[] { 0, 4, 6, 7, 10 })]
    [TestCase("C9#11", new[] { 0, 2, 4, 6, 7, 10 })]
    [TestCase("C7b9#11", new[] { 0, 1, 4, 6, 7, 10 })]
    [TestCase("C7(b9,#11)", new[] { 0, 1, 4, 6, 7, 10 })]
    [TestCase("C7#9b13", new[] { 0, 3, 4, 7, 8, 10 })]
    [TestCase("Cmaj7#11", new[] { 0, 4, 6, 7, 11 })]
    [TestCase("C7alt", new[] { 0, 1, 3, 4, 6, 8, 10 })]
    [TestCase("C9sus4", new[] { 0, 2, 5, 7, 10 })]
    [TestCase("C/F#", new[] { 0, 4, 6, 7 })]
    [TestCase("C5", new[] { 0, 7 })]
    public void Parse_ReadsEachToneOfTheSymbol(string symbol, int[] pitchClasses) =>
        Assert.That(new ChordSymbolParser().Parse(symbol).PitchClassSet.Select(pc => pc.Value), Is.EqualTo(pitchClasses));

    [Test]
    public void TryParse_ReturnsTrue_On_ValidSymbols()
    {
        // Arrange
        var parser = new ChordSymbolParser();
        // Act & Assert
        foreach (var symbol in _validSymbols)
        {
            var result = parser.TryParse(symbol, out var chord);
            TestContext.WriteLine($"TryParse '{symbol}' -> Success: {result}, Chord: {chord?.Symbol}");
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, $"TryParse failed for '{symbol}'");
                Assert.That(chord, Is.Not.Null);
            });
        }
    }
}
