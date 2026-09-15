namespace GaMcpServer.Tests;

using GaMcpServer.Tools;

/// <summary>
///     The atonal MCP tools turn DSL interval names into pitch classes and group set-class
///     equivalents by chord quality.
/// </summary>
[TestFixture]
public sealed class ChordAtonalToolTests
{
    [OneTimeSetUp]
    public void EnsureClosuresRegistered() =>
        GA.Business.DSL.GaClosureBootstrap.init();

    [TestCase("Cm7b5", "{C, Eb, F#, Bb} (4 tones)")]
    [TestCase("Cdim7", "{C, Eb, F#, A} (4 tones)")]
    [TestCase("G7b9", "{D, F, G, Ab, B} (5 tones)")]
    [TestCase("Caug", "{C, E, Ab} (3 tones)")]
    public async Task GaChordToSet_AlteredChord_KeepsEveryTone(string symbol, string expectedSet)
    {
        var card = await ChordAtonalTool.GaChordToSet(symbol);

        Assert.That(card, Contains.Substring(expectedSet));
    }

    [Test]
    public async Task GaSetClassSubs_MajorTriad_ListsMinorTriadsUnderTheirOwnQuality()
    {
        var result = await ChordAtonalTool.GaSetClassSubs("C");
        var lines = result.Split('\n');

        Assert.That(lines.Single(l => l.StartsWith("  [m] ")), Contains.Substring("Am"));
        Assert.That(lines.Single(l => l.StartsWith("  [maj] "))["  [maj] ".Length..], Does.Not.Contain("m"));
    }

    [Test]
    public async Task GaSetClassSubs_Dominant7_ListsHalfDiminishedInversions()
    {
        var result = await ChordAtonalTool.GaSetClassSubs("G7");

        Assert.That(result.Split('\n').Single(l => l.StartsWith("  [m7b5] ")), Contains.Substring("Bm7b5"));
    }
}
