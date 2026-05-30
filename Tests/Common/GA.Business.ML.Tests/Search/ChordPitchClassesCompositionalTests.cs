namespace GA.Business.ML.Tests.Search;

using System.Linq;
using GA.Business.ML.Search;

/// <summary>
/// Exhaustiveness tests for the COMPOSITIONAL chord-symbol parser
/// (<see cref="ChordPitchClasses.TryParse"/>). The parser builds pitch classes
/// algorithmically from base triad + spine + alteration/addition/omission tokens,
/// replacing the old finite quality lookup table. These tests assert it resolves the
/// full combinatorial space — including the jazz qualities (7b13, add11, maj7#5, 7#11 …)
/// that previously fell through to a hallucinating LLM fallback.
/// </summary>
[TestFixture]
public class ChordPitchClassesCompositionalTests
{
    // All 17 canonical roots the parser accepts.
    private static readonly string[] Roots =
        ["C", "C#", "Db", "D", "D#", "Eb", "E", "F", "F#", "Gb", "G", "G#", "Ab", "A", "A#", "Bb", "B"];

    // A broad quality matrix: triads, sixths, sevenths, extensions, suspensions, additions,
    // and a wide span of altered/extended jazz qualities + notation variants.
    private static readonly string[] Qualities =
    [
        "", "maj", "m", "min", "dim", "aug", "+", "sus2", "sus4", "sus", "5",
        "6", "m6", "69", "6/9", "m6/9",
        "7", "maj7", "M7", "△7", "m7", "min7", "m7b5", "ø7", "dim7", "°7", "7sus4", "+7", "aug7",
        "9", "maj9", "m9", "11", "maj11", "m11", "13", "maj13", "m13",
        "add9", "madd9", "add11", "add2", "add4", "sus2add11", "sus4add9",
        "mmaj7", "minmaj7", "m(maj7)",
        "7b5", "7#5", "7b9", "7#9", "7#11", "7b13", "7sus4b9", "7alt",
        "9#11", "9b5", "9#5", "13#9", "13b9", "13#11",
        "maj7#5", "maj7b5", "maj9#11", "m7#5", "m9b5", "m11b5",
    ];

    [Test]
    public void EveryRootTimesQuality_ParsesToNonEmptyPitchClasses()
    {
        var failures = new List<string>();

        foreach (var root in Roots)
        foreach (var quality in Qualities)
        {
            var symbol = root + quality;
            if (!ChordPitchClasses.TryParse(symbol, out var r, out var pcs))
            {
                failures.Add($"{symbol} → did not parse");
                continue;
            }

            if (r is null) failures.Add($"{symbol} → null root");
            if (pcs.Length < 2) failures.Add($"{symbol} → only {pcs.Length} pitch class(es)");
            if (pcs.Distinct().Count() != pcs.Length) failures.Add($"{symbol} → duplicate pcs [{string.Join(",", pcs)}]");
            if (pcs.Any(p => p is < 0 or > 11)) failures.Add($"{symbol} → pc out of range [{string.Join(",", pcs)}]");
        }

        Assert.That(failures, Is.Empty,
            $"{failures.Count} symbol(s) failed:\n{string.Join("\n", failures.Take(40))}");
    }

    // Exact pitch-class sets. Root C ⇒ root pitch class 0, so pitch classes == offsets.
    [TestCase("C", new[] { 0, 4, 7 })]
    [TestCase("Cm", new[] { 0, 3, 7 })]
    [TestCase("Cdim", new[] { 0, 3, 6 })]
    [TestCase("Caug", new[] { 0, 4, 8 })]
    [TestCase("C5", new[] { 0, 7 })]
    [TestCase("Csus2", new[] { 0, 2, 7 })]
    [TestCase("Csus4", new[] { 0, 5, 7 })]
    [TestCase("C6", new[] { 0, 4, 7, 9 })]
    [TestCase("Cm6", new[] { 0, 3, 7, 9 })]
    [TestCase("C6/9", new[] { 0, 2, 4, 7, 9 })]
    [TestCase("C7", new[] { 0, 4, 7, 10 })]
    [TestCase("Cmaj7", new[] { 0, 4, 7, 11 })]
    [TestCase("CM7", new[] { 0, 4, 7, 11 })]
    [TestCase("C△7", new[] { 0, 4, 7, 11 })]
    [TestCase("Cm7", new[] { 0, 3, 7, 10 })]
    [TestCase("Cm7b5", new[] { 0, 3, 6, 10 })]
    [TestCase("Cø7", new[] { 0, 3, 6, 10 })]
    [TestCase("Cdim7", new[] { 0, 3, 6, 9 })]
    [TestCase("Cmmaj7", new[] { 0, 3, 7, 11 })]
    [TestCase("Cm(maj7)", new[] { 0, 3, 7, 11 })]
    [TestCase("C7sus4", new[] { 0, 5, 7, 10 })]
    [TestCase("C9", new[] { 0, 2, 4, 7, 10 })]
    [TestCase("Cmaj9", new[] { 0, 2, 4, 7, 11 })]
    [TestCase("C13", new[] { 0, 2, 4, 5, 7, 9, 10 })]
    // The previously-broken jazz qualities:
    [TestCase("C7b13", new[] { 0, 4, 7, 8, 10 })]
    [TestCase("Cadd11", new[] { 0, 4, 5, 7 })]
    [TestCase("Cmaj7#5", new[] { 0, 4, 8, 11 })]
    [TestCase("C7#11", new[] { 0, 4, 6, 7, 10 })]
    [TestCase("C7#9", new[] { 0, 3, 4, 7, 10 })]
    [TestCase("C7b9", new[] { 0, 1, 4, 7, 10 })]
    [TestCase("Cm7#5", new[] { 0, 3, 8, 10 })]
    [TestCase("Cmadd9", new[] { 0, 2, 3, 7 })]
    // Extension omissions — the standard "13 without 11" and friends must resolve:
    [TestCase("C13no11", new[] { 0, 2, 4, 7, 9, 10 })]
    [TestCase("C9no3", new[] { 0, 2, 7, 10 })]
    [TestCase("Cmaj7no5", new[] { 0, 4, 11 })]
    public void ResolvesExactPitchClasses(string symbol, int[] expected)
    {
        Assert.That(ChordPitchClasses.TryParse(symbol, out var root, out var pcs), Is.True, $"{symbol} should parse");
        Assert.That(root, Is.EqualTo(0), $"{symbol} root should be C=0");
        Assert.That(pcs, Is.EqualTo(expected), $"{symbol} → [{string.Join(",", pcs)}]");
    }

    // A root letter followed by ordinary words must NOT parse as a chord — otherwise every
    // English word starting with A–G injects false musical structure into retrieval.
    [TestCase("Fade")]
    [TestCase("Bee")]
    [TestCase("Cab")]
    [TestCase("Dad")]
    [TestCase("Gem")]
    [TestCase("Bed")]
    [TestCase("Age")]
    [TestCase("Effect")]
    [TestCase("Garbage")]
    public void RejectsOrdinaryWords(string word)
    {
        Assert.That(ChordPitchClasses.TryParse(word, out _, out _), Is.False, $"{word} should NOT parse as a chord");
    }

    [Test]
    public void SlashBass_IsDroppedFromPitchClasses()
    {
        Assert.That(ChordPitchClasses.TryParse("Cmaj7/G", out _, out var pcs), Is.True);
        Assert.That(pcs, Is.EqualTo(new[] { 0, 4, 7, 11 }), "slash bass note must not add a pitch class");
    }
}
