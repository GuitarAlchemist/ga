namespace GaMcpServer.Tests;

using GaMcpServer.Tools;

/// <summary>
/// Set-theory MCP tools checked against textbook values (Forte numbers from Rahn's list,
/// as used by the Music theory for Guitar Alchemist course, lesson 4).
/// </summary>
[TestFixture]
public sealed class ChordAtonalToolTests
{
    [OneTimeSetUp]
    public void RegisterClosures() => GA.Business.DSL.GaClosureBootstrap.init();

    [TestCase("Cm7b5", "{C, Eb, F#, Bb}", "4-27")]
    [TestCase("Cdim7", "{C, Eb, F#, A}", "4-28")]
    [TestCase("C9", "{C, D, E, G, Bb}", "5-34")]
    [TestCase("G7b9", "{D, F, G, Ab, B}", "5-31")]
    [TestCase("Caug", "{C, E, Ab}", "3-12")]
    public async Task GaChordToSet_UsesAlteredAndImpliedChordTones(string symbol, string pitchSet, string forte)
    {
        var result = await ChordAtonalTool.GaChordToSet(symbol);

        Assert.That(result, Contains.Substring(pitchSet));
        Assert.That(result, Contains.Substring($"Forte:      {forte}"));
    }

    [TestCase("C", "Major Triad")]
    [TestCase("Am", "Minor Triad")]
    [TestCase("Cm7b5", "Half Diminished Seventh")]
    [TestCase("G7", "Dominant Seventh")]
    public async Task GaChordToSet_NamesTheSetOnlyWithATranspositionOfIt(string symbol, string name)
    {
        // Modes.yaml files several seventh chords with different interval vectors under the vector
        // <0 1 2 1 1 1>; a lookup by vector alone labelled Cm7b5 (and Cdim7) "Major Seventh".
        var result = await ChordAtonalTool.GaChordToSet(symbol);

        var scaleLine = result.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith("Scale:"));
        Assert.That(scaleLine, Is.EqualTo($"Scale:      {name}"));
    }
}
