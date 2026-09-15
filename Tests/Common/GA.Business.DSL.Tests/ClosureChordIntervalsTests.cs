namespace GA.Business.DSL.Tests;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
///     domain.chordIntervals and the closures built on the same chord tones must spell sevenths,
///     stacked extensions and alterations the way a theory textbook does.
/// </summary>
[TestFixture]
public class ClosureChordIntervalsTests
{
    [OneTimeSetUp]
    public void EnsureClosuresRegistered() =>
        GaClosureBootstrap.init();

    private static async Task<object> Invoke(string closureName, params (string Key, object Value)[] inputs)
    {
        var map = MapModule.OfSeq(inputs.Select(kv => Tuple.Create(kv.Key, kv.Value)));
        var result = await FSharpAsync.StartAsTask(
            Global.Invoke(closureName, map),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);
        Assert.That(result.IsOk, Is.True, () => $"{closureName} failed: {result.ErrorValue}");
        return result.ResultValue;
    }

    [TestCase("C", "P1 M3 P5")]
    [TestCase("Am", "P1 m3 P5")]
    [TestCase("G7", "P1 M3 P5 m7")]
    [TestCase("Cmaj7", "P1 M3 P5 M7")]
    [TestCase("Am7", "P1 m3 P5 m7")]
    [TestCase("Cm7b5", "P1 m3 d5 m7")]
    [TestCase("Cdim", "P1 m3 d5")]
    [TestCase("Cdim7", "P1 m3 d5 d7")]
    [TestCase("Caug", "P1 M3 A5")]
    [TestCase("C9", "P1 M3 P5 m7 M9")]
    [TestCase("Cmaj9", "P1 M3 P5 M7 M9")]
    [TestCase("G7b9", "P1 M3 P5 m7 m9")]
    [TestCase("G7#9", "P1 M3 P5 m7 A9")]
    [TestCase("C13", "P1 M3 P5 m7 M9 M13")]
    [TestCase("Cadd9", "P1 M3 P5 M9")]
    [TestCase("C6", "P1 M3 P5 M6")]
    [TestCase("Csus4", "P1 P4 P5")]
    [TestCase("C5", "P1 P5")]
    public async Task ChordIntervals_SpellsChordTones(string symbol, string expected)
    {
        var intervals = (string[])await Invoke("domain.chordIntervals", ("symbol", symbol));

        Assert.That(string.Join(' ', intervals), Is.EqualTo(expected));
    }

    [Test]
    public async Task ProjectChord_Intervals_UseTheSameChordTones()
    {
        var row = (string)await Invoke("domain.projectChord", ("symbol", "Cm7b5"), ("fields", "intervals"));

        Assert.That(row, Is.EqualTo("intervals=P1 m3 d5 m7"));
    }

    [Test]
    public async Task CommonTones_Bm7b5AndG7_ShareTheFlatFifthAsSeventh()
    {
        var result = (string)await Invoke("domain.commonTones", ("chord1", "Bm7b5"), ("chord2", "G7"));

        // Bm7b5 = B D F A, G7 = G B D F: F is the d5 of Bm7b5 and the m7 of G7.
        Assert.That(result, Does.StartWith("Common tones (3)"));
        Assert.That(result, Contains.Substring("F (d5 in Bm7b5, m7 in G7)"));
    }
}
