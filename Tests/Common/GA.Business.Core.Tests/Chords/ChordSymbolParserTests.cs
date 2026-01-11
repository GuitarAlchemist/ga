namespace GA.Business.Core.Tests.Chords;

using System;
using GA.Business.Core.Chords;
using GA.Business.Core.Notes;
using NUnit.Framework;

/// <summary>
/// Tests for the ChordSymbolParser.
/// </summary>
[Category("ChordParsing")]
public class ChordSymbolParserTests
{
    private static readonly string[] ValidSymbols =
    {
        "C", "Am", "F#dim", "G7", "Cmaj7", "Dm7", "Bdim7", "Fø7", "Bb△9",
        "Dsus2", "Esus4", "A6", "Em6", "C9", "Cmaj11", "Dm11", "G13",
        "Cmaj13", "Em13", "Cadd9", "Emadd9", "C6/9", "Em6/9",
        "Galt", "C7b5", "C7#5", "C7b9", "C7#9", "Cmaj7#11", "C7b13",
        "C/E"
    };

    private static readonly string[] InvalidSymbols =
    {
        "", " ", "Hmaj7", "R7", "7", "#C", "Z"
    };

    [Test]
    public void Parse_ReturnsValidChord_On_ValidSymbols()
    {
        var parser = new ChordSymbolParser();

        foreach (var symbol in ValidSymbols)
        {
            var chord = parser.Parse(symbol);
            Assert.That(chord, Is.Not.Null, $"Failed to parse '{symbol}'");
            Assert.That(chord.Symbol, Is.Not.Null); 
            
            // Basic check: Root should be valid
            Assert.That(chord.Root, Is.Not.Null);
        }
    }

    [Test]
    public void Parse_Rejects_Invalid_Symbols()
    {
        var parser = new ChordSymbolParser();

        foreach (var symbol in InvalidSymbols)
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => parser.Parse(symbol), Throws.Exception,
                    $"Should throw for invalid symbol '{symbol}'");
                
                Assert.That(parser.TryParse(symbol, out var chord), Is.False);
                Assert.That(chord, Is.Null);
            });
        }
    }
    
    [Test]
    public void TryParse_ReturnsTrue_On_ValidSymbols()
    {
        var parser = new ChordSymbolParser();

        foreach (var symbol in ValidSymbols)
        {
            var result = parser.TryParse(symbol, out var chord);
            Assert.That(result, Is.True, $"TryParse failed for '{symbol}'");
            Assert.That(chord, Is.Not.Null);
        }
    }
}
