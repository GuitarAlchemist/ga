namespace GaMcpServer.Tests;

using System.ComponentModel;
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

    private static Dictionary<string, string[]> SubsByQuality(string result) =>
        result.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('['))
            .ToDictionary(
                l => l[1..l.IndexOf(']')],
                l => l[(l.IndexOf(']') + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries));

    [Test]
    public async Task GaSetClassSubs_GroupsEquivalentsByTheirOwnQuality()
    {
        var groups = SubsByQuality(await ChordAtonalTool.GaSetClassSubs("Am"));

        Assert.That(groups.Keys, Is.EquivalentTo(new[] { "maj", "m" }));
        Assert.That(groups["maj"], Has.Length.EqualTo(12).And.Contains("C").And.Contains("Bb"));
        Assert.That(groups["m"], Has.Length.EqualTo(11).And.Contains("Em").And.Not.Contains("Am"));
        Assert.That(groups["maj"].Concat(groups["m"]), Has.None.EndsWith("dim"));
    }

    [Test]
    public async Task GaSetClassSubs_G7_ListsDominantAndHalfDiminishedSevenths()
    {
        var groups = SubsByQuality(await ChordAtonalTool.GaSetClassSubs("G7"));

        Assert.That(groups.Keys, Is.EquivalentTo(new[] { "7", "m7b5" }));
        Assert.That(groups["7"], Has.Length.EqualTo(11).And.Not.Contains("G7"));
        Assert.That(groups["m7b5"], Has.Length.EqualTo(12).And.Contains("Bm7b5"));
    }

    [Test]
    public void GaSetClassSubs_Description_DoesNotContradictSetTheory()
    {
        // Am (A C E) and C (C E G) are inversions of each other: both are set class 3-11.
        var description = typeof(ChordAtonalTool).GetMethod(nameof(ChordAtonalTool.GaSetClassSubs))!
            .GetCustomAttributes(typeof(DescriptionAttribute), false)
            .Cast<DescriptionAttribute>()
            .Single().Description;

        Assert.That(description, Does.Not.Contain("NOT equivalent"));
        Assert.That(description, Contains.Substring("Am and C"));
    }
}
