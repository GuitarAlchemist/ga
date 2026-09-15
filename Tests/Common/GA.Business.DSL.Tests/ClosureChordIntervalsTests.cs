namespace GA.Business.DSL.Tests;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
/// Verifies domain.chordIntervals (and the closures sharing its chord-tone model)
/// against textbook chord spelling: one interval per chord degree, named by degree
/// (d5, not TT), with alterations applied and the seventh implied by 9/11/13 chords.
/// Expected values match <c>Chord.FromSymbol</c> in GA.Domain.Core.
/// </summary>
[TestFixture]
public class ClosureChordIntervalsTests
{
    [OneTimeSetUp]
    public void EnsureClosuresRegistered() =>
        GaClosureBootstrap.init();

    private static async Task<object?> Invoke(string closureName, params (string Key, object Value)[] inputs)
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
    [TestCase("Cm", "P1 m3 P5")]
    [TestCase("Cdim", "P1 m3 d5")]
    [TestCase("Caug", "P1 M3 A5")]
    [TestCase("Csus2", "P1 M2 P5")]
    [TestCase("Csus4", "P1 P4 P5")]
    [TestCase("C5", "P1 P5")]
    [TestCase("C6", "P1 M3 P5 M6")]
    [TestCase("C7", "P1 M3 P5 m7")]
    [TestCase("Cmaj7", "P1 M3 P5 M7")]
    [TestCase("Cm7", "P1 m3 P5 m7")]
    [TestCase("Cm7b5", "P1 m3 d5 m7")]
    [TestCase("Cdim7", "P1 m3 d5 d7")]
    [TestCase("C9", "P1 M3 P5 m7 M9")]
    [TestCase("Cmaj9", "P1 M3 P5 M7 M9")]
    [TestCase("Cm9", "P1 m3 P5 m7 M9")]
    [TestCase("Cadd9", "P1 M3 P5 M9")]
    [TestCase("C6/9", "P1 M3 P5 M6 M9")]
    [TestCase("C11", "P1 M3 P5 m7 M9 P11")]
    [TestCase("C13", "P1 M3 P5 m7 M9 P11 M13")]
    [TestCase("G7b9", "P1 M3 P5 m7 m9")]
    [TestCase("C7#9", "P1 M3 P5 m7 A9")]
    [TestCase("C7#11", "P1 M3 P5 m7 A11")]
    [TestCase("C7b13", "P1 M3 P5 m7 m13")]
    [TestCase("C7b5", "P1 M3 d5 m7")]
    [TestCase("C7#5", "P1 M3 A5 m7")]
    public async Task ChordIntervals_MatchTextbookSpelling(string symbol, string expected)
    {
        var value = await Invoke("domain.chordIntervals", ("symbol", symbol));
        Assert.That(string.Join(" ", (string[])value!), Is.EqualTo(expected));
    }

    [Test]
    public async Task ProjectChord_Intervals_UseTheSameChordTones()
    {
        var value = await Invoke("domain.projectChord", ("symbol", "Cm7b5"), ("fields", "intervals"));
        Assert.That(value, Is.EqualTo("intervals=P1 m3 d5 m7"));
    }

    [Test]
    public async Task CommonTones_SeeTheAlteredFifth()
    {
        // Cm7b5 = C Eb Gb Bb; Ebm = Eb Gb Bb — three shared tones, Gb is the d5 of Cm7b5.
        var value = (string)(await Invoke("domain.commonTones", ("chord1", "Cm7b5"), ("chord2", "Ebm")))!;
        Assert.That(value, Does.StartWith("Common tones (3):"));
        Assert.That(value, Contains.Substring("d5 in Cm7b5"));
    }

    [Test]
    public async Task ChordSubstitutions_Dim7_HasNoTritoneSub()
    {
        // Cdim7 has no major third or minor seventh: not a dominant.
        var value = (string)(await Invoke("domain.chordSubstitutions", ("symbol", "Cdim7"), ("key", "C")))!;
        Assert.That(value, Does.Not.Contain("tritone sub"));
    }

    [Test]
    public async Task QueryChords_HasInterval_AcceptsDegreeNames()
    {
        var d5 = (string[])(await Invoke("domain.queryChords", ("key", "C"), ("scale", "major"), ("hasInterval", "d5")))!;
        var tt = (string[])(await Invoke("domain.queryChords", ("key", "C"), ("scale", "major"), ("hasInterval", "TT")))!;
        Assert.That(d5, Is.EqualTo(new[] { "vii°=Bdim" }));
        Assert.That(tt, Is.EqualTo(d5));
    }

    [Test]
    public async Task QueryChords_HasInterval_IsCaseSensitive_MinorThirdIsNotMajorThird()
    {
        var m3 = (string[])(await Invoke("domain.queryChords", ("key", "C"), ("scale", "major"), ("hasInterval", "m3")))!;
        Assert.That(m3, Is.EqualTo(new[] { "ii=Dm", "iii=Em", "vi=Am", "vii°=Bdim" }));
    }
}
