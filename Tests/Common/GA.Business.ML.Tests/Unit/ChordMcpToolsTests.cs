namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;

[TestFixture]
public class ChordMcpToolsTests
{
    private static ChordMcpTools MakeTool() => new();

    // Canonical triads — what an LLM is most likely to call the tool for.
    [TestCase("C",      "C", "major",      new[] { "C", "E",  "G"  })]
    [TestCase("Cm",     "C", "minor",      new[] { "C", "Eb", "G"  })]
    [TestCase("Cdim",   "C", "diminished", new[] { "C", "Eb", "Gb" })]
    [TestCase("Caug",   "C", "augmented",  new[] { "C", "E",  "G#" })]
    [TestCase("F#",     "F#", "major",     new[] { "F#", "A#", "C#" })]
    [TestCase("Bbm",    "Bb", "minor",     new[] { "Bb", "Db", "F"  })]
    public void GetChordInfo_KnownTriads_ReturnsCorrectNotes(
        string symbol, string expectedRoot, string expectedQuality, string[] expectedNotes)
    {
        var result = MakeTool().GetChordInfo(symbol);

        Assert.That(result.Error,   Is.Null,                 $"valid input must not produce an Error for {symbol}");
        Assert.That(result.Root,    Is.EqualTo(expectedRoot));
        Assert.That(result.Quality, Is.EqualTo(expectedQuality));
        Assert.That(result.Notes,   Is.EqualTo(expectedNotes), $"notes mismatch for {symbol}");
    }

    [TestCase("C7",     "C", "dominant 7", new[] { "C", "E",  "G", "Bb" })]
    [TestCase("Cmaj7",  "C", "major 7",    new[] { "C", "E",  "G", "B"  })]
    [TestCase("Cm7",    "C", "minor 7",    new[] { "C", "Eb", "G", "Bb" })]
    [TestCase("Bbmaj7", "Bb", "major 7",   new[] { "Bb", "D",  "F", "A"  })]
    [TestCase("F#m7",   "F#", "minor 7",   new[] { "F#", "A",  "C#", "E" })]
    public void GetChordInfo_SeventhChords_ReturnsCorrectNotes(
        string symbol, string expectedRoot, string expectedQuality, string[] expectedNotes)
    {
        var result = MakeTool().GetChordInfo(symbol);

        Assert.That(result.Error,   Is.Null);
        Assert.That(result.Root,    Is.EqualTo(expectedRoot));
        Assert.That(result.Quality, Is.EqualTo(expectedQuality));
        Assert.That(result.Notes,   Is.EqualTo(expectedNotes));
    }

    [Test]
    public void GetChordInfo_EnharmonicSpelling_PrefersLetterStepsOverPitchClass()
    {
        // C minor must spell as C-Eb-G, NOT C-D#-G. The pitch classes are
        // identical but the letter-steps math forces the right enharmonic
        // (D# would be a doubled letter — root is C, third must be E-letter).
        var result = MakeTool().GetChordInfo("Cm");

        Assert.That(result.Notes[1], Is.EqualTo("Eb"),
            "minor third of C must spell as Eb (E with flat), not D# — letter-steps math drives enharmonics");
    }

    [Test]
    public void GetChordInfo_DiminishedSpelling_HandlesUnusualLetters()
    {
        // Bdim → B-D-F is the textbook spelling. The diminished fifth is F,
        // not E# (same pitch class but wrong letter step).
        var result = MakeTool().GetChordInfo("Bdim");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Notes, Is.EqualTo(new[] { "B", "D", "F" }));
    }

    [Test]
    public void GetChordInfo_NormalizesSymbolCasing()
    {
        // Lowercase root and short minor — common LLM emission patterns.
        var result = MakeTool().GetChordInfo("cm7");

        Assert.That(result.Error,   Is.Null);
        Assert.That(result.Root,    Is.EqualTo("C"));
        Assert.That(result.Quality, Is.EqualTo("minor 7"));
    }

    [TestCase("")]
    [TestCase("Q")]
    [TestCase("Qmaj7")]
    [TestCase("Cnotaquality")]
    [TestCase("not a chord")]
    public void GetChordInfo_InvalidInputs_ReturnFailureResult(string symbol)
    {
        var result = MakeTool().GetChordInfo(symbol);

        Assert.That(result.Error, Is.Not.Null,
            $"invalid input '{symbol}' must populate Error rather than throw");
        Assert.That(result.Notes, Is.Empty);
    }

    [Test]
    public void GetChordInfo_DoesNotThrowOnNullArgument()
    {
        Assert.That(() => MakeTool().GetChordInfo(null!), Throws.Nothing);
        var result = MakeTool().GetChordInfo(null!);
        Assert.That(result.Error, Is.Not.Null);
    }

    [Test]
    public void GetChordInfo_PathologicallyLongInput_ReturnsErrorWithoutScanning()
    {
        // Length guard short-circuits before regex, avoiding a multi-MB scan.
        var huge = new string('C', 10_000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = MakeTool().GetChordInfo(huge);
        sw.Stop();

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10),
            "long-input rejection must short-circuit, not scan the whole string");
    }

    [Test]
    public void GetChordInfo_ControlCharsInError_AreSanitized()
    {
        var esc      = (char)0x1B;
        var injected = "Q\n\r" + esc + "[31mFAKE";
        var result   = MakeTool().GetChordInfo(injected);

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.IndexOf('\n'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf('\r'), Is.EqualTo(-1));
        Assert.That(result.Error.IndexOf(esc),  Is.EqualTo(-1));
        Assert.That(result.Error.All(c => !char.IsControl(c)), Is.True);
    }

    [Test]
    public void GetChordInfo_DefaultsToMajorWhenNoQualitySuffix()
    {
        // Bare "C" without any quality suffix should yield C major. This is
        // the most common shorthand and the LLM will emit it constantly.
        var result = MakeTool().GetChordInfo("C");

        Assert.That(result.Error,   Is.Null);
        Assert.That(result.Quality, Is.EqualTo("major"));
        Assert.That(result.Notes,   Is.EqualTo(new[] { "C", "E", "G" }));
    }

    // Regression suite for the PR #80 review's BLOCK finding: the case-sensitive
    // "M" / "M7" alternates were unreachable because NormalizeQuality lowercased
    // the input first, so `CM` silently resolved to C minor and `CM7` to C minor 7.
    // The SKILL.md advertises CM=major / CM7=major 7 — these tests pin the
    // documented contract so the regression cannot recur.
    [TestCase("CM",   "C", "major",   new[] { "C", "E",  "G"        })]
    [TestCase("CM7",  "C", "major 7", new[] { "C", "E",  "G",  "B"  })]
    [TestCase("FM7",  "F", "major 7", new[] { "F", "A",  "C",  "E"  })]
    [TestCase("BbM7", "Bb", "major 7", new[] { "Bb", "D", "F",  "A"  })]
    public void GetChordInfo_UppercaseMQualifier_ResolvesToMajor(
        string symbol, string expectedRoot, string expectedQuality, string[] expectedNotes)
    {
        var result = MakeTool().GetChordInfo(symbol);

        Assert.That(result.Error,   Is.Null,                 $"valid input must not produce an Error for {symbol}");
        Assert.That(result.Root,    Is.EqualTo(expectedRoot));
        Assert.That(result.Quality, Is.EqualTo(expectedQuality),
            $"'{symbol}' must resolve to {expectedQuality}, NOT minor (regression from PR #80 review)");
        Assert.That(result.Notes,   Is.EqualTo(expectedNotes), $"notes mismatch for {symbol}");
    }

    [TestCase("Csus4")]
    [TestCase("C9")]
    [TestCase("Cadd9")]
    [TestCase("C/E")]
    public void GetChordInfo_OutOfScopeSymbols_ReturnError(string symbol)
    {
        // SKILL.md explicitly declines extended / altered / sus / slash chords.
        // Tool must reject cleanly rather than fabricating notes — this is the
        // failure mode the LLM is most likely to expose.
        var result = MakeTool().GetChordInfo(symbol);

        Assert.That(result.Error, Is.Not.Null,
            $"out-of-scope chord symbol '{symbol}' must populate Error rather than return fabricated notes");
        Assert.That(result.Notes, Is.Empty);
    }

    [Test]
    public void GetChordInfo_IntervalsSurfacedAsHumanReadable()
    {
        // Major chord intervals are the load-bearing label — root, major 3rd,
        // perfect 5th — these are what the LLM phrases the answer with.
        var result = MakeTool().GetChordInfo("C");

        Assert.That(result.Intervals, Is.EqualTo(new[] { "root", "major third", "perfect fifth" }));
    }

    // Diminished seventh (dim7) — stacked minor thirds. Cdim7's seventh is a
    // double-flat B because letter-step math forces the consecutive-odd-letters
    // spelling: C-E-G-B with each lowered (Eb-Gb-Bbb). Adim7 / Bbdim7 also
    // pinned because they exercise different enharmonic paths.
    [TestCase("Cdim7",  "C",  "diminished 7", new[] { "C",  "Eb", "Gb", "Bbb" })]
    [TestCase("Adim7",  "A",  "diminished 7", new[] { "A",  "C",  "Eb", "Gb"  })]
    [TestCase("Bbdim7", "Bb", "diminished 7", new[] { "Bb", "Db", "Fb", "Abb" })]
    [TestCase("F#dim7", "F#", "diminished 7", new[] { "F#", "A",  "C",  "Eb"  })]
    public void GetChordInfo_DiminishedSeventh_ReturnsCorrectNotes(
        string symbol, string expectedRoot, string expectedQuality, string[] expectedNotes)
    {
        var result = MakeTool().GetChordInfo(symbol);

        Assert.That(result.Error,   Is.Null,                 $"valid input must not produce an Error for {symbol}");
        Assert.That(result.Root,    Is.EqualTo(expectedRoot));
        Assert.That(result.Quality, Is.EqualTo(expectedQuality));
        Assert.That(result.Notes,   Is.EqualTo(expectedNotes), $"notes mismatch for {symbol}");
    }

    // Half-diminished (m7b5) — minor seventh with a flat fifth. Classic ii° in
    // a minor key: Bm7b5 = B D F A is the ii° of A minor. Am7b5 was previously
    // in the OutOfScope regression list (now removed alongside this addition).
    [TestCase("Cm7b5",  "C",  "half-diminished", new[] { "C",  "Eb", "Gb", "Bb" })]
    [TestCase("Bm7b5",  "B",  "half-diminished", new[] { "B",  "D",  "F",  "A"  })]
    [TestCase("F#m7b5", "F#", "half-diminished", new[] { "F#", "A",  "C",  "E"  })]
    [TestCase("Am7b5",  "A",  "half-diminished", new[] { "A",  "C",  "Eb", "G"  })]
    public void GetChordInfo_HalfDiminished_ReturnsCorrectNotes(
        string symbol, string expectedRoot, string expectedQuality, string[] expectedNotes)
    {
        var result = MakeTool().GetChordInfo(symbol);

        Assert.That(result.Error,   Is.Null,                 $"valid input must not produce an Error for {symbol}");
        Assert.That(result.Root,    Is.EqualTo(expectedRoot));
        Assert.That(result.Quality, Is.EqualTo(expectedQuality));
        Assert.That(result.Notes,   Is.EqualTo(expectedNotes), $"notes mismatch for {symbol}");
    }

    [TestCase("min7b5", "half-diminished")]
    public void GetChordInfo_AlternateQualityForms_NormalizeToCanonical(string suffix, string expectedCanonical)
    {
        // The verbose form `min7b5` is uncommon but legal — pin it so a future
        // refactor of NormalizeQuality doesn't silently drop it. The full word
        // `diminished7` is NOT in the regex on purpose — `dim7` is the only
        // canonical short form for the regex; the NormalizeQuality switch
        // accepts the full word as defensive code if the regex ever expands.
        var result = MakeTool().GetChordInfo("C" + suffix);

        Assert.That(result.Error,   Is.Null);
        Assert.That(result.Quality, Is.EqualTo(expectedCanonical));
    }
}
