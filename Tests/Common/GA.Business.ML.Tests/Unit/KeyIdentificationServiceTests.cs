namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Domain.Services.Tonal;
using NUnit.Framework;

[TestFixture]
public class KeyIdentificationServiceTests
{
    // ── ExtractChords ─────────────────────────────────────────────────────────

    [TestCase("Am F C G",         new[] { "Am", "F", "C", "G" })]
    [TestCase("D, A, Bm, G",      new[] { "D", "A", "Bm", "G" })]
    [TestCase("What key is Em C G D?", new[] { "Em", "C", "G", "D" })]
    [TestCase("I play Dm then Bb then F then C", new[] { "Dm", "Bb", "F", "C" })]
    public void ExtractChords_ShouldParseExpectedSymbols(string query, string[] expected)
    {
        var result = KeyIdentificationService.ExtractChords(query);
        Assert.That(expected, Is.SubsetOf(result));
    }

    // Issue #771: the pattern ended with \b, and there is no word boundary between "#" and a
    // space, so the regex gave the sharp back and matched the bare letter.
    [TestCase("What key is F# B C# F# in?", new[] { "F#", "B", "C#", "F#" })]
    [TestCase("What key is B E F# B in?", new[] { "B", "E", "F#", "B" })]
    [TestCase("C#, F#, G#", new[] { "C#", "F#", "G#" })]
    // Unicode accidentals are read as b and #; ° and ø are the diminished and half-diminished
    // signs, Δ a major seventh, and a slash chord is one chord.
    [TestCase("What key is B♭ E♭ F♯m in?", new[] { "Bb", "Eb", "F#m" })]
    [TestCase("C° Cø7 CΔ7 C/E D/F#", new[] { "C°", "Cø7", "CΔ7", "C/E", "D/F#" })]
    // Order and repeats are kept: the cadence weight reads the last two chords.
    [TestCase("What key is C D G C in?", new[] { "C", "D", "G", "C" })]
    public void ExtractChords_ReadsTheProgressionAsWritten(string query, string[] expected) =>
        Assert.That(KeyIdentificationService.ExtractChords(query), Is.EqualTo(expected));

    // ── IsKeyIdentificationQuery ──────────────────────────────────────────────

    [TestCase("What key am I in if I play Am F C G?", true)]
    [TestCase("Which key is Am F C G?", true)]
    [TestCase("What scale fits Am F C G?", true)]
    [TestCase("Identify the key for D A Bm G", true)]
    [TestCase("Show me a C major scale", false)]           // no 2+ chords
    [TestCase("What is a tritone substitution?", false)]   // no chord progression
    [TestCase("What key is C C in?", false)]               // one chord, repeated
    public void IsKeyIdentificationQuery_ShouldDetectCorrectly(string query, bool expected) =>
        Assert.That(KeyIdentificationService.IsKeyIdentificationQuery(query), Is.EqualTo(expected));

    // ── Identify — exact matches ──────────────────────────────────────────────

    [Test]
    public void Identify_AmFCG_ShouldReturnCMajorAndAMinorAsTied()
    {
        var chords = new[] { "Am", "F", "C", "G" };
        var results = KeyIdentificationService.Identify(chords);
        var top = TopTied(results);

        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "C major"));
        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "A minor"));
        Assert.That(top[0].MatchCount, Is.EqualTo(4)); // all 4 chords match
    }

    [Test]
    public void Identify_DABmG_ShouldReturnDMajorAndBMinorAsTied()
    {
        var chords = new[] { "D", "A", "Bm", "G" };
        var results = KeyIdentificationService.Identify(chords);
        var top = TopTied(results);

        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "D major"));
        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "B minor"));
        Assert.That(top[0].MatchCount, Is.EqualTo(4));
    }

    [Test]
    public void Identify_EmCGD_ShouldReturnGMajorAndEMinorAsTied()
    {
        var chords = new[] { "Em", "C", "G", "D" };
        var results = KeyIdentificationService.Identify(chords);
        var top = TopTied(results);

        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "G major"));
        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "E minor"));
        Assert.That(top[0].MatchCount, Is.EqualTo(4));
    }

    [Test]
    public void Identify_DmBbFC_ShouldReturnFMajorAndDMinorAsTied()
    {
        var chords = new[] { "Dm", "Bb", "F", "C" };
        var results = KeyIdentificationService.Identify(chords);
        var top = TopTied(results);

        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "F major"));
        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "D minor"));
        Assert.That(top[0].MatchCount, Is.EqualTo(4));
    }

    [Test]
    public void Identify_EAB_ShouldReturnEMajorAsTopCandidate()
    {
        // E A B are all in E major (I IV V); B minor is missing "A" as major
        var chords = new[] { "E", "A", "B" };
        var results = KeyIdentificationService.Identify(chords);
        var top = TopTied(results);

        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "E major"));
    }

    // V7/vi must not pull a major progression toward its relative minor (corpus case pc-07),
    // and the authentic cadence G7 -> C puts C major first among the tied keys.
    [Test]
    public void Identify_SecondaryDominantInMajor_KeepsTheMajorKeyOnTop()
    {
        var results = KeyIdentificationService.Identify(new[] { "C", "E7", "Am", "F", "G7", "C" });

        Assert.Multiple(() =>
        {
            Assert.That(TopTied(results).Select(c => c.Key), Does.Contain("C major"));
            Assert.That(results[0].Key, Is.EqualTo("C major"));
        });
    }

    // The harmonic-minor V7 is not counted as diatonic, but resolving it to i decides the order.
    // F# minor and A major tie on the count; by name alone A major would be listed first.
    [Test]
    public void Identify_HarmonicMinorCadence_ListsTheMinorKeyFirst()
    {
        var results = KeyIdentificationService.Identify(new[] { "F#m", "C#7", "F#m" });

        Assert.That(results[0].Key, Is.EqualTo("F# minor"));
    }

    // Relative keys tie on every progression that stays diatonic; the first chord's key wins,
    // not the alphabet.
    [TestCase(new[] { "Am", "F", "C", "G" }, "A minor")]
    [TestCase(new[] { "C", "G", "Am", "F" }, "C major")]
    [TestCase(new[] { "Em", "C", "G", "D" }, "E minor")]
    [TestCase(new[] { "G", "D", "Em", "C" }, "G major")]
    [TestCase(new[] { "Dm7", "G7" }, "C major")] // neither tied key opens it: the major one
    public void Identify_RelativeKeyTie_GoesToTheKeyOfTheFirstChord(string[] chords, string expected) =>
        Assert.That(KeyIdentificationService.Identify(chords)[0].Key, Is.EqualTo(expected));

    [TestCase("A minor", "E7", true)]
    [TestCase("A minor", "E", true)]
    [TestCase("A minor", "Em", true)]
    [TestCase("C major", "E7", false)]
    public void IsChordDiatonic_HarmonicMinorDominant_BelongsToTheMinorKey(string key, string chord, bool expected) =>
        Assert.That(KeyIdentificationService.IsChordDiatonic(key, chord), Is.EqualTo(expected));

    // ── Extensions stripped correctly ─────────────────────────────────────────

    [Test]
    public void Identify_ShouldNormaliseExtensions()
    {
        // G7 → G, Cmaj7 → C, Am7 → Am — all diatonic to C major
        var chords = new[] { "G7", "Cmaj7", "Am7", "F" };
        var results = KeyIdentificationService.Identify(chords);
        var top = TopTied(results);

        Assert.That(top, Has.Some.Matches<KeyIdentificationService.KeyCandidate>(c => c.Key == "C major"));
        Assert.That(top[0].MatchCount, Is.EqualTo(4));
    }

    // Issue #771: ExtractChords dropped the repeated C, so the cadence weight heard the cut-off
    // ending, D G, as V–I of G major.
    [Test]
    public void Identify_ReturnToTheTonic_GetsTheCadenceWeight()
    {
        var results = KeyIdentificationService.Identify(
            KeyIdentificationService.ExtractChords("What key is C D G C in?"));

        Assert.That(results[0].Key, Is.EqualTo("C major"));
    }

    // Issue #771: read without its sharps, I IV V I in F# major was F B C, and C major came first.
    [Test]
    public void Identify_SharpKeys_KeepTheirSharps()
    {
        var results = KeyIdentificationService.Identify(
            KeyIdentificationService.ExtractChords("What key is F# B C# F# in?"));

        Assert.That(results[0].Key, Is.EqualTo("F# major"));
    }

    // Issue #771: NormalizeChord cut everything from the first digit, so Bm7b5 counted as a minor
    // triad, and ° and ø were read as major.
    [TestCase("C major", "Bm7b5", true)]   // vii°
    [TestCase("C major", "Bø7", true)]
    [TestCase("C major", "B°", true)]
    [TestCase("C major", "Bdim7", true)]
    [TestCase("A minor", "Bm7b5", true)]   // ii°
    [TestCase("A minor", "Bm", false)]
    [TestCase("C major", "CΔ7", true)]     // a major seventh, not a dominant
    [TestCase("C major", "C/E", true)]
    [TestCase("C major", "G7/B", true)]
    public void IsChordDiatonic_ReadsTheTriadUnderTheSymbol(string key, string chord, bool expected) =>
        Assert.That(KeyIdentificationService.IsChordDiatonic(key, chord), Is.EqualTo(expected));

    // iiø7 V7 i: the half-diminished ii now counts in A minor.
    [Test]
    public void Identify_MinorTwoFiveOne_CountsTheHalfDiminishedChord()
    {
        var results = KeyIdentificationService.Identify(["Bm7b5", "E7", "Am"]);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Key, Is.EqualTo("A minor"));
            Assert.That(results[0].MatchCount, Is.EqualTo(2));
        });
    }

    [TestCase("C major", "AM7")]
    [TestCase("Bb major", "GMIN7")]
    public void IsChordDiatonic_UppercaseMinorQuality_IsParsedAsMinor(string key, string chord) =>
        Assert.That(KeyIdentificationService.IsChordDiatonic(key, chord), Is.True);

    // ── Empty / no-match cases ────────────────────────────────────────────────

    [Test]
    public void Identify_EmptyInput_ShouldReturnEmpty() =>
        Assert.That(KeyIdentificationService.Identify([]), Is.Empty);

    [Test]
    public void Identify_UnrecognisedChords_ShouldReturnEmpty() =>
        Assert.That(KeyIdentificationService.Identify(["Qx", "Zy"]), Is.Empty);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<KeyIdentificationService.KeyCandidate> TopTied(
        IReadOnlyList<KeyIdentificationService.KeyCandidate> results)
    {
        if (results.Count == 0) return [];
        var topScore = results[0].MatchCount;
        return [.. results.Where(r => r.MatchCount == topScore)];
    }
}
