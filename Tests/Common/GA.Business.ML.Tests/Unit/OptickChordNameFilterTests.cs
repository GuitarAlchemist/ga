namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Search;

/// <summary>
///     The OPTIC-K chord filter compares a requested symbol with the full chord name stored in the
///     index (CanonicalChordRecognizer output plus an optional slash bass), not a substring of it.
/// </summary>
[TestFixture]
public class OptickChordNameFilterTests
{
    [TestCase("Am7", "Am7")]
    [TestCase("Am7", "Am7/G")]
    [TestCase("Cmaj7", "Cmaj7(shell)")]
    [TestCase("C", "C")]
    [TestCase("C", "C/E")]
    [TestCase("F#m7b5", "F#m7b5")]
    [TestCase("Gbm7", "F#m7")]
    [TestCase("C6/9", "C6/9")]
    [TestCase("C6/9", "C6/9/E")]
    [TestCase("CM7", "Cmaj7")]
    [TestCase("Cmin", "Cm")]
    public void MatchesChordSymbol_SameRootAndQuality_Matches(string symbol, string storedName) =>
        Assert.That(OptickSearchStrategy.MatchesChordSymbol(storedName, symbol), Is.True);

    [TestCase("Am7", "Gbm7(shell)/A")]
    [TestCase("C", "C + E (Major 3rd)")]
    [TestCase("C", "Am/C")]
    [TestCase("C", "C5")]
    [TestCase("C", "Cm")]
    [TestCase("C7", "Cmaj7")]
    [TestCase("Cm", "Cm7")]
    [TestCase("Cmaj7", "Cmaj7#11")]
    [TestCase("Cm7", "C#m7")]
    [TestCase("Am7", null)]
    [TestCase("Am7", "Unknown")]
    public void MatchesChordSymbol_OtherChord_DoesNotMatch(string symbol, string? storedName) =>
        Assert.That(OptickSearchStrategy.MatchesChordSymbol(storedName, symbol), Is.False);
}
