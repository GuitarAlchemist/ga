namespace GA.Business.DSL.Tests;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
/// Verifies domain.chordSubstitutions closure against known music-theory results.
/// Tests use real chord symbols — no mocking.
/// </summary>
[TestFixture]
public class ClosureSubstitutionsTests
{
    [OneTimeSetUp]
    public void EnsureClosuresRegistered() =>
        GA.Business.DSL.GaClosureBootstrap.init();

    private static async Task<string> Invoke(string closureName, params (string Key, object Value)[] inputs)
    {
        var map = MapModule.OfSeq(inputs.Select(kv => Tuple.Create(kv.Key, kv.Value)));
        var result = await FSharpAsync.StartAsTask(
            Global.Invoke(closureName, map),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);
        return result.IsOk ? result.ResultValue?.ToString() ?? "" : $"Error: {result.ErrorValue}";
    }

    [Test]
    public async Task Am_InCMajor_SuggestsC_AndF_AsTopSubstitutions()
    {
        var result = await Invoke("domain.chordSubstitutions",
            ("symbol", "Am"), ("key", "C"), ("scale", "major"));

        // C (relative major) and F share 2 tones each with Am
        Assert.That(result, Contains.Substring("★★"));
        Assert.That(result, Contains.Substring("C"));
        Assert.That(result, Contains.Substring("F"));
    }

    [Test]
    public async Task Am_InCMajor_DoesNotReturnAmItself()
    {
        var result = await Invoke("domain.chordSubstitutions",
            ("symbol", "Am"), ("key", "C"), ("scale", "major"));

        // Should not suggest Am as a substitution for Am — the ★ lines should not contain Am
        var subLines = result.Split('\n').Skip(1); // skip header line
        Assert.That(string.Join('\n', subLines), Does.Not.Contain("Am"));
    }

    [Test]
    public async Task G7_InCMajor_SuggestsBdim_AsThreeStarSub()
    {
        var result = await Invoke("domain.chordSubstitutions",
            ("symbol", "G7"), ("key", "C"), ("scale", "major"));

        // Bdim shares B, D, F with G7 (3 tones = ★★★)
        Assert.That(result, Contains.Substring("★★★"));
        Assert.That(result, Contains.Substring("Bdim"));
    }

    [Test]
    public async Task G7_InCMajor_IncludesTritoneSub_Db7()
    {
        var result = await Invoke("domain.chordSubstitutions",
            ("symbol", "G7"), ("key", "C"), ("scale", "major"));

        Assert.That(result, Contains.Substring("Db7"));
        Assert.That(result, Contains.Substring("tritone sub"));
    }

    [Test]
    public async Task Cmaj7_HasNoTritoneSub_NotDominant()
    {
        var result = await Invoke("domain.chordSubstitutions",
            ("symbol", "Cmaj7"), ("key", "C"), ("scale", "major"));

        // Cmaj7 is not a dominant 7th — no tritone sub
        Assert.That(result, Does.Not.Contain("tritone sub"));
    }

    [Test]
    public async Task Am_InAMinor_ReturnsMinorScaleSubs()
    {
        var result = await Invoke("domain.chordSubstitutions",
            ("symbol", "Am"), ("key", "A"), ("scale", "minor"));

        // In A minor, Am (i) is the tonic — expect related minor-key chords
        Assert.That(result, Contains.Substring("key of A minor"));
        Assert.That(result, Does.Not.StartWith("Error"));
    }
}
